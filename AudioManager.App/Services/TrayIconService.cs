using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using Application = System.Windows.Application;
using AudioManager.App.Views;
using AudioManager.Core.Interfaces;
using AudioManager.Core.Models;
using AudioManager.Voicemeeter.Services;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.Logging;

namespace AudioManager.App.Services;

/// <summary>
/// System-Tray-Icon mit Kontextmenü.
/// Entspricht TrayIcon-Klasse aus dem Python-Original.
/// </summary>
public class TrayIconService : IDisposable
{
    private readonly ILogger<TrayIconService> _logger;
    private readonly IVoicemeeterService _vm;
    private readonly IMidiDevice _midiDevice;
    private readonly AudioManagerConfig _config;
    private readonly IConfigurationService _configService;
    private readonly VoicemeeterBridge _bridge;
    private TaskbarIcon? _trayIcon;
    private LogWindow? _logWindow;
    private MidiDebugWindow? _midiDebugWindow;
    private XTouchPanelWindow? _xtouchPanelWindow;
    private bool _disposed;

    public TrayIconService(
        ILogger<TrayIconService> logger,
        IVoicemeeterService vm,
        IMidiDevice midiDevice,
        AudioManagerConfig config,
        IConfigurationService configService,
        VoicemeeterBridge bridge)
    {
        _logger = logger;
        _vm = vm;
        _midiDevice = midiDevice;
        _config = config;
        _configService = configService;
        _bridge = bridge;
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
            ToolTipText = "AudioManager",
            ContextMenu = CreateContextMenu()
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowLogWindow();
        _logger.LogInformation("Tray-Icon initialisiert.");
    }

    private System.Windows.Controls.ContextMenu CreateContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

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

        var restartVm = new System.Windows.Controls.MenuItem { Header = "Voicemeeter neustarten" };
        restartVm.Click += (_, _) =>
        {
            _vm.Restart();
            _logger.LogInformation("Voicemeeter Neustart über Tray-Menü angefordert.");
        };
        menu.Items.Add(restartVm);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var restart = new System.Windows.Controls.MenuItem { Header = "AudioManager neustarten" };
        restart.Click += (_, _) => RestartApplication();
        menu.Items.Add(restart);

        var exit = new System.Windows.Controls.MenuItem { Header = "Beenden" };
        exit.Click += (_, _) =>
        {
            _logger.LogInformation("Beenden über Tray-Menü.");
            Application.Current.Shutdown();
        };
        menu.Items.Add(exit);

        return menu;
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

    private void ShowXTouchPanel()
    {
        if (_xtouchPanelWindow != null && _xtouchPanelWindow.IsVisible)
        {
            _xtouchPanelWindow.Activate();
            return;
        }

        _xtouchPanelWindow = new XTouchPanelWindow(_midiDevice, _config, _configService, _bridge);
        _xtouchPanelWindow.Closed += (_, _) => _xtouchPanelWindow = null;
        _xtouchPanelWindow.Show();

        _logger.LogInformation("X-Touch Panel geöffnet.");
    }

    private void RestartApplication()
    {
        _logger.LogInformation("AudioManager Neustart angefordert.");
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
        var size = g.MeasureString("AM", font);
        float x = (64 - size.Width) / 2;
        float y = (64 - size.Height) / 2;
        g.DrawString("AM", font, brush, x, y);

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
