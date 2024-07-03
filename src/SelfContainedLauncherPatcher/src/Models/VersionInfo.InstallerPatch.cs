using System.Xml.Serialization;

namespace CsLauncher.Models;

public partial class VersionInfo
{
    public class InstallerPatchInfo
    {
        [XmlAttribute("CompressionFormat")]
        public string? CompressionFormat { get; init; }

        [XmlAttribute("PatchSize")]
        public int PatchSize { get; init; }

        [XmlAttribute("PatchMd5Hash")]
        public string? PatchMd5Hash { get; init; }

        [XmlAttribute("DownloadURL")]
        public string? DownloadURL { get; init; }

        public InstallerPatchInfo() { }
    }
}