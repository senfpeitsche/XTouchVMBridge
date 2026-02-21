using System.Windows;
using Microsoft.Win32;
using Application = System.Windows.Application;
using XTouchVMBridge.App.Views;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using XTouchVMBridge.Voicemeeter.Services;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.Logging;

namespace XTouchVMBridge.App.Services;

/// <summary>
/// Owns the tray icon, context menu, and related utility windows.
/// </summary>
public class TrayIconService : IDisposable
{
    private const string AutoStartRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AutoStartValueName = "XTouchVMBridge";

    private readonly ILogger<TrayIconService> _logger;
    private readonly IVoicemeeterService _vm;
    private readonly IMidiDevice _midiDevice;
    private readonly XTouchVMBridgeConfig _config;
    private readonly IConfigurationService _configService;
    private readonly VoicemeeterBridge _bridge;
    private readonly MasterButtonActionService _masterButtonActionService;
    private readonly MqttClientService _mqttClientService;
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
        MasterButtonActionService masterButtonActionService,
        MqttClientService mqttClientService)
    {
        _logger = logger;
        _vm = vm;
        _midiDevice = midiDevice;
        _config = config;
        _configService = configService;
        _bridge = bridge;
        _masterButtonActionService = masterButtonActionService;
        _mqttClientService = mqttClientService;
    }

    public MidiDebugWindow? ActiveDebugWindow => _midiDebugWindow is { IsVisible: true }
        ? _midiDebugWindow
        : null;

    public void Initialize()
    {
        _trayIcon = new TaskbarIcon
        {
            Icon = AppIconFactory.CreateTrayIcon(),
            ToolTipText = BuildTooltip(),
            ContextMenu = CreateContextMenu()
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowLogWindow();

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
        string status = _midiDevice.IsConnected
            ? LocalizationService.T("Verbunden", "Connected")
            : LocalizationService.T("Getrennt", "Disconnected");
        return $"XTouchVMBridge — X-Touch: {status}";
    }

    private System.Windows.Controls.ContextMenu CreateContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var statusItem = new System.Windows.Controls.MenuItem
        {
            Header = GetConnectionStatusText(),
            IsEnabled = false
        };
        menu.Items.Add(statusItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var showLog = new System.Windows.Controls.MenuItem { Header = LocalizationService.T("Log anzeigen", "Show log") };
        showLog.Click += (_, _) => ShowLogWindow();
        menu.Items.Add(showLog);

        var showMidiDebug = new System.Windows.Controls.MenuItem { Header = LocalizationService.T("MIDI Debug Monitor", "MIDI debug monitor") };
        showMidiDebug.Click += (_, _) => ShowMidiDebugWindow();
        menu.Items.Add(showMidiDebug);

        var showPanel = new System.Windows.Controls.MenuItem { Header = LocalizationService.T("X-Touch Panel", "X-Touch panel") };
        showPanel.Click += (_, _) => ShowXTouchPanel();
        menu.Items.Add(showPanel);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var deviceMenu = new System.Windows.Controls.MenuItem { Header = LocalizationService.T("X-Touch Gerät", "X-Touch device") };
        menu.Items.Add(deviceMenu);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var autoStartItem = new System.Windows.Controls.MenuItem
        {
            Header = LocalizationService.T("Mit Windows starten", "Start with Windows"),
            IsCheckable = true,
            IsChecked = IsAutoStartEnabled()
        };
        autoStartItem.Click += (_, _) =>
        {
            try
            {
                SetAutoStartEnabled(autoStartItem.IsChecked);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Autostart konnte nicht geändert werden.");
                System.Windows.MessageBox.Show(
                    $"Autostart konnte nicht geändert werden: {ex.Message}",
                    "Autostart",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                autoStartItem.IsChecked = IsAutoStartEnabled();
            }
        };
        menu.Items.Add(autoStartItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var languageMenu = new System.Windows.Controls.MenuItem { Header = LocalizationService.T("Sprache", "Language") };
        var languageDe = new System.Windows.Controls.MenuItem
        {
            Header = LocalizationService.T("Deutsch", "German"),
            IsCheckable = true,
            IsChecked = string.Equals(_config.UiLanguage, "de", StringComparison.OrdinalIgnoreCase)
        };
        languageDe.Click += (_, _) => ChangeLanguage("de");
        languageMenu.Items.Add(languageDe);
        var languageEn = new System.Windows.Controls.MenuItem
        {
            Header = LocalizationService.T("Englisch", "English"),
            IsCheckable = true,
            IsChecked = string.Equals(_config.UiLanguage, "en", StringComparison.OrdinalIgnoreCase)
        };
        languageEn.Click += (_, _) => ChangeLanguage("en");
        languageMenu.Items.Add(languageEn);
        menu.Items.Add(languageMenu);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var restartVm = new System.Windows.Controls.MenuItem { Header = LocalizationService.T("Voicemeeter neustarten", "Restart Voicemeeter") };
        restartVm.Click += (_, _) =>
        {
            _vm.Restart();
            _logger.LogInformation("Voicemeeter Neustart über Tray-Menü angefordert.");
        };
        menu.Items.Add(restartVm);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var restart = new System.Windows.Controls.MenuItem { Header = LocalizationService.T("XTouchVMBridge neustarten", "Restart XTouchVMBridge") };
        restart.Click += (_, _) => RestartApplication();
        menu.Items.Add(restart);

        var exit = new System.Windows.Controls.MenuItem { Header = LocalizationService.T("Beenden", "Exit") };
        exit.Click += (_, _) =>
        {
            _logger.LogInformation("Beenden über Tray-Menü.");
            Application.Current.Shutdown();
        };
        menu.Items.Add(exit);

        menu.Opened += (_, _) =>
        {
            statusItem.Header = GetConnectionStatusText();
            RefreshDeviceMenu(deviceMenu);
            autoStartItem.IsChecked = IsAutoStartEnabled();
            languageDe.IsChecked = string.Equals(_config.UiLanguage, "de", StringComparison.OrdinalIgnoreCase);
            languageEn.IsChecked = string.Equals(_config.UiLanguage, "en", StringComparison.OrdinalIgnoreCase);
        };

        return menu;
    }

    private string GetConnectionStatusText()
    {
        return _midiDevice.IsConnected
            ? LocalizationService.T("X-Touch: Verbunden", "X-Touch: Connected")
            : LocalizationService.T("X-Touch: Getrennt", "X-Touch: Disconnected");
    }

    private void RefreshDeviceMenu(System.Windows.Controls.MenuItem deviceMenu)
    {
        deviceMenu.Items.Clear();

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

        var devices = _midiDevice.ListDevices();
        if (devices.Count == 0)
        {
            var noDevice = new System.Windows.Controls.MenuItem
            {
                Header = LocalizationService.T("Kein X-Touch gefunden", "No X-Touch found"),
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
                var name = deviceName; // Capture local copy for closure.
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

            _xtouchPanelWindow = new XTouchPanelWindow(
                _midiDevice,
                _config,
                _configService,
                _bridge,
                _vm,
                _masterButtonActionService,
                _mqttClientService);
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

    private void ChangeLanguage(string language)
    {
        var normalized = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "de";
        if (string.Equals(_config.UiLanguage, normalized, StringComparison.OrdinalIgnoreCase))
            return;

        _config.UiLanguage = normalized;
        _configService.Save(_config);
        LocalizationService.SetLanguage(normalized);

        System.Windows.MessageBox.Show(
            LocalizationService.T("Sprache wird nach Neustart aktiv.", "Language will be applied after restart."),
            LocalizationService.T("Sprache", "Language"),
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);

        RestartApplication();
    }

    private static bool IsAutoStartEnabled()
    {
        string? configured = GetAutoStartValue();
        if (string.IsNullOrWhiteSpace(configured))
            return false;

        string? expected = BuildAutoStartCommand();
        if (string.IsNullOrWhiteSpace(expected))
            return false;

        return string.Equals(configured.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }

    private void SetAutoStartEnabled(bool enabled)
    {
        string? command = BuildAutoStartCommand();
        if (string.IsNullOrWhiteSpace(command))
            throw new InvalidOperationException("Konnte den Anwendungspfad nicht bestimmen.");

        using var key = Registry.CurrentUser.CreateSubKey(AutoStartRegistryPath, writable: true)
            ?? throw new InvalidOperationException("Run-Registry-Key konnte nicht geöffnet werden.");

        if (enabled)
        {
            key.SetValue(AutoStartValueName, command, RegistryValueKind.String);
            _logger.LogInformation("Autostart aktiviert.");
        }
        else
        {
            key.DeleteValue(AutoStartValueName, throwOnMissingValue: false);
            _logger.LogInformation("Autostart deaktiviert.");
        }
    }

    private static string? GetAutoStartValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AutoStartRegistryPath, writable: false);
        return key?.GetValue(AutoStartValueName) as string;
    }

    private static string? BuildAutoStartCommand()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
            return null;

        return $"\"{exePath}\"";
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


