using System.Windows;
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

        // Serilog konfigurieren
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("logfile.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            // WICHTIG: DLL-Suchpfad für VoicemeeterRemote64.dll setzen BEVOR
            // irgendwelche Services gestartet werden (DllImport wird beim ersten
            // Zugriff auf die Klasse ausgelöst).
            var vmDllPath = XTouchVMBridge.Voicemeeter.Native.VoicemeeterRemote.EnsureDllSearchPath();
            if (vmDllPath != null)
                Log.Information("Voicemeeter DLL-Pfad gesetzt: {Path}", vmDllPath);
            else
                Log.Warning("Voicemeeter-Installation nicht gefunden. Prüfe ob Voicemeeter installiert ist.");

            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    // Konfiguration
                    var configService = new ConfigurationService(
                        new LoggerFactory().CreateLogger<ConfigurationService>());
                    var config = configService.Load();
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
}
