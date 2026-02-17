using System.Windows;
using System.IO;
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

/// <summary>
/// WPF Application Entry Point.
/// Konfiguriert Dependency Injection, Logging und startet alle Services.
/// Entspricht main() aus audiomanager.pyw.
/// </summary>
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
            // Fallback auf Arbeitsverzeichnis
        }

        // Serilog konfigurieren
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        Log.Information("Log-Datei: {LogFilePath}", logFilePath);

        try
        {
            // Konfiguration früh laden, damit optionale Pfade (z.B. Voicemeeter DLL) bereits verfügbar sind.
            var configService = new ConfigurationService(
                new LoggerFactory().CreateLogger<ConfigurationService>());
            var config = configService.Load();

            // WICHTIG: DLL-Suchpfad für VoicemeeterRemote64.dll setzen BEVOR
            // irgendwelche Services gestartet werden (DllImport wird beim ersten
            // Zugriff auf die Klasse ausgelöst).
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
                    // Konfiguration
                    services.AddSingleton(Options.Create(config));
                    services.AddSingleton(config); // Direkte Registrierung für TrayIconService/XTouchPanel
                    services.AddSingleton<IConfigurationService>(configService);

                    // Core Services
                    services.AddSingleton<IMidiDevice, XTouchDevice>();
                    services.AddSingleton<IVoicemeeterService, VoicemeeterService>();
                    services.AddSingleton<IScreenLockDetector, ScreenLockDetector>();
                    services.AddSingleton<ScreenLockMidiFilter>();

                    // Background Services
                    services.AddHostedService<AudioDeviceMonitorService>();
                    services.AddSingleton<VoicemeeterBridge>();
                    services.AddHostedService(sp => sp.GetRequiredService<VoicemeeterBridge>());

                    // Master-Button-Aktionen
                    services.AddSingleton<MasterButtonActionService>();

                    // Segment-Display (7-Segment Timecode-Anzeige)
                    services.AddHostedService<SegmentDisplayService>();

                    // MQTT Client
                    services.AddSingleton<MqttClientService>();
                    services.AddHostedService(sp => sp.GetRequiredService<MqttClientService>());
                    services.AddSingleton<MqttButtonIntegrationService>();

                    // WPF-spezifisch
                    services.AddSingleton<TrayIconService>();
                })
                .Build();

            // Services starten
            await _host.StartAsync();

            // Voicemeeter verbinden (DLL-Suchpfad wurde oben bereits gesetzt)
            var vm = _host.Services.GetRequiredService<IVoicemeeterService>();
            vm.Connect();

            // X-Touch verbinden (Reconnect läuft automatisch über AudioDeviceMonitorService)
            var xtouch = _host.Services.GetRequiredService<IMidiDevice>();
            await xtouch.ConnectAsync();
            if (!xtouch.IsConnected)
                Log.Warning("X-Touch beim Start nicht gefunden — Reconnect läuft im Hintergrund.");

            // Screen Lock Filter initialisieren (registriert sich selbst auf MIDI-Events)
            _host.Services.GetRequiredService<ScreenLockMidiFilter>();

            // Master-Button-Aktionen initialisieren (registriert sich auf MasterButtonChanged)
            _host.Services.GetRequiredService<MasterButtonActionService>();
            _host.Services.GetRequiredService<MqttButtonIntegrationService>();

            // Tray Icon starten
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
