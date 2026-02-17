using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using XTouchVMBridge.App.Services;

namespace XTouchVMBridge.App.Views;

/// <summary>
/// Log-Fenster: zeigt die laufende Log-Datei an.
/// </summary>
public partial class LogWindow : Window
{
    private string _logFilePath = "";
    private DispatcherTimer? _updateTimer;
    private long _lastFilePosition;
    private string _currentLogLevel = "INFO";
    private bool _isUpdating;
    private bool _initialLoadDone;
    private List<string> _allLoadedLines = new();

    // Maximale Anzahl Zeilen beim ersten Laden (letzte X Zeilen)
    private const int MaxInitialLines = 500;

    private static readonly string[] LogLevelOrder = { "DBG", "INF", "WRN", "ERR" };

    public LogWindow(string? logFilePath = null)
    {
        InitializeComponent();
        Icon = AppIconFactory.CreateWindowIcon();

        // Log-Datei in %APPDATA%\XTouchVMBridge suchen
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XTouchVMBridge");
        var today = DateTime.Now.ToString("yyyyMMdd");
        var dateFile = Path.Combine(appDataDir, $"logfile{today}.log");
        var defaultFile = Path.Combine(appDataDir, "logfile.log");
        var configuredFile = string.IsNullOrWhiteSpace(logFilePath)
            ? defaultFile
            : Path.GetFullPath(logFilePath);

        if (File.Exists(dateFile))
            _logFilePath = dateFile;
        else if (File.Exists(configuredFile))
            _logFilePath = configuredFile;
        else
            _logFilePath = configuredFile;

        Loaded += OnWindowLoaded;
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_logFilePath) || !File.Exists(_logFilePath))
        {
            LogTextBox.Text = $"Keine Log-Datei gefunden.\nErwartet unter: {_logFilePath}\n";
            return;
        }

        LogTextBox.Text = $"Lade Log-Datei: {_logFilePath}...\n";

        // Initiales Laden asynchron
        await Task.Run(() => LoadInitialContent());

        _initialLoadDone = true;

        // Timer für Updates starten
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += OnTimerTick;
        _updateTimer.Start();
    }

    private void LoadInitialContent()
    {
        try
        {
            if (!File.Exists(_logFilePath)) return;

            // Nur die letzten X Zeilen laden für schnelles Starten
            var lines = ReadLastLines(_logFilePath, MaxInitialLines);
            var fileInfo = new FileInfo(_logFilePath);
            _lastFilePosition = fileInfo.Length;

            Dispatcher.Invoke(() =>
            {
                _allLoadedLines = lines;
                RefreshDisplay();
            });
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.Text = $"Fehler beim Laden: {ex.Message}\n";
            });
        }
    }

    private static List<string> ReadLastLines(string filePath, int lineCount)
    {
        var lines = new List<string>();

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);

        // Für kleine Dateien: alles lesen
        if (fs.Length < 100_000) // < 100 KB
        {
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line != null) lines.Add(line);
            }

            // Nur letzte X Zeilen behalten
            if (lines.Count > lineCount)
            {
                lines = lines.Skip(lines.Count - lineCount).ToList();
            }
            return lines;
        }

        // Für große Dateien: vom Ende lesen
        var buffer = new char[64 * 1024]; // 64 KB Buffer
        long position = Math.Max(0, fs.Length - buffer.Length);

        fs.Seek(position, SeekOrigin.Begin);
        int charsRead = reader.ReadBlock(buffer, 0, buffer.Length);

        var content = new string(buffer, 0, charsRead);
        var allLines = content.Split('\n');

        // Erste Zeile könnte unvollständig sein, überspringen
        for (int i = 1; i < allLines.Length && lines.Count < lineCount; i++)
        {
            var line = allLines[allLines.Length - 1 - i].TrimEnd('\r');
            if (!string.IsNullOrEmpty(line))
            {
                lines.Insert(0, line);
            }
        }

        return lines;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_isUpdating || !_initialLoadDone) return;
        _isUpdating = true;

        try
        {
            UpdateLog();
        }
        catch
        {
            // Fehler ignorieren
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void UpdateLog()
    {
        if (string.IsNullOrEmpty(_logFilePath) || !File.Exists(_logFilePath))
            return;

        using var fs = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        if (fs.Length < _lastFilePosition)
        {
            // Datei wurde rotiert/gelöscht
            _lastFilePosition = 0;
            LogTextBox.Clear();
            LogTextBox.Text = $"=== Log-Datei wurde zurückgesetzt ===\n";
        }

        if (fs.Length == _lastFilePosition)
            return;

        fs.Seek(_lastFilePosition, SeekOrigin.Begin);
        using var reader = new StreamReader(fs);
        string newContent = reader.ReadToEnd();
        _lastFilePosition = fs.Position;

        var lines = newContent.Split('\n');
        foreach (string line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                _allLoadedLines.Add(line);

                if (ShouldShowLine(line))
                {
                    LogTextBox.AppendText(line + "\n");
                }
            }
        }

        // Maximale Anzahl Zeilen begrenzen
        if (_allLoadedLines.Count > MaxInitialLines * 2)
        {
            _allLoadedLines = _allLoadedLines.Skip(_allLoadedLines.Count - MaxInitialLines).ToList();
        }

        if (AutoScrollCheck?.IsChecked == true)
        {
            LogTextBox.ScrollToEnd();
        }
    }

    private void RefreshDisplay()
    {
        if (string.IsNullOrEmpty(_logFilePath) || !File.Exists(_logFilePath))
        {
            LogTextBox.Text = $"Keine Log-Datei gefunden.\nErwartet unter: {_logFilePath}\n";
            return;
        }

        LogTextBox.Clear();

        var fileInfo = new FileInfo(_logFilePath);
        LogTextBox.AppendText($"=== Log-Datei: {_logFilePath} ({fileInfo.Length / 1024} KB) ===\n");
        LogTextBox.AppendText($"=== Filter: {_currentLogLevel}+ | {_allLoadedLines.Count} Zeilen geladen ===\n\n");

        foreach (var line in _allLoadedLines)
        {
            if (ShouldShowLine(line))
            {
                LogTextBox.AppendText(line + "\n");
            }
        }

        if (AutoScrollCheck?.IsChecked == true)
        {
            LogTextBox.ScrollToEnd();
        }
    }

    private bool ShouldShowLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        int currentLevelIndex = _currentLogLevel switch
        {
            "DEBUG" => 0,
            "INFO" => 1,
            "WARNING" => 2,
            "ERROR" => 3,
            _ => 1
        };

        for (int i = currentLevelIndex; i < LogLevelOrder.Length; i++)
        {
            if (line.Contains($"[{LogLevelOrder[i]}]", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        bool hasAnyLevel = LogLevelOrder.Any(l => line.Contains($"[{l}]", StringComparison.OrdinalIgnoreCase));
        return !hasAnyLevel;
    }

    private async void OnLogLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LogLevelCombo?.SelectedItem is ComboBoxItem item)
        {
            _currentLogLevel = item.Content?.ToString() ?? "INFO";

            // Log neu laden mit neuem Filter
            if (_initialLoadDone)
            {
                LogTextBox.Text = "Lade Log mit Filter...\n";
                _allLoadedLines.Clear();
                await Task.Run(() => LoadInitialContent());
            }
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        LogTextBox.Clear();
        if (!string.IsNullOrWhiteSpace(_logFilePath) && File.Exists(_logFilePath))
            _lastFilePosition = new FileInfo(_logFilePath).Length;
        else
            LogTextBox.Text = $"Keine Log-Datei gefunden.\nErwartet unter: {_logFilePath}\n";
    }

    private void OnOpenInExplorerClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(_logFilePath) && File.Exists(_logFilePath))
            {
                Process.Start("explorer.exe", $"/select,\"{Path.GetFullPath(_logFilePath)}\"");
            }
            else
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "XTouchVMBridge");
                Directory.CreateDirectory(logDir);
                Process.Start("explorer.exe", logDir);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Fehler beim Öffnen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _updateTimer?.Stop();
        _updateTimer = null;
        base.OnClosed(e);
    }
}
