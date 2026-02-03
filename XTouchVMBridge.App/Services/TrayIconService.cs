using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using Application = System.Windows.Application;
using XTouchVMBridge.App.Views;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using XTouchVMBridge.Voicemeeter.Services;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.Logging;

namespace XTouchVMBridge.App.Services;

/// <summary>
/// System-Tray-Icon mit Kontextmenü.
/// Entspricht TrayIcon-Klasse aus dem Python-Original.
/// </summary>
public class TrayIconService : IDisposable
{
    private readonly ILogger<TrayIconService> _logger;
    private readonly IVoicemeeterService _vm;
    private readonly IMidiDevice _midiDevice;
    private readonly XTouchVMBridgeConfig _config;
    private readonly IConfigurationService _configService;
    private readonly VoicemeeterBridge _bridge;
    private readonly MasterButtonActionService _masterButtonActionService;
    private TaskbarIcon? _trayIcon;
    private LogWindow? _logWindow;
    private MidiDebugWindow? _midiDebugWindow;
    private XTouchPanelWindow? _xtouchPanelWindow;
    private bool _disposed;

    public TrayIconService(
        ILogger<TrayIconService> logger,
        IVoicemeeterService vm,
        IMidiDevice midiDevice,
        XTouchVMBridgeConfig config,
        IConfigurationService configService,
        VoicemeeterBridge bridge,
        MasterButtonActionService masterButtonActionService)
    {
        _logger = logger;
        _vm = vm;
        _midiDevice = midiDevice;
        _config = config;
        _configService = configService;
        _bridge = bridge;
        _masterButtonActionService = masterButtonActionService;
    }

    /// <summary>
    /// Zugriff auf das aktive MIDI-Debug-Fenster (falls geöffnet).
    /// Wird vom XTouchDevice genutzt um ausgehende Nachrichten zu loggen.
    /// </summary>
    public MidiDebugWindow? ActiveDebugWindow => _midiDebugWindow is { IsVisible: true }
        ? _midiDebugWindow
        : null;

    public void Initialize()
    {
        _trayIcon = new TaskbarIcon
        {
            Icon = CreateIcon(),
            ToolTipText = BuildTooltip(),
            ContextMenu = CreateContextMenu()
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowLogWindow();

        // Tooltip automatisch aktualisieren wenn sich der Verbindungsstatus ändert
        _midiDevice.ConnectionStateChanged += (_, connected) =>
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (_trayIcon != null)
                    _trayIcon.ToolTipText = BuildTooltip();
            });
        };

        _logger.LogInformation("Tray-Icon initialisiert.");
    }

    private string BuildTooltip()
    {
        string status = _midiDevice.IsConnected ? "Verbunden" : "Getrennt";
        return $"XTouchVMBridge — X-Touch: {status}";
    }

    private System.Windows.Controls.ContextMenu CreateContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        // Dynamischer Verbindungsstatus (wird bei jedem Öffnen aktualisiert)
        var statusItem = new System.Windows.Controls.MenuItem
        {
            Header = GetConnectionStatusText(),
            IsEnabled = false
        };
        menu.Items.Add(statusItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var showLog = new System.Windows.Controls.MenuItem { Header = "Log anzeigen" };
        showLog.Click += (_, _) => ShowLogWindow();
        menu.Items.Add(showLog);

        var showMidiDebug = new System.Windows.Controls.MenuItem { Header = "MIDI Debug Monitor" };
        showMidiDebug.Click += (_, _) => ShowMidiDebugWindow();
        menu.Items.Add(showMidiDebug);

        var showPanel = new System.Windows.Controls.MenuItem { Header = "X-Touch Panel" };
        showPanel.Click += (_, _) => ShowXTouchPanel();
        menu.Items.Add(showPanel);

        menu.Items.Add(new System.Windows.Controls.Separator());

        // X-Touch Geräteauswahl Untermenü
        var deviceMenu = new System.Windows.Controls.MenuItem { Header = "X-Touch Gerät" };
        menu.Items.Add(deviceMenu);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var restartVm = new System.Windows.Controls.MenuItem { Header = "Voicemeeter neustarten" };
        restartVm.Click += (_, _) =>
        {
            _vm.Restart();
            _logger.LogInformation("Voicemeeter Neustart über Tray-Menü angefordert.");
        };
        menu.Items.Add(restartVm);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var restart = new System.Windows.Controls.MenuItem { Header = "XTouchVMBridge neustarten" };
        restart.Click += (_, _) => RestartApplication();
        menu.Items.Add(restart);

        var exit = new System.Windows.Controls.MenuItem { Header = "Beenden" };
        exit.Click += (_, _) =>
        {
            _logger.LogInformation("Beenden über Tray-Menü.");
            Application.Current.Shutdown();
        };
        menu.Items.Add(exit);

        // Beim Öffnen des Menüs: Status und Geräteliste aktualisieren
        menu.Opened += (_, _) =>
        {
            statusItem.Header = GetConnectionStatusText();
            RefreshDeviceMenu(deviceMenu);
        };

        return menu;
    }

    private string GetConnectionStatusText()
    {
        return _midiDevice.IsConnected ? "X-Touch: Verbunden" : "X-Touch: Getrennt";
    }

    private void RefreshDeviceMenu(System.Windows.Controls.MenuItem deviceMenu)
    {
        deviceMenu.Items.Clear();

        // "Auto" Option
        var autoItem = new System.Windows.Controls.MenuItem
        {
            Header = "Auto",
            IsCheckable = true,
            IsChecked = _midiDevice.SelectedDeviceName == null
        };
        autoItem.Click += (_, _) =>
        {
            _midiDevice.SelectedDeviceName = null;
            _logger.LogInformation("X-Touch Gerät: Auto ausgewählt.");
        };
        deviceMenu.Items.Add(autoItem);

        // Verfügbare Geräte auflisten
        var devices = _midiDevice.ListDevices();
        if (devices.Count == 0)
        {
            var noDevice = new System.Windows.Controls.MenuItem
            {
                Header = "Kein X-Touch gefunden",
                IsEnabled = false
            };
            deviceMenu.Items.Add(noDevice);
        }
        else
        {
            foreach (var deviceName in devices)
            {
                var devItem = new System.Windows.Controls.MenuItem
                {
                    Header = deviceName,
                    IsCheckable = true,
                    IsChecked = _midiDevice.SelectedDeviceName == deviceName
                };
                var name = deviceName; // Capture für Closure
                devItem.Click += (_, _) =>
                {
                    _midiDevice.SelectedDeviceName = name;
                    _logger.LogInformation("X-Touch Gerät ausgewählt: {Device}", name);
                };
                deviceMenu.Items.Add(devItem);
            }
        }
    }

    private void ShowLogWindow()
    {
        if (_logWindow != null && _logWindow.IsVisible)
        {
            _logWindow.Activate();
            return;
        }

        _logWindow = new LogWindow();
        _logWindow.Show();
    }

    private void ShowMidiDebugWindow()
    {
        try
        {
            if (_midiDebugWindow != null && _midiDebugWindow.IsVisible)
            {
                _midiDebugWindow.Activate();
                return;
            }

            _midiDebugWindow = new MidiDebugWindow(_midiDevice);
            _midiDebugWindow.Closed += (_, _) => _midiDebugWindow = null;
            _midiDebugWindow.Show();

            _logger.LogInformation("MIDI Debug Monitor geöffnet.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Öffnen des MIDI Debug Monitors.");
            System.Windows.MessageBox.Show($"Fehler: {ex.Message}", "MIDI Debug Monitor",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void ShowXTouchPanel()
    {
        try
        {
            if (_xtouchPanelWindow != null && _xtouchPanelWindow.IsVisible)
            {
                _xtouchPanelWindow.Activate();
                return;
            }

            _xtouchPanelWindow = new XTouchPanelWindow(_midiDevice, _config, _configService, _bridge, _vm, _masterButtonActionService);
            _xtouchPanelWindow.Closed += (_, _) => _xtouchPanelWindow = null;
            _xtouchPanelWindow.Show();

            _logger.LogInformation("X-Touch Panel geöffnet.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Öffnen des X-Touch Panels.");
            System.Windows.MessageBox.Show($"Fehler: {ex.Message}", "X-Touch Panel",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void RestartApplication()
    {
        _logger.LogInformation("XTouchVMBridge Neustart angefordert.");
        var exePath = Environment.ProcessPath;
        if (exePath != null)
        {
            System.Diagnostics.Process.Start(exePath);
        }
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Erzeugt ein 64×64 Icon mit "AM" Text.
    /// Entspricht create_image() aus dem Python-Original.
    /// </summary>
    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(64, 64);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(30, 30, 30));

        using var font = new Font("Segoe UI", 22, System.Drawing.FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(0, 180, 255));
        var size = g.MeasureString("XV", font);
        float x = (64 - size.Width) / 2;
        float y = (64 - size.Height) / 2;
        g.DrawString("XV", font, brush, x, y);

        IntPtr hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _xtouchPanelWindow?.Close();
        _midiDebugWindow?.Close();
        _logWindow?.Close();
        _trayIcon?.Dispose();

        GC.SuppressFinalize(this);
    }
}
