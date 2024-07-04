using Microsoft.Extensions.Logging;
using CiLauncher.Models;
using System.Text.Json;

namespace CiLauncher;

internal class LauncherConfigurationStore
{
    private readonly ILogger<LauncherConfigurationStore> _logger;
    private readonly string _configFilePath;

    private Models.LauncherConfig? _config;

    public LauncherConfigurationStore(ILogger<LauncherConfigurationStore> logger)
    {
        _logger = logger;
        _configFilePath = Path.Combine(Utils.GetLocalStorageDirectory(), Constants.Files.ConfigurationFileName);
    }

    public string? InstallDirectory
    {
        get
        {
            return _config?.InstallDirectory ?? (_config = LoadConfig().Result).InstallDirectory;
        }
    }

    public async Task<bool> SetInstallDirectoryAsync(string installDirectory)
    {
        _config ??= new Models.LauncherConfig();
        _config.InstallDirectory = installDirectory;
        return await SaveConfig(_config);
    }

    private async Task<bool> SaveConfig(LauncherConfig config)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var json = JsonSerializer.Serialize(config, SourceGenerationContext.Default.LauncherConfig);
            await File.WriteAllTextAsync(tempFile, json);
            // swap files
            Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath)!);
            using var configFs = new FileStream(_configFilePath, FileMode.Create);
            using var tempFs = new FileStream(tempFile, FileMode.Open);
            tempFs.CopyTo(configFs);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
            return false;
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private async Task<Models.LauncherConfig> LoadConfig()
    {
        if (File.Exists(_configFilePath))
        {
            _logger.LogDebug("Loading configuration from {path}", _configFilePath);
            var json = await File.ReadAllTextAsync(_configFilePath);
            return JsonSerializer.Deserialize(json, SourceGenerationContext.Default.LauncherConfig) ?? new();
        }
        return new();
    }
}
