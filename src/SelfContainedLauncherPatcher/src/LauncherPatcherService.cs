using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.Logging;
using SingleFileExtractor.Core;

namespace CsLauncher;

internal sealed class LauncherPatcherService
{
    private readonly ILogger<LauncherPatcherService> logger;

    public LauncherPatcherService(ILogger<LauncherPatcherService> logger)
    {
        this.logger = logger;
    }

    public async Task<bool> PatchLauncherAsync(string executablePath, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(executablePath, nameof(executablePath));
        ArgumentNullException.ThrowIfNull(outputPath, nameof(outputPath));

        using var reader = new ExecutableReader(executablePath);
        return await PatchLauncherAsync(reader, outputPath);
    }

    public async Task<bool> PatchLauncherAsync(ExecutableReader reader, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(reader, nameof(reader));
        ArgumentNullException.ThrowIfNull(outputPath, nameof(outputPath));

        try
        {
            Directory.CreateDirectory(outputPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create output directory: {outputPath}", outputPath);
            return false;
        }

        if (!reader.IsSupported)
        {
            logger.LogError("Executable is not supported: {executablePath}", reader.FileName);
            return false;
        }

        if (string.IsNullOrEmpty(reader.StartupInfo.EntryPoint))
        {
            logger.LogWarning("Bundle entry point not found, using guessed default entry point: {name}", Constants.Files.BundleMainAssemblyName);
        }
        else
        {
            logger.LogInformation("Bundle entry point assembly found: {entryPoint}", reader.StartupInfo.EntryPoint);
        }

        string entryPointAssemblyName = reader.StartupInfo.EntryPoint ?? Constants.Files.BundleMainAssemblyName;

        var fileEntries = reader.Bundle.Files;
        var mainAssemblyEntry = fileEntries
            .FirstOrDefault(x => Path.GetFileName(x.RelativePath) == entryPointAssemblyName);

        if (mainAssemblyEntry == null)
        {
            logger.LogError("Main assembly {assembly} not found on bundle", entryPointAssemblyName);
            return false;
        }

        using var assemblyStream = await mainAssemblyEntry.AsStreamAsync();

        using var bundleAssemblyResolver = new BundleAssemblyResolver(reader, keepOpen: true);
        ModuleContext modCtx = new(bundleAssemblyResolver);
        var mainAssemblyDef = AssemblyDef.Load(assemblyStream, modCtx);

        logger.LogInformation("Main assembly '{assembly}' loaded", mainAssemblyDef.FullName);

        var module = mainAssemblyDef.ManifestModule;
        var types = module.GetTypes().SelectMany(x => x.Methods).ToArray();

        var openMainWndScanner = new PatternScanner(Constants.OpenMainWindowPattern);
        var openMainWndMethods = types.AsParallel()
            .Where(x => Utils.MethodMatchesPattern(assemblyStream, x, openMainWndScanner))
            .ToArray();

        if (openMainWndMethods.Length == 0)
        {
            logger.LogError("Failed to locate the Open main window function");
            return false;
        }
        if (openMainWndMethods.Length > 1)
        {
            logger.LogError("Found more than one open main window function, unable to proceed");
            return false;
        }

        var openMainWndMethod = openMainWndMethods[0];
        var targetMethodType = openMainWndMethod.DeclaringType;
        var targetTypeCtor = targetMethodType.FindDefaultConstructor();
        if (targetTypeCtor == null)
        {
            logger.LogError("Failed to locate the constructor for the sign window");
            return false;
        }

        Instruction[] callOpenWnd =
        [
            OpCodes.Ldarg_0.ToInstruction(),
            OpCodes.Call.ToInstruction(openMainWndMethod),
            OpCodes.Ret.ToInstruction()
        ];

        var ctorIlCode = new LinkedList<Instruction>(targetTypeCtor.Body.Instructions);
        var ip = ctorIlCode.First;
        while (ip != null)
        {
            if (ip.Value.OpCode.Code == Code.Ret)
            {
                logger.LogDebug("Patching ret instruction [{offset:X}] at RVA: {rva:X}", ip.Value.Offset, Utils.GetRVA(targetTypeCtor));
                AddInstructionsAfter(ip, callOpenWnd);
                ctorIlCode.Remove(ip);
            }
            ip = ip.Next;

            void AddInstructionsAfter(LinkedListNode<Instruction> target, Instruction[] instructions)
            {
                foreach (var instr in instructions)
                {
                    ctorIlCode.AddAfter(target, instr);
                    target = target.Next!;
                }
            }
        }

        targetTypeCtor.Body.Instructions.Clear();
        foreach (var instr in ctorIlCode)
        {
            targetTypeCtor.Body.Instructions.Add(instr);
        }

        var patchedAttbType = new TypeDefUser("DefaultNamespace", "PatchedAttribute", module.CorLibTypes.Object.TypeDefOrRef)
        {
            Attributes = TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout |
                TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Class,
            BaseType = new TypeRefUser(
                module,
                nameof(System),
                nameof(System.Attribute),
                module.CorLibTypes.AssemblyRef)
        };
        var patchedAttbCtor = new MethodDefUser(".ctor",
            MethodSig.CreateInstance(module.CorLibTypes.Void),
            MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.HideBySig |
            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.ReuseSlot)
        {
            Body = new CilBody(true, [OpCodes.Ret.ToInstruction()], [], [])
        };
        patchedAttbType.Methods.Add(patchedAttbCtor);

        module.Types.Add(patchedAttbType);

        var patchedAttribute = new CustomAttribute(patchedAttbType.FindDefaultConstructor());
        module.Assembly.CustomAttributes.Add(patchedAttribute);
        module.IsILOnly = true;

        try
        {
            module.Write(Path.Combine(outputPath, entryPointAssemblyName));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write patched assembly");
            return false;
        }

        if (!await WriteBundleTo(outputPath, reader, [entryPointAssemblyName]))
        {
            logger.LogError("Failed to write bundle files");
            return false;
        }

        return true;
    }

    public static bool IsModulePatched(string modulePath)
    {
        ArgumentNullException.ThrowIfNull(modulePath, nameof(modulePath));

        using var moduleStream = new FileStream(modulePath, FileMode.Open, FileAccess.Read);
        using var moduleDef = ModuleDefMD.Load(moduleStream);
        return moduleDef.Assembly.CustomAttributes.Any(x => x.TypeFullName == "DefaultNamespace.PatchedAttribute");
    }

    private async Task<bool> WriteBundleTo(string outputPath, ExecutableReader reader, string[] ignoreFiles)
    {
        var files = reader.Bundle.Files
            .Where(x => !ignoreFiles.Contains(Path.GetFileName(x.RelativePath)))
            .ToArray();

        foreach (var file in files)
        {
            var filePath = Path.Combine(outputPath, file.RelativePath);
            var fileDir = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(fileDir);

            try
            {
                using var fileStream = await file.AsStreamAsync();
                using var outFs = new FileStream(filePath, FileMode.Create);
                await fileStream.CopyToAsync(outFs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write file {file}", filePath);
                return false;
            }
        }
        return true;
    }
}
