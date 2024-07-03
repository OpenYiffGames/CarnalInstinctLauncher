
namespace CiLauncher;

static class Constants
{
    public static class Files
    {
        public const string ConfigurationFileName = "launcher-config.json";
        public const string BundleExecutableName = "Carnal Instinct Launcher.exe";
        public const string BundleMainAssemblyName = "Carnal Instinct Launcher.dll";
        public const string UpdaterFileName = "SelfPatcher.exe";
        public const string UpdaterCoreFileName = "SelfPatcherCore.dll";
        public const string LauncherVersionFile = "Launcher_version.data";
        public const string LauncherLoader = "run_launcher.exe";
        public const string LogFile = "launcher.log";

        public const string UpdaterFilePath = $"{Directories.UpdaterDirectory}/{UpdaterFileName}";
        public const string UpdaterCoreFilePath = $"{Directories.UpdaterDirectory}/{UpdaterCoreFileName}";
    }

    public static class Directories
    {
        public const string UpdaterDirectory = "SelfUpdater";
        public const string ResourcesDirectory = "Resources";
    }

    public static class CarnalCDN
    {
        public const string VersionInfoUrl = @"https://cdn.carnal-instinct.com/newhost/Launcher/VersionInfo.info";
        public const string DownloadPath = "RepairPatch/";
        public const string DownloadFileSuffix = ".lzdat";
        public const string IncrementaPatchPath = "IncrementalPatch/";
        public const string IncrementalPatchFileSuffix = ".patch";
    }

    public const string OpenMainWindowPattern = "0x28 ?? ?? ?? ?? 0x02 0x7B ?? ?? ?? ?? 0x6F ?? ?? ?? ?? 0x6F ?? ?? ?? ?? 0x28 ?? ?? ?? ?? 0x02 0x7B ?? ?? ?? ?? 0x7B ?? ?? ?? ?? 0x6F ?? ?? ?? ?? 0x28 ?? ?? ?? ?? 0x6F ?? ?? ?? ?? 0x28 ?? ?? ?? ?? 0x02 0x7B";
}
