using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CsLauncher;
using Serilog;
using Serilog.Exceptions;
using System.Diagnostics;

string logPath = Path.Combine(Utils.GetLocalStorageDirectory(), Constants.Files.LogFile);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .Enrich.WithExceptionDetails()
    .WriteTo.Console(
        outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(logPath, 
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Verbose, 
        retainedFileCountLimit: 1, 
        rollOnFileSizeLimit: true,
        fileSizeLimitBytes: 0x4000 // 16 KB
    )
    .CreateLogger();

var serviceCollection = new ServiceCollection();

serviceCollection
    .AddLogging(config => config
        .AddSerilog()
        .SetMinimumLevel(LogLevel.Debug));

serviceCollection.AddHttpClient<LauncherInstallerService>();

var serviceProvider = serviceCollection
    .AddSingleton<LauncherPatcherService>()
    .AddSingleton<LauncherInstallerService>()
    .AddSingleton<LauncherConfigurationStore>()
    .BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

string installDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    Path.GetFileNameWithoutExtension(Constants.Files.BundleExecutableName));

var installer = serviceProvider.GetRequiredService<LauncherInstallerService>();

if (await installer.IsLauncherInstalledAsync())
{
    if (!installer.IsLauncherPatched())
    {
        await installer.ReinstallLauncherAsync();
    }
    StartLauncher();
    return;
}
if (await installer.InstallLauncherAsync(installDirectory))
{
    StartLauncher();
    return;
}

ShowError();

void StartLauncher()
{
    logger.LogInformation("Starting launcher");
    var configurationStore = serviceProvider.GetRequiredService<LauncherConfigurationStore>();
    string? installationDir = configurationStore.InstallDirectory;
    if (installationDir == null)
    {
        logger.LogError("Installation directory not found");
        ShowError();
        return;
    }

    string executablePath = Path.Combine(installationDir, Constants.Files.LauncherLoader);
    if (!File.Exists(executablePath))
    {
        logger.LogError("Executable not found: {executablePath}", executablePath);
        ShowError();
        return;
    }

    var startupInfo = new ProcessStartInfo(executablePath)
    {
        WorkingDirectory = installationDir,
    };

    var process = Process.Start(startupInfo);
    if (process == null)
    {
        logger.LogError("Failed to start process: {executablePath}", executablePath);
        ShowError();
        return;
    }
}

void ShowError()
{
    var prevColor = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(
@"


   _____                      _   _     _                                   _                                     
  / ____|                    | | | |   (_)                                 | |                                    
 | (___   ___  _ __ ___   ___| |_| |__  _ _ __   __ _   __      _____ _ __ | |_   __      ___ __ ___  _ __   __ _ 
  \___ \ / _ \| '_ ` _ \ / _ \ __| '_ \| | '_ \ / _` |  \ \ /\ / / _ \ '_ \| __|  \ \ /\ / / '__/ _ \| '_ \ / _` |
  ____) | (_) | | | | | |  __/ |_| | | | | | | | (_| |   \ V  V /  __/ | | | |_    \ V  V /| | | (_) | | | | (_| |
 |_____/ \___/|_| |_| |_|\___|\__|_| |_|_|_| |_|\__, |    \_/\_/ \___|_| |_|\__|    \_/\_/ |_|  \___/|_| |_|\__, |
                                                 __/ |                                                      __/ |
                                                |___/                                                      |___/ 
");
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("Sorry for the inconvenience, but the launcher could not be started.");
    Console.WriteLine("If you believe this is an error, please open an issue at:");
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine("https://github.com/OpenYiffGames/CarnalInstinctLauncher/issues");
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("If possible, please attach the log file located at:");
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine(logPath);
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("Press any key to exit...");
    Console.ForegroundColor = prevColor;
    Console.ReadKey();
}

