using System.Xml.Linq;
using System.Xml.Serialization;

namespace CiLauncher.Models;

[XmlRoot("VersionInfo")]
public partial class VersionInfo
{
    public required string BaseDownloadURL { get; init; }
    public string? MaintenanceCheckURL { get; init; }
    public required string Version { get; init; }

    [XmlArray("Patches")]
    [XmlArrayItem("IncrementalPatch")]
    public List<IncrementalPatch>? Patches { get; init; }

    public InstallerPatchInfo? InstallerPatch { get; init; }

    [XmlArray("IgnoredPaths")]
    [XmlArrayItem("string")]
    public List<string>? IgnoredPaths { get; init; }

    public string? CompressionFormat { get; init; }

    [XmlArray("Files")]
    [XmlArrayItem("VersionItem")]
    public List<VersionItem>? Files { get; init; }

    public string? Name { get; init; }

    public VersionInfo() { }

    public static VersionInfo DeserializeXml(Stream source)
    {
        XDocument doc = XDocument.Load(source);
        XElement root = doc.Element("VersionInfo")!;

        VersionInfo versionInfo = new()
        {
            BaseDownloadURL = root.Element("BaseDownloadURL")?.Value!,
            MaintenanceCheckURL = root.Element("MaintenanceCheckURL")?.Value,
            Version = root.Element("Version")?.Value!,
            Patches = root.Element("Patches")?.Elements("IncrementalPatch").Select(p => new IncrementalPatch
            {
                Files = int.Parse(p.Attribute("Files")?.Value ?? "0"),
                CompressionFormat = p.Attribute("CompressionFormat")?.Value,
                PatchSize = int.Parse(p.Attribute("PatchSize")?.Value ?? "0"),
                PatchMd5Hash = p.Attribute("PatchMd5Hash")?.Value,
                InfoURL = p.Attribute("InfoURL")?.Value!,
                DownloadURL = p.Attribute("DownloadURL")?.Value,
                FromVersion = p.Element("FromVersion")?.Value!,
                ToVersion = p.Element("ToVersion")?.Value!
            }).ToList(),
            InstallerPatch = new InstallerPatchInfo
            {
                CompressionFormat = root.Element("InstallerPatch")?.Attribute("CompressionFormat")?.Value,
                PatchSize = int.Parse(root.Element("InstallerPatch")?.Attribute("PatchSize")?.Value ?? "0"),
                PatchMd5Hash = root.Element("InstallerPatch")?.Attribute("PatchMd5Hash")?.Value,
                DownloadURL = root.Element("InstallerPatch")?.Attribute("DownloadURL")?.Value
            },
            IgnoredPaths = root.Element("IgnoredPaths")?.Elements("string").Select(e => e.Value).ToList(),
            CompressionFormat = root.Element("CompressionFormat")?.Value,
            Files = root.Element("Files")?.Elements("VersionItem").Select(f => new VersionItem
            {
                Path = f.Attribute("Path")?.Value,
                FileSize = long.Parse(f.Attribute("FileSize")?.Value ?? "0"),
                Md5Hash = f.Attribute("Md5Hash")?.Value,
                CompressedFileSize = long.Parse(f.Attribute("CompressedFileSize")?.Value ?? "0"),
                CompressedMd5Hash = f.Attribute("CompressedMd5Hash")?.Value,
                DownloadURL = f.Attribute("DownloadURL")?.Value
            }).ToList(),
            Name = root.Element("Name")?.Value
        };

        return versionInfo;
    }
}


