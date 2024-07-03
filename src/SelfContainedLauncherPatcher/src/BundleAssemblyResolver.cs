using dnlib.DotNet;
using SingleFileExtractor.Core;

namespace CsLauncher;

class BundleAssemblyResolver : IAssemblyResolver, IDisposable
{
    private readonly ExecutableReader _reader;
    public readonly bool KeepOpen;

    public BundleAssemblyResolver(ExecutableReader reader, bool keepOpen = false)
    {
        _reader = reader;
        KeepOpen = keepOpen;
    }

    public AssemblyDef? Resolve(IAssembly assembly, ModuleDef sourceModule)
    {
        foreach (var file in _reader.Bundle.Files)
        {
            string fileName = Path.GetFileName(file.RelativePath);
            if (fileName == assembly.Name)
            {
                using var stream = file.AsStream();
                return AssemblyDef.Load(stream);
            }
        }
        return null;
    }

    public void Dispose()
    {
        if (!KeepOpen)
        {
            _reader.Dispose();
        }
    }
}