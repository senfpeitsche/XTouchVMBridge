using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using XTouchVMBridge.App.Services;
using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using XTouchVMBridge.Voicemeeter.Services;
using Color = System.Windows.Media.Color;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using FontFamily = System.Windows.Media.FontFamily;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace XTouchVMBridge.App.Views;

/// <summary>
/// Dialog zum Bearbeiten der Channel Views.
/// Ermöglicht das Konfigurieren welche VM-Kanäle auf die 8 X-Touch-Strips
/// und den Main Fader gemappt werden.
/// </summary>
public partial class ChannelViewEditorDialog : Window
{
    private readonly XTouchVMBridgeConfig _config;
    private readonly IConfigurationService _configService;
    private readonly VoicemeeterBridge? _bridge;
    private readonly IVoicemeeterService? _vm;

    private bool _suppressEvents;
    private readonly ComboBox[] _channelCombos = new ComboBox[8];
    private readonly ComboBox[] _colorCombos = new ComboBox[8];

    /// <summary>Wird true wenn der User gespeichert hat.</summary>
    public bool WasSaved { get; private set; }

    public ChannelViewEditorDialog(
        XTouchVMBridgeConfig config,
        IConfigurationService configService,
        VoicemeeterBridge? bridge = null,
        IVoicemeeterService? vm = null)
    {
        _config = config;
        _configService = configService;
        _bridge = bridge;
        _vm = vm;

        InitializeComponent();
        Icon = AppIconFactory.CreateWindowIcon();
        BuildChannelGrid();
        PopulateViewList();
    }

    // ─── UI-Aufbau ───────────────────────────────────────────────────

    /// <summary>
    /// Erzeugt 8 Zeilen im ChannelGrid mit Label + ComboBox.
    /// </summary>
    private void BuildChannelGrid()
    {
        ChannelGrid.RowDefinitions.Clear();
        ChannelGrid.Children.Clear();

        for (int i = 0; i < 8; i++)
        {
            ChannelGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = $"Strip {i + 1}:",
                Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 2, 4, 2)
            };
            Grid.SetRow(label, i);
            Grid.SetColumn(label, 0);
            ChannelGrid.Children.Add(label);

            var combo = new ComboBox { Width = 200, Tag = i };
            PopulateChannelCombo(combo, includeNone: false);
            combo.SelectionChanged += OnChannelComboChanged;
            Grid.SetRow(combo, i);
            Grid.SetColumn(combo, 1);
            ChannelGrid.Children.Add(combo);

            var colorCombo = new ComboBox { Width = 100, Tag = i };
            PopulateColorCombo(colorCombo);
            colorCombo.SelectionChanged += OnColorComboChanged;
            Grid.SetRow(colorCombo, i);
            Grid.SetColumn(colorCombo, 2);
            ChannelGrid.Children.Add(colorCombo);

            _channelCombos[i] = combo;
            _colorCombos[i] = colorCombo;
        }

        // Main Fader Combo
        PopulateChannelCombo(MainFaderCombo, includeNone: true);
    }

    /// <summary>
    /// Befüllt eine ComboBox mit allen verfügbaren X-Touch-Display-Farben.
    /// Jeder Eintrag zeigt ein farbiges Rechteck + Farbnamen.
    /// </summary>
    private static void PopulateColorCombo(ComboBox combo)
    {
        combo.Items.Clear();

        // "(Standard)" = globale Farbe verwenden
        combo.Items.Add(new ComboBoxItem { Content = "(Standard)", Tag = (XTouchColor?)null });

        var colors = new (XTouchColor Color, string Name, Color Wpf)[]
        {
            (XTouchColor.Red,     "Rot",     Color.FromRgb(220, 40, 40)),
            (XTouchColor.Green,   "Grün",    Color.FromRgb(40, 200, 40)),
            (XTouchColor.Yellow,  "Gelb",    Color.FromRgb(220, 200, 30)),
            (XTouchColor.Blue,    "Blau",    Color.FromRgb(40, 100, 255)),
            (XTouchColor.Magenta, "Magenta", Color.FromRgb(220, 40, 220)),
            (XTouchColor.Cyan,    "Cyan",    Color.FromRgb(40, 220, 220)),
            (XTouchColor.White,   "Weiß",    Color.FromRgb(200, 200, 200)),
        };

        foreach (var (color, name, wpf) in colors)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new Rectangle
            {
                Width = 12, Height = 12,
                Fill = new SolidColorBrush(wpf),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = name,
                Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });
            combo.Items.Add(new ComboBoxItem { Content = panel, Tag = (XTouchColor?)color });
        }
    }

    /// <summary>
    /// Befüllt eine ComboBox mit allen 16 VM-Kanälen (+ optional "Keine").
    /// Labels werden direkt aus Voicemeeter gelesen, mit Config-Fallback.
    /// </summary>
    private void PopulateChannelCombo(ComboBox combo, bool includeNone)
    {
        combo.Items.Clear();

        if (includeNone)
        {
            combo.Items.Add(new ComboBoxItem { Content = "(Keine)", Tag = -1 });
        }

        for (int ch = 0; ch < 16; ch++)
        {
            string name = GetVmLabel(ch);
            string type = ch < 8 ? "Strip" : "Bus";
            int index = ch < 8 ? ch : ch - 8;

            combo.Items.Add(new ComboBoxItem
            {
                Content = $"{ch}: {name} ({type}[{index}])",
                Tag = ch
            });
        }
    }

    /// <summary>
    /// Liest den Label eines VM-Kanals aus Voicemeeter, mit Config-/Generic-Fallback.
    /// </summary>
    private string GetVmLabel(int vmChannel)
    {
        if (_vm != null)
        {
            try
            {
                string paramName = vmChannel < 8
                    ? $"Strip[{vmChannel}].Label"
                    : $"Bus[{vmChannel - 8}].Label";

                string label = _vm.GetParameterString(paramName);
                if (!string.IsNullOrWhiteSpace(label))
                    return label.Length > 7 ? label[..7] : label;
            }
            catch { /* Fallback */ }
        }

        return _config.Channels.TryGetValue(vmChannel, out var chConfig)
            ? chConfig.Name
            : $"Ch {vmChannel + 1}";
    }

    // ─── View-Liste ──────────────────────────────────────────────────

    private void PopulateViewList()
    {
        _suppressEvents = true;
        ViewList.Items.Clear();

        foreach (var view in _config.ChannelViews)
        {
            ViewList.Items.Add(view.Name);
        }

        if (ViewList.Items.Count > 0)
            ViewList.SelectedIndex = 0;

        _suppressEvents = false;

        if (ViewList.SelectedIndex >= 0)
            ShowViewDetails(ViewList.SelectedIndex);
    }

    private void OnViewSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || ViewList.SelectedIndex < 0) return;
        ShowViewDetails(ViewList.SelectedIndex);
    }

    /// <summary>
    /// Zeigt die Details der ausgewählten View im rechten Panel.
    /// </summary>
    private void ShowViewDetails(int viewIndex)
    {
        if (viewIndex < 0 || viewIndex >= _config.ChannelViews.Count) return;

        _suppressEvents = true;
        var view = _config.ChannelViews[viewIndex];

        ViewNameBox.Text = view.Name;

        // 8 Channel Combos + Farb-Combos setzen
        for (int i = 0; i < 8; i++)
        {
            int vmCh = i < view.Channels.Length ? view.Channels[i] : i;
            SelectComboByTag(_channelCombos[i], vmCh);

            var viewColor = view.GetChannelColor(i);
            SelectColorCombo(_colorCombos[i], viewColor);
        }

        // Main Fader
        SelectComboByTag(MainFaderCombo, view.MainFaderChannel ?? -1);

        DetailPanel.Visibility = Visibility.Visible;
        _suppressEvents = false;
    }

    /// <summary>
    /// Wählt das ComboBox-Item aus, dessen Tag dem angegebenen Wert entspricht.
    /// </summary>
    private static void SelectComboByTag(ComboBox combo, int tag)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Tag is int t && t == tag)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        combo.SelectedIndex = -1;
    }

    // ─── Event Handlers ──────────────────────────────────────────────

    private void OnViewNameChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressEvents || ViewList.SelectedIndex < 0) return;

        var view = _config.ChannelViews[ViewList.SelectedIndex];
        view.Name = ViewNameBox.Text;

        // View-Liste aktualisieren
        _suppressEvents = true;
        int idx = ViewList.SelectedIndex;
        ViewList.Items[idx] = view.Name;
        ViewList.SelectedIndex = idx;
        _suppressEvents = false;
    }

    private void OnChannelComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || ViewList.SelectedIndex < 0) return;
        if (sender is not ComboBox combo || combo.Tag is not int stripIndex) return;
        if (combo.SelectedItem is not ComboBoxItem item || item.Tag is not int vmCh) return;

        var view = _config.ChannelViews[ViewList.SelectedIndex];
        if (stripIndex >= 0 && stripIndex < view.Channels.Length)
        {
            view.Channels[stripIndex] = vmCh;
        }
    }

    private void OnColorComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || ViewList.SelectedIndex < 0) return;
        if (sender is not ComboBox combo || combo.Tag is not int stripIndex) return;
        if (combo.SelectedItem is not ComboBoxItem item) return;

        var view = _config.ChannelViews[ViewList.SelectedIndex];

        // ChannelColors-Array initialisieren falls nötig
        if (view.ChannelColors == null)
            view.ChannelColors = new XTouchColor?[8];

        if (stripIndex >= 0 && stripIndex < view.ChannelColors.Length)
        {
            view.ChannelColors[stripIndex] = item.Tag as XTouchColor?;
        }
    }

    /// <summary>
    /// Wählt die Farbe in einer Farb-ComboBox aus.
    /// </summary>
    private static void SelectColorCombo(ComboBox combo, XTouchColor? color)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item)
            {
                var itemColor = item.Tag as XTouchColor?;
                if (itemColor == color)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }
        combo.SelectedIndex = 0; // "(Standard)"
    }

    private void OnMainFaderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || ViewList.SelectedIndex < 0) return;
        if (MainFaderCombo.SelectedItem is not ComboBoxItem item || item.Tag is not int vmCh) return;

        var view = _config.ChannelViews[ViewList.SelectedIndex];
        view.MainFaderChannel = vmCh >= 0 ? vmCh : null;
    }

    private void OnAddView(object sender, RoutedEventArgs e)
    {
        var newView = new ChannelViewConfig
        {
            Name = "New",
            Channels = new[] { 0, 1, 2, 3, 4, 5, 6, 7 },
            ChannelColors = new XTouchColor?[8],
            MainFaderChannel = null
        };
        _config.ChannelViews.Add(newView);

        _suppressEvents = true;
        ViewList.Items.Add(newView.Name);
        _suppressEvents = false;

        ViewList.SelectedIndex = ViewList.Items.Count - 1;
    }

    private void OnRemoveView(object sender, RoutedEventArgs e)
    {
        int idx = ViewList.SelectedIndex;
        if (idx < 0) return;

        if (_config.ChannelViews.Count <= 1)
        {
            MessageBox.Show("Mindestens eine View muss vorhanden sein.",
                "Channel Views", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _config.ChannelViews.RemoveAt(idx);

        _suppressEvents = true;
        ViewList.Items.RemoveAt(idx);
        _suppressEvents = false;

        ViewList.SelectedIndex = Math.Min(idx, ViewList.Items.Count - 1);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _configService.Save(_config);
        _bridge?.ReloadMappings();
        WasSaved = true;

        MessageBox.Show("Channel Views gespeichert.",
            "Channel Views", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
