using dnlib.DotNet;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CiLauncher;

static class Utils
{
    public static bool MethodMatchesPattern(Stream assemblySource, MethodDef method, PatternScanner patternScanner)
    {
        if (!method.HasBody)
        {
            return false;
        }
        byte[]? methodCilBody = GetCilBodyBytes(assemblySource, method, assemblySource) ?? throw new Exception("Failed to read method body");
        var offset = patternScanner.Find(methodCilBody);
        return offset >= 0;
    }

    public static byte[]? GetCilBodyBytes(Stream assemblySource, MethodDef method, Stream assemblyStream)
    {
        if (!method.HasBody)
        {
            return null;
        }
        long bodyOffset = GetRVA(method);
        if (bodyOffset < 0)
        {
            return null;
        }
        bool isBigHeader = method.Body.IsBigHeader;
        lock (assemblySource)
        {
            assemblyStream.Seek(bodyOffset, SeekOrigin.Begin);
            var header = ReadMethodHeader(assemblyStream, isBigHeader);
            byte[] methodCilBody = new byte[isBigHeader ? header.Fat_CodeSize : header.Tiny_Flags_CodeSize];
            assemblyStream.Read(methodCilBody);
            return methodCilBody;
        }
    }

    public static long GetRVA(MethodDef methodDef)
    {
        if (methodDef.Module is not ModuleDefMD module)
        {
            return -1;
        }
        return (long)module.Metadata.PEImage.ToFileOffset(methodDef.RVA);
    }

    public static IMAGE_COR_ILMETHOD ReadMethodHeader(Stream stream, bool isBigHeader)
    {
        int ImageCorILMethodSize = Marshal.SizeOf<IMAGE_COR_ILMETHOD>();
        Span<byte> buffer = stackalloc byte[ImageCorILMethodSize];
        if (!isBigHeader)
        {
            buffer = buffer[..1];
        }
        stream.Read(buffer);

        IntPtr ptr = Marshal.AllocHGlobal(ImageCorILMethodSize);
        try
        {
            var unmanagedBufferSpan = MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), ptr), ImageCorILMethodSize);
            buffer.CopyTo(unmanagedBufferSpan);
            return Marshal.PtrToStructure<IMAGE_COR_ILMETHOD>(ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public static string GetLocalStorageDirectory()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyGuid = (assembly.GetCustomAttribute<GuidAttribute>()?.Value) ?? assembly.GetName().FullName;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            assemblyGuid);
    }
}