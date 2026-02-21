using System.Windows;
using System.IO;
using System.Runtime.InteropServices;
using Application = System.Windows.Application;
using XTouchVMBridge.App.Services;
using XTouchVMBridge.App.Views;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using XTouchVMBridge.Midi.XTouch;
using XTouchVMBridge.Voicemeeter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

namespace XTouchVMBridge.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string logFilePath = "logfile.log";
        try
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XTouchVMBridge");
            Directory.CreateDirectory(appDataDir);
            logFilePath = Path.Combine(appDataDir, "logfile.log");
        }
        catch
        {
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        Log.Information("Log-Datei: {LogFilePath}", logFilePath);
        Log.Information("App-Version: {Version}; OS: {OS}; 64BitProcess: {Is64BitProcess}",
            typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown",
            RuntimeInformation.OSDescription,
            Environment.Is64BitProcess);

        try
        {
            var configService = new ConfigurationService(
                new LoggerFactory().CreateLogger<ConfigurationService>());
            var config = configService.Load();
            LocalizationService.SetLanguage(config.UiLanguage);
            Log.Information("Config-Version: {ConfigVersion}", config.ConfigVersion);
            Log.Information("UI-Language: {Language}", config.UiLanguage);

            var configuredVmDllDirectory = ResolveConfiguredVmDllDirectory(config.VoicemeeterDllPath);
            var vmDllPath = XTouchVMBridge.Voicemeeter.Native.VoicemeeterRemote.EnsureDllSearchPath(config.VoicemeeterDllPath);
            if (vmDllPath != null)
                Log.Information("Voicemeeter DLL-Pfad gesetzt: {Path}", vmDllPath);
            else
                Log.Warning("Voicemeeter-Installation nicht gefunden. Prüfe ob Voicemeeter installiert ist.");

            if (!string.IsNullOrWhiteSpace(config.VoicemeeterDllPath) && configuredVmDllDirectory == null)
                Log.Warning("Konfigurierter Voicemeeter-DLL-Pfad ist ungültig: {ConfiguredPath}", config.VoicemeeterDllPath);
            else if (!string.IsNullOrWhiteSpace(config.VoicemeeterDllPath) &&
                     !string.IsNullOrWhiteSpace(vmDllPath) &&
                     configuredVmDllDirectory != null &&
                     !string.Equals(configuredVmDllDirectory, vmDllPath, StringComparison.OrdinalIgnoreCase))
                Log.Warning("Konfigurierter Voicemeeter-DLL-Pfad ohne Treffer, Fallback verwendet. Konfiguriert: {ConfiguredPath}; Verwendet: {UsedPath}",
                    config.VoicemeeterDllPath, vmDllPath);

            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(Options.Create(config));
                    services.AddSingleton(config); // Direct registration for TrayIconService/XTouchPanel.
                    services.AddSingleton<IConfigurationService>(configService);

                    services.AddSingleton<IMidiDevice, XTouchDevice>();
                    services.AddSingleton<IVoicemeeterService, VoicemeeterService>();
                    services.AddSingleton<IScreenLockDetector, ScreenLockDetector>();
                    services.AddSingleton<ScreenLockMidiFilter>();

                    services.AddHostedService<AudioDeviceMonitorService>();
                    services.AddSingleton<VoicemeeterBridge>();
                    services.AddHostedService(sp => sp.GetRequiredService<VoicemeeterBridge>());

                    services.AddSingleton<MasterButtonActionService>();

                    services.AddHostedService<SegmentDisplayService>();

                    services.AddSingleton<MqttClientService>();
                    services.AddHostedService(sp => sp.GetRequiredService<MqttClientService>());
                    services.AddSingleton<MqttButtonIntegrationService>();

                    services.AddSingleton<TrayIconService>();
                })
                .Build();

            await _host.StartAsync();

            var vm = _host.Services.GetRequiredService<IVoicemeeterService>();
            vm.Connect();

            var xtouch = _host.Services.GetRequiredService<IMidiDevice>();
            await xtouch.ConnectAsync();
            if (!xtouch.IsConnected)
                Log.Warning("X-Touch beim Start nicht gefunden — Reconnect läuft im Hintergrund.");

            _host.Services.GetRequiredService<ScreenLockMidiFilter>();

            _host.Services.GetRequiredService<MasterButtonActionService>();
            _host.Services.GetRequiredService<MqttButtonIntegrationService>();

            var trayIcon = _host.Services.GetRequiredService<TrayIconService>();
            trayIcon.Initialize();

            Log.Information("XTouchVMBridge gestartet.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fehler beim Starten der Anwendung.");
            System.Windows.MessageBox.Show($"Startfehler: {ex.Message}", "XTouchVMBridge", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("XTouchVMBridge wird beendet.");

        if (_host != null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static string? ResolveConfiguredVmDllDirectory(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return null;

        var fullPath = Path.GetFullPath(configuredPath.Trim().Trim('"'));
        if (Directory.Exists(fullPath))
            return fullPath;

        if (File.Exists(fullPath) &&
            string.Equals(Path.GetFileName(fullPath), "VoicemeeterRemote64.dll", StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(fullPath);

        return null;
    }
}
