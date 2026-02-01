using System.Windows;
using Application = System.Windows.Application;
using AudioManager.App.Services;
using AudioManager.App.Views;
using AudioManager.Core.Interfaces;
using AudioManager.Core.Models;
using AudioManager.Midi.Fantom;
using AudioManager.Midi.XTouch;
using AudioManager.Voicemeeter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

namespace AudioManager.App;

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
                    services.AddSingleton<FantomMidiHandler>();
                    services.AddSingleton<ScreenLockMidiFilter>();

                    // Background Services
                    services.AddHostedService<AudioDeviceMonitorService>();
                    services.AddSingleton<VoicemeeterBridge>();
                    services.AddHostedService(sp => sp.GetRequiredService<VoicemeeterBridge>());

                    // WPF-spezifisch
                    services.AddSingleton<TrayIconService>();
                })
                .Build();

            // Services starten
            await _host.StartAsync();

            // Voicemeeter verbinden
            var vm = _host.Services.GetRequiredService<IVoicemeeterService>();
            vm.Connect();

            // X-Touch verbinden (Reconnect läuft automatisch über AudioDeviceMonitorService)
            var xtouch = _host.Services.GetRequiredService<IMidiDevice>();
            await xtouch.ConnectAsync();
            if (!xtouch.IsConnected)
                Log.Warning("X-Touch beim Start nicht gefunden — Reconnect läuft im Hintergrund.");

            // Screen Lock Filter initialisieren (registriert sich selbst auf MIDI-Events)
            _host.Services.GetRequiredService<ScreenLockMidiFilter>();

            // Fantom prüfen
            var fantom = _host.Services.GetRequiredService<FantomMidiHandler>();
            fantom.TryConnect();

            // Tray Icon starten
            var trayIcon = _host.Services.GetRequiredService<TrayIconService>();
            trayIcon.Initialize();

            Log.Information("AudioManager gestartet.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fehler beim Starten der Anwendung.");
            System.Windows.MessageBox.Show($"Startfehler: {ex.Message}", "AudioManager", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("AudioManager wird beendet.");

        if (_host != null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
