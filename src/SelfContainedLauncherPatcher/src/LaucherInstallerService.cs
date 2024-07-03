using Microsoft.Extensions.Logging;
using Octodiff.Core;
using CsLauncher.Models;
using SingleFileExtractor.Core;
using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

namespace CsLauncher;

internal sealed class LauncherInstallerService
{
    private readonly ILogger<LauncherInstallerService> _logger;
    private readonly HttpClient _httpClient;
    private readonly LauncherConfigurationStore _configStore;
    private readonly LauncherPatcherService launcherPatcher;

    public LauncherInstallerService(
        ILogger<LauncherInstallerService> logger,
        HttpClient httpClient,
        LauncherConfigurationStore configStore,
        LauncherPatcherService launcherPatcher)
    {
        _logger = logger;
        _httpClient = httpClient;
        _configStore = configStore;
        this.launcherPatcher = launcherPatcher;
    }

    public async Task<bool> InstallLauncherAsync(string installationPath)
    {
        var versionInfo = await DownloadVesionInfo();
        if (versionInfo == null)
        {
            _logger.LogError("Failed to download version info");
            return false;
        }

        Directory.CreateDirectory(installationPath);

        _logger.LogInformation("Installing launcher to {path}", installationPath);
        await _configStore.SetInstallDirectoryAsync(installationPath);

        string executableFileName = Constants.Files.BundleExecutableName;
        (string path, string fileName)[] downloadFiles =
        [
            (Constants.Files.BundleExecutableName, executableFileName),
            (Constants.Files.UpdaterFilePath, Constants.Files.UpdaterFileName),
            (Constants.Files.UpdaterCoreFilePath, Constants.Files.UpdaterCoreFileName)
        ];

        foreach (var (path, fileName) in downloadFiles)
        {
            string serverFilePath = Path.Combine(
                Constants.CarnalCDN.DownloadPath,
                Uri.EscapeDataString(path)
            ) + Constants.CarnalCDN.DownloadFileSuffix;
            var baseUri = new Uri(versionInfo.BaseDownloadURL);
            var uri = new UriBuilder(baseUri)
            {
                Path = Path.Combine(baseUri.AbsolutePath, serverFilePath)
            }.Uri;
            string outPath = Path.Combine(installationPath, Path.GetDirectoryName(path) ?? string.Empty, fileName);
            if (File.Exists(outPath))
            {
                _logger.LogInformation("File {path} already exists", path);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            using var launcherStream = await _httpClient.GetStreamAsync(uri);
            using var launcherFs = new FileStream(outPath, FileMode.Create);
            _logger.LogInformation("Downloading {path} from {url}", path, uri);
            await launcherStream.CopyToAsync(launcherFs);
        }

        // TODO: Fix this
        //if (!await CheckForUpdates(versionInfo))
        //{
        //    _logger.LogError("Failed to apply updates");
        //    return false;
        //}

        string executablePath = Path.Combine(installationPath, executableFileName);
        try
        {
            var patchResult = await launcherPatcher.PatchLauncherAsync(
                executablePath,
                installationPath
            );
            if (!patchResult)
            {
                _logger.LogError("Failed to patch launcher");
                return false;
            }
        }
        finally
        {
            File.Delete(executablePath);
        }

        File.WriteAllText(Path.Combine(installationPath, Constants.Files.LauncherVersionFile), versionInfo.Version);

        using var launcherLoaderFs = new FileStream(Path.Combine(installationPath, Constants.Files.LauncherLoader), FileMode.Create);
        var loaderRs = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            $"{nameof(CsLauncher)}.{Constants.Directories.ResourcesDirectory}.{Constants.Files.LauncherLoader}");
        if (loaderRs == null)
        {
            _logger.LogError("Failed to load launcher loader resource");
            return false;
        }
        await loaderRs.CopyToAsync(launcherLoaderFs);

        return true;
    }

    public async Task<bool> ReinstallLauncherAsync()
    {
        string installationDir = _configStore.InstallDirectory ?? throw new InvalidOperationException("Install directory not set");
        Directory.GetFiles(installationDir, "*.dll", SearchOption.AllDirectories)
            .ToList()
            .ForEach(File.Delete);

        var bundleExecutable = Path.Combine(installationDir, Constants.Files.BundleExecutableName);
        if (!File.Exists(bundleExecutable))
        {
            return await InstallLauncherAsync(installationDir);
        }

        try
        {
            using var bundleReader = new ExecutableReader(bundleExecutable);
            if (!bundleReader.IsSupported || !bundleReader.IsSingleFile)
            {
                bundleReader.Dispose();
                File.Delete(bundleExecutable);
                return await InstallLauncherAsync(installationDir);
            }
            return await launcherPatcher.PatchLauncherAsync(bundleReader, installationDir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read bundle executable");
            if (File.Exists(bundleExecutable))
            {
                File.Delete(bundleExecutable);
            }
            return await InstallLauncherAsync(installationDir);
        }
    }

    public async Task<bool> IsLauncherInstalledAsync()
    {
        string? installationDir = _configStore.InstallDirectory;
        if (string.IsNullOrEmpty(installationDir))
        {
            return false;
        }
        string versionFile = Path.Combine(installationDir, Constants.Files.LauncherVersionFile);
        if (!File.Exists(versionFile))
        {
            return false;
        }
        string version = await File.ReadAllTextAsync(versionFile);
        return !string.IsNullOrEmpty(version);
    }

    public bool IsLauncherPatched()
    {
        string? installationDir = _configStore.InstallDirectory;
        if (string.IsNullOrEmpty(installationDir))
        {
            return false;
        }
        string mainModuleName = Path.Combine(installationDir, Constants.Files.BundleMainAssemblyName);
        if (!File.Exists(mainModuleName))
        {
            return false;
        }
        return LauncherPatcherService.IsModulePatched(mainModuleName);
    }

    private async Task<bool> CheckForUpdates(VersionInfo info)
    {
        if (info.Files == null || info.Files.Count == 0)
        {
            _logger.LogInformation("No files to update");
            return true;
        }

        if (info.Patches == null || info.Patches.Count == 0)
        {
            _logger.LogError("Not pacthes to apply found");
            return false;
        }

        string installationDir = _configStore.InstallDirectory ?? throw new InvalidOperationException("Install directory not set");

        var filesToPatch = Directory.EnumerateFiles(installationDir, "*", SearchOption.AllDirectories)
            .ToList();

        foreach (var fileToPatch in filesToPatch)
        {
            byte[] md5Hash;
            using (var fs = File.OpenRead(fileToPatch))
            {
                md5Hash = await MD5.HashDataAsync(fs);
            }
            var hashString = BitConverter.ToString(md5Hash).Replace("-", "");

            for (int i = 0; i < info.Files.Count; i++)
            {
                var file = info.Files[i];
                if (!string.Equals(file.Md5Hash, hashString, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var patches = info.Patches
                    .Where(x => x.Files == i + 1)
                    .ToList();
                if (patches.Count == 0)
                {
                    _logger.LogWarning("No patches found for file {file}", file.Path);
                    continue;
                }

                _logger.LogInformation("Applying patches for file {file}", file.Path);
                if (!await ApplyIncrementalPatches(info, patches, fileToPatch))
                {
                    _logger.LogError("Failed to apply patches for file {file}", file.Path);
                    return false;
                }
            }
        }
        return true;
    }

    private async Task<bool> ApplyIncrementalPatches(VersionInfo info, IEnumerable<VersionInfo.IncrementalPatch> patches, string targetFile)
    {
        string swapFile = targetFile + ".tmp";
        foreach (var patch in patches)
        {
            string deltaFile = targetFile + Constants.CarnalCDN.IncrementalPatchFileSuffix;
            using var deltaFs = new FileStream(deltaFile, FileMode.Create);
            if (!await DownloadPatch(info, patch, deltaFs))
            {
                return false;
            }
            deltaFs.Seek(0, SeekOrigin.Begin);

            var deltaApplier = new DeltaApplier
            {
                SkipHashCheck = true
            };
            try
            {
                using (var targetFs = new FileStream(targetFile, FileMode.Open))
                {
                    using var swapFs = new FileStream(swapFile, FileMode.Create);
                    deltaApplier.Apply(targetFs, new BinaryDeltaReader(deltaFs, null), swapFs);
                }
                File.Delete(targetFile);
                File.Move(swapFile, targetFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply patch {patch}", patch);
                return false;
            }
            finally
            {
                deltaFs.Close();
                File.Delete(deltaFile);
            }
        }
        return true;
    }

    private async Task<bool> DownloadPatch(VersionInfo info, VersionInfo.IncrementalPatch patch, Stream outputStream)
    {
        string fileName = $"{patch.FromVersion.Replace('.', '_')}__{patch.ToVersion.Replace('.', '_')}";
        var baseUri = new Uri(info.BaseDownloadURL);
        var uri = new UriBuilder(baseUri)
        {
            Path = Path.Combine(baseUri.AbsolutePath, Constants.CarnalCDN.IncrementaPatchPath, fileName) + Constants.CarnalCDN.IncrementalPatchFileSuffix
        }.Uri;

        using var patchMs = new MemoryStream();
        using (var patchStream = await _httpClient.GetStreamAsync(uri))
        {
            _logger.LogInformation("Downloading patch from {url}", uri);
            await patchStream.CopyToAsync(patchMs);
            patchMs.Seek(0, SeekOrigin.Begin);
        }

        string installationDir = _configStore.InstallDirectory ?? throw new InvalidOperationException("Install directory not set");

        var tempTarFile = Path.Combine(installationDir, Path.GetFileName(uri.LocalPath)) + ".tar";
        Directory.CreateDirectory(Path.GetDirectoryName(tempTarFile)!);
        try
        {
            using var tempTarFs = new FileStream(tempTarFile, FileMode.Create);
            if (patch.CompressionFormat == "LZMA")
            {
                using var archive = SharpCompress.Archives.SevenZip.SevenZipArchive.Open(patchMs);
                using var reader = archive.ExtractAllEntries();
                reader.WriteEntryTo(tempTarFs);
            }
            else if (patch.CompressionFormat == "GZIP")
            {
                using var archive = new GZipStream(patchMs, CompressionMode.Decompress);
                await archive.CopyToAsync(tempTarFs);
            }
            else
            {
                await patchMs.CopyToAsync(tempTarFs);
            }

            tempTarFs.Seek(0, SeekOrigin.Begin);
            var tarfile = new TarReader(tempTarFs, leaveOpen: true);
            var tarEntry = await tarfile.GetNextEntryAsync();
            if (tarEntry == null)
            {
                _logger.LogError("No entries found in tar file");
                return false;
            }
            await tarEntry.DataStream!.CopyToAsync(outputStream);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract patch");
            return false;
        }
        finally
        {
            File.Delete(tempTarFile);
        }
    }

    private async Task<VersionInfo?> DownloadVesionInfo()
    {
        using var versionXmlS = new MemoryStream();
        using (var s = await _httpClient.GetStreamAsync(Constants.CarnalCDN.VersionInfoUrl))
        {
            _logger.LogInformation("Downloading version info from {url}", Constants.CarnalCDN.VersionInfoUrl);
            await s.CopyToAsync(versionXmlS);
            versionXmlS.Seek(0, SeekOrigin.Begin);
        }

        try
        {
            var versionInfo = VersionInfo.DeserializeXml(versionXmlS);
            return versionInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing version info");
            return null;
        }
    }
}