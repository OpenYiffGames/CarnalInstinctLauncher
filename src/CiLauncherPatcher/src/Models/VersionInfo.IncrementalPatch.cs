using System.Xml.Serialization;

namespace CiLauncher.Models;

public partial class VersionInfo
{
    public class IncrementalPatch
    {
        [XmlAttribute("Files")]
        public int Files { get; init; }

        [XmlAttribute("CompressionFormat")]
        public string? CompressionFormat { get; init; }

        [XmlAttribute("PatchSize")]
        public int PatchSize { get; init; }

        [XmlAttribute("PatchMd5Hash")]
        public string? PatchMd5Hash { get; init; }

        [XmlAttribute("InfoURL")]
        public required string InfoURL { get; init; }

        [XmlAttribute("DownloadURL")]
        public string? DownloadURL { get; init; }
        public required string FromVersion { get; init; }
        public required string ToVersion { get; init; }

        public IncrementalPatch() { }
    }
}


