using System.Xml.Serialization;

namespace CsLauncher.Models;

public partial class VersionInfo
{
    public class VersionItem
    {
        [XmlAttribute("Path")]
        public string? Path { get; init; }

        [XmlAttribute("FileSize")]
        public long FileSize { get; init; }

        [XmlAttribute("Md5Hash")]
        public string? Md5Hash { get; init; }

        [XmlAttribute("CompressedFileSize")]
        public long CompressedFileSize { get; init; }

        [XmlAttribute("CompressedMd5Hash")]
        public string? CompressedMd5Hash { get; init; }

        [XmlAttribute("DownloadURL")]
        public string? DownloadURL { get; init; }

        public VersionItem() { }
    }
}


