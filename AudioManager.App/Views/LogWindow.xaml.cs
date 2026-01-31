using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AudioManager.App.Views;

/// <summary>
/// Log-Fenster: zeigt die laufende Log-Datei an.
/// Entspricht LogWindow aus dem Python-Original.
/// </summary>
public partial class LogWindow : Window
{
    private readonly string _logFilePath;
    private readonly DispatcherTimer _updateTimer;
    private long _lastFilePosition;
    private string _currentLogLevel = "INFO";

    private static readonly string[] LogLevelOrder = { "DEBUG", "INFO", "WARNING", "ERROR" };

    public LogWindow(string logFilePath = "logfile.log")
    {
        InitializeComponent();
        _logFilePath = logFilePath;

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _updateTimer.Tick += (_, _) => UpdateLog();
        _updateTimer.Start();
    }

    private void UpdateLog()
    {
        if (!File.Exists(_logFilePath)) return;

        try
        {
            using var fs = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            if (fs.Length < _lastFilePosition)
            {
                // Datei wurde zurückgesetzt
                _lastFilePosition = 0;
                LogTextBox.Clear();
            }

            if (fs.Length == _lastFilePosition) return;

            fs.Seek(_lastFilePosition, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            string newContent = reader.ReadToEnd();
            _lastFilePosition = fs.Position;

            // Log-Level filtern
            var lines = newContent.Split('\n');
            foreach (string line in lines)
            {
                if (ShouldShowLine(line))
                {
                    LogTextBox.AppendText(line + "\n");
                }
            }

            // Auto-Scroll
            if (AutoScrollCheck.IsChecked == true)
            {
                LogTextBox.ScrollToEnd();
            }
        }
        catch
        {
            // Dateizugriff kann fehlschlagen — ignorieren
        }
    }

    private bool ShouldShowLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;

        int currentLevelIndex = Array.IndexOf(LogLevelOrder, _currentLogLevel);
        for (int i = currentLevelIndex; i < LogLevelOrder.Length; i++)
        {
            if (line.Contains(LogLevelOrder[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Zeilen ohne erkennbaren Level immer anzeigen
        return !LogLevelOrder.Any(l => line.Contains(l, StringComparison.OrdinalIgnoreCase));
    }

    private void OnLogLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LogLevelCombo.SelectedItem is ComboBoxItem item)
        {
            _currentLogLevel = item.Content?.ToString() ?? "INFO";
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        LogTextBox.Clear();
    }

    protected override void OnClosed(EventArgs e)
    {
        _updateTimer.Stop();
        base.OnClosed(e);
    }
}
