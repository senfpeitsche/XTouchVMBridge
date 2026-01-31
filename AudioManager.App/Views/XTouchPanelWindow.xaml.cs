using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AudioManager.Core.Enums;
using AudioManager.Core.Hardware;
using AudioManager.Core.Interfaces;
using AudioManager.Core.Models;
using AudioManager.Voicemeeter.Services;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using ProgressBar = System.Windows.Controls.ProgressBar;

namespace AudioManager.App.Views;

/// <summary>
/// Interaktive Darstellung der vollständigen X-Touch Oberfläche.
/// Links: 8 Kanalstreifen + Main-Fader, Rechts: Master Section
/// (Encoder Assign, Display, Global View, Function, Modify/Automation/Utility,
///  Transport, Fader Bank/Channel, Jog Wheel).
/// Klick auf jedes Control zeigt MIDI-Details und zugeordnete Funktion im Detail-Panel.
/// </summary>
public partial class XTouchPanelWindow : Window
{
    private readonly IMidiDevice? _device;
    private readonly AudioManagerConfig? _config;
    private readonly IConfigurationService? _configService;
    private readonly VoicemeeterBridge? _bridge;
    private readonly IVoicemeeterService? _vm;
    private readonly DispatcherTimer _refreshTimer;

    // ─── UI-Referenzen: 8 Kanalstreifen ──────────────────────────────
    private readonly Border[] _displayPanels = new Border[8];
    private readonly TextBlock[] _displayTop = new TextBlock[8];
    private readonly TextBlock[] _displayBottom = new TextBlock[8];
    private readonly Button[] _encoderButtons = new Button[8];
    private readonly ProgressBar[] _encoderRings = new ProgressBar[8];
    private readonly Button[] _recButtons = new Button[8];
    private readonly Button[] _soloButtons = new Button[8];
    private readonly Button[] _muteButtons = new Button[8];
    private readonly Button[] _selectButtons = new Button[8];
    private readonly Slider[] _faderSliders = new Slider[8];
    private readonly TextBlock[] _faderDbLabels = new TextBlock[8];
    private readonly ProgressBar[] _levelMeters = new ProgressBar[8];
    private readonly Ellipse[] _touchIndicators = new Ellipse[8];

    // ─── UI-Referenzen: Main Fader ───────────────────────────────────
    private Slider? _mainFaderSlider;
    private TextBlock? _mainFaderDbLabel;

    // ─── UI-Referenzen: Master Section Buttons ───────────────────────
    private readonly Dictionary<string, Button> _masterButtons = new();

    // ─── Mapping-Editor State ────────────────────────────────────────
    private int _selectedVmChannel = -1;
    private string _selectedControlType = ""; // "Button", "Fader", "Encoder"
    private XTouchButtonType _selectedButtonType;
    private bool _suppressMappingEvents;

    public XTouchPanelWindow() : this(null, null, null, null, null) { }

    public XTouchPanelWindow(IMidiDevice? device, AudioManagerConfig? config,
        IConfigurationService? configService = null, VoicemeeterBridge? bridge = null,
        IVoicemeeterService? vm = null)
    {
        InitializeComponent();
        _device = device;
        _config = config;
        _configService = configService;
        _bridge = bridge;
        _vm = vm;

        BuildChannelStrips();
        BuildMainFader();
        BuildMasterSection();
        SubscribeToEvents();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _refreshTimer.Tick += (_, _) => RefreshAll();
        _refreshTimer.Start();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  UI aufbauen: 8 Kanalstreifen
    // ═══════════════════════════════════════════════════════════════════

    private void BuildChannelStrips()
    {
        for (int ch = 0; ch < 8; ch++)
        {
            int channel = ch;
            var strip = BuildSingleChannelStrip(channel);
            ChannelGrid.Children.Add(strip);
        }
    }

    private Border BuildSingleChannelStrip(int ch)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        // Kanal-Nummer
        stack.Children.Add(new TextBlock
        {
            Text = $"CH {ch + 1}",
            Foreground = Brushes.Gray,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        });

        // LCD Display
        var displayBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(20, 20, 20)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(4, 2, 4, 2),
            Margin = new Thickness(0, 0, 0, 4),
            Cursor = System.Windows.Input.Cursors.Hand,
            Width = 72
        };
        var displayStack = new StackPanel();
        var topText = new TextBlock
        {
            Text = "       ",
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var bottomText = new TextBlock
        {
            Text = "       ",
            Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        displayStack.Children.Add(topText);
        displayStack.Children.Add(bottomText);
        displayBorder.Child = displayStack;
        displayBorder.MouseLeftButtonDown += (_, _) => ShowDisplayDetail(ch);
        stack.Children.Add(displayBorder);
        _displayPanels[ch] = displayBorder;
        _displayTop[ch] = topText;
        _displayBottom[ch] = bottomText;

        // Encoder Ring
        var encoderRing = new ProgressBar
        {
            Width = 48, Height = 6,
            Minimum = 0, Maximum = 15, Value = 0,
            Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            Foreground = new SolidColorBrush(Color.FromRgb(255, 180, 0)),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 2, 0, 0)
        };
        stack.Children.Add(encoderRing);
        _encoderRings[ch] = encoderRing;

        // Encoder Button (Knob)
        var encoderBtn = new Button
        {
            Width = 44, Height = 44,
            Margin = new Thickness(0, 2, 0, 4),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = $"Encoder {ch + 1}"
        };
        encoderBtn.Template = CreateEncoderTemplate();
        encoderBtn.Click += (_, _) => ShowEncoderDetail(ch);
        stack.Children.Add(encoderBtn);
        _encoderButtons[ch] = encoderBtn;

        // Buttons: REC, SOLO, MUTE, SELECT
        _recButtons[ch] = CreateHwButton("REC", Color.FromRgb(180, 40, 40), Color.FromRgb(60, 15, 15), ch, XTouchButtonType.Rec);
        _soloButtons[ch] = CreateHwButton("SOLO", Color.FromRgb(200, 180, 30), Color.FromRgb(60, 55, 10), ch, XTouchButtonType.Solo);
        _muteButtons[ch] = CreateHwButton("MUTE", Color.FromRgb(220, 100, 20), Color.FromRgb(65, 30, 8), ch, XTouchButtonType.Mute);
        _selectButtons[ch] = CreateHwButton("SEL", Color.FromRgb(40, 140, 220), Color.FromRgb(12, 42, 65), ch, XTouchButtonType.Select);
        stack.Children.Add(_recButtons[ch]);
        stack.Children.Add(_soloButtons[ch]);
        stack.Children.Add(_muteButtons[ch]);
        stack.Children.Add(_selectButtons[ch]);

        // Touch-Indikator
        var touchDot = new Ellipse
        {
            Width = 8, Height = 8,
            Fill = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            ToolTip = "Fader Touch"
        };
        stack.Children.Add(touchDot);
        _touchIndicators[ch] = touchDot;

        // Fader + Level-Meter nebeneinander
        var faderPanel = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        faderPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        faderPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Level Meter (links, schmal)
        var meter = new ProgressBar
        {
            Orientation = Orientation.Vertical,
            Width = 8, Height = 150,
            Minimum = 0, Maximum = 13, Value = 0,
            Background = new SolidColorBrush(Color.FromRgb(25, 25, 25)),
            Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 80)),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 4, 0)
        };
        meter.MouseLeftButtonDown += (_, _) => ShowLevelMeterDetail(ch);
        Grid.SetColumn(meter, 0);
        faderPanel.Children.Add(meter);
        _levelMeters[ch] = meter;

        // Fader (rechts)
        var faderContainer = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var fader = new Slider
        {
            Orientation = Orientation.Vertical,
            Height = 150, Width = 32,
            Minimum = -8192, Maximum = 8188, Value = 0,
            IsEnabled = false,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        fader.MouseLeftButtonDown += (_, _) => ShowFaderDetail(ch);
        faderContainer.Children.Add(fader);

        var dbLabel = new TextBlock
        {
            Text = "-∞ dB",
            Foreground = new SolidColorBrush(Color.FromRgb(150, 210, 150)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        faderContainer.Children.Add(dbLabel);
        Grid.SetColumn(faderContainer, 1);
        faderPanel.Children.Add(faderContainer);
        _faderSliders[ch] = fader;
        _faderDbLabels[ch] = dbLabel;

        stack.Children.Add(faderPanel);

        // Channel-Nummer unten
        stack.Children.Add(new TextBlock
        {
            Text = $"{ch + 1}",
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 14, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        });

        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
            Margin = new Thickness(3),
            Padding = new Thickness(6),
            Child = stack
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Main Fader (Kanal 9 / Fader rechts neben den 8 Strips)
    // ═══════════════════════════════════════════════════════════════════

    private void BuildMainFader()
    {
        var stack = MainFaderPanel;

        stack.Children.Add(new TextBlock
        {
            Text = "MAIN",
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

        _mainFaderSlider = new Slider
        {
            Orientation = Orientation.Vertical,
            Height = 200, Width = 32,
            Minimum = -8192, Maximum = 8188, Value = 0,
            IsEnabled = false,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        _mainFaderSlider.MouseLeftButtonDown += (_, _) => ShowMainFaderDetail();
        stack.Children.Add(_mainFaderSlider);

        _mainFaderDbLabel = new TextBlock
        {
            Text = "-∞ dB",
            Foreground = new SolidColorBrush(Color.FromRgb(150, 210, 150)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };
        stack.Children.Add(_mainFaderDbLabel);

        stack.Children.Add(new TextBlock
        {
            Text = "MAIN",
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Master Section: Alle Panels
    // ═══════════════════════════════════════════════════════════════════

    private void BuildMasterSection()
    {
        BuildEncoderAssignButtons();
        BuildDisplayModeButtons();
        BuildGlobalViewButtons();
        BuildFunctionButtons();
        BuildModifyButtons();
        BuildAutomationButtons();
        BuildUtilityButtons();
        BuildTransportButtons();
        BuildFaderBankChannelButtons();
        SetupJogWheel();
    }

    // ── Encoder Assign: TRACK, PAN/SURROUND, EQ, SEND, PLUG-IN, INST
    private void BuildEncoderAssignButtons()
    {
        var names = new[] { "TRACK", "PAN", "EQ", "SEND", "PLUG-IN", "INST" };
        var notes = new[] { 40, 42, 44, 41, 43, 45 }; // Mackie Control Note-Nummern
        for (int i = 0; i < names.Length; i++)
        {
            int noteNum = notes[i];
            var btn = CreateMasterButton(names[i], $"EncoderAssign_{names[i]}", Color.FromRgb(35, 35, 35), Color.FromRgb(80, 80, 80));
            btn.Click += (_, _) => ShowMasterButtonDetail("Encoder Assign", names[Array.IndexOf(notes, noteNum)], noteNum,
                "Wählt den Parameter für alle 8 Encoder.\nDrehen der Encoder ändert den gewählten Parameter pro Kanal.");
            EncoderAssignPanel.Children.Add(btn);
        }
    }

    // ── Display Mode: NAME/VALUE, SMPTE/BEATS
    private void BuildDisplayModeButtons()
    {
        var items = new (string Name, int Note, string Desc)[]
        {
            ("NAME", 52, "Zeigt Kanalnamen im Display"),
            ("VALUE", 53, "Zeigt Parameterwert im Display"),
            ("SMPTE", 113, "Timecode: SMPTE-Format (HH:MM:SS:FF)"),
            ("BEATS", 114, "Timecode: Bars/Beats-Format")
        };
        foreach (var (name, note, desc) in items)
        {
            var btn = CreateMasterButton(name, $"Display_{name}", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
            btn.Click += (_, _) => ShowMasterButtonDetail("Display Mode", name, note, desc);
            DisplayModePanel.Children.Add(btn);
        }
    }

    // ── Global View: MIDI TRACKS, INPUTS, AUDIO TRACKS, AUDIO INST, AUX, BUSES, OUTPUTS, USER
    private void BuildGlobalViewButtons()
    {
        var names = new[] { "MIDI TR", "INPUTS", "AUDIO", "A.INST", "AUX", "BUSES", "OUTPUTS", "USER" };
        var notes = new[] { 62, 63, 64, 65, 66, 67, 68, 69 };
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var btn = CreateMasterButton(names[i], $"GlobalView_{names[i]}", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
            btn.Click += (_, _) => ShowMasterButtonDetail("Global View", names[idx], notes[idx],
                $"Globale Ansicht: {names[idx]}.\nFiltert die Kanalstreifen nach Typ.");
            GlobalViewPanel.Children.Add(btn);
        }
    }

    // ── Function: F1..F8
    private void BuildFunctionButtons()
    {
        for (int i = 0; i < 8; i++)
        {
            int idx = i;
            string label = $"F{i + 1}";
            int noteNum = 54 + i; // F1=54..F8=61
            var btn = CreateMasterButton(label, $"Function_{label}", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
            btn.Click += (_, _) => ShowMasterButtonDetail("Function", label, noteNum,
                $"Funktion {idx + 1} — benutzerdefinierbar.\nZuweisung hängt von der DAW/Anwendung ab.");
            FunctionPanel.Children.Add(btn);
        }
    }

    // ── Modify: SHIFT, OPTION, CONTROL, ALT
    private void BuildModifyButtons()
    {
        var items = new (string Name, int Note)[] { ("SHIFT", 70), ("OPTION", 71), ("CONTROL", 72), ("ALT", 73) };
        foreach (var (name, note) in items)
        {
            var btn = CreateMasterButton(name, $"Modify_{name}", Color.FromRgb(30, 30, 45), Color.FromRgb(60, 60, 100));
            btn.Click += (_, _) => ShowMasterButtonDetail("Modify", name, note,
                $"Modifier-Taste '{name}'.\nKombiniert mit anderen Tasten für erweiterte Funktionen.");
            ModifyPanel.Children.Add(btn);
        }
    }

    // ── Automation: READ, WRITE, TRIM, TOUCH, LATCH, GROUP
    private void BuildAutomationButtons()
    {
        var items = new (string Name, int Note)[]
        {
            ("READ", 74), ("WRITE", 75), ("TRIM", 76),
            ("TOUCH", 77), ("LATCH", 78), ("GROUP", 79)
        };
        foreach (var (name, note) in items)
        {
            var btn = CreateMasterButton(name, $"Auto_{name}", Color.FromRgb(30, 40, 30), Color.FromRgb(60, 90, 60));
            btn.Click += (_, _) => ShowMasterButtonDetail("Automation", name, note,
                $"Automation-Modus '{name}'.\nSteuert den Automation-Modus der DAW.");
            AutomationPanel.Children.Add(btn);
        }
    }

    // ── Utility: SAVE, UNDO, CANCEL, ENTER
    private void BuildUtilityButtons()
    {
        var items = new (string Name, int Note)[] { ("SAVE", 80), ("UNDO", 81), ("CANCEL", 82), ("ENTER", 83) };
        foreach (var (name, note) in items)
        {
            var btn = CreateMasterButton(name, $"Util_{name}", Color.FromRgb(40, 35, 25), Color.FromRgb(90, 80, 50));
            btn.Click += (_, _) => ShowMasterButtonDetail("Utility", name, note,
                $"Utility-Taste '{name}'.\nWird von der DAW zugewiesen.");
            UtilityPanel.Children.Add(btn);
        }
    }

    // ── Transport: obere Reihe (MARKER, NUDGE, CYCLE, DROP, REPLACE, CLICK, SOLO) + Transport Buttons
    private void BuildTransportButtons()
    {
        // Obere Reihe
        var topItems = new (string Name, int Note)[]
        {
            ("MARKER", 84), ("NUDGE", 85), ("CYCLE", 86), ("DROP", 87),
            ("REPLACE", 88), ("CLICK", 89), ("SOLO", 90)
        };
        foreach (var (name, note) in topItems)
        {
            var btn = CreateMasterButton(name, $"Transport_{name}", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
            btn.Click += (_, _) => ShowMasterButtonDetail("Transport", name, note,
                $"Transport-Taste '{name}'.\nSteuert die DAW-Transport-Funktionen.");
            TransportTopPanel.Children.Add(btn);
        }

        // Transport-Buttons: ◄◄  ►►  ■  ▶  ●
        var transportItems = new (string Symbol, string Name, int Note, Color ActiveColor)[]
        {
            ("◄◄", "Rewind", 91, Color.FromRgb(80, 80, 80)),
            ("►►", "Forward", 92, Color.FromRgb(80, 80, 80)),
            ("■",  "Stop", 93, Color.FromRgb(120, 120, 120)),
            ("▶",  "Play", 94, Color.FromRgb(30, 180, 30)),
            ("●",  "Record", 95, Color.FromRgb(220, 30, 30))
        };
        foreach (var (symbol, name, note, activeColor) in transportItems)
        {
            var btn = new Button
            {
                Content = symbol,
                Width = 40, Height = 32,
                Margin = new Thickness(2),
                FontSize = 14,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.Template = CreateRoundedButtonTemplate(4);
            btn.Click += (_, _) => ShowMasterButtonDetail("Transport", name, note,
                $"Transport: {name} ({symbol})\nMIDI: Note On #{note}");
            _masterButtons[$"Transport_{name}"] = btn;
            TransportButtonPanel.Children.Add(btn);
        }
    }

    // ── Fader Bank / Channel Navigation
    private void BuildFaderBankChannelButtons()
    {
        // Fader Bank: ◄ ► → Channel View wechseln
        var fbLeft = CreateMasterButton("◄", "FaderBank_Left", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
        fbLeft.FontSize = 14; fbLeft.Width = 36;
        fbLeft.Click += (_, _) =>
        {
            _bridge?.SwitchView(-1);
        };
        FaderBankPanel.Children.Add(fbLeft);

        var fbRight = CreateMasterButton("►", "FaderBank_Right", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
        fbRight.FontSize = 14; fbRight.Width = 36;
        fbRight.Click += (_, _) =>
        {
            _bridge?.SwitchView(+1);
        };
        FaderBankPanel.Children.Add(fbRight);

        // Channel: ◄ ►
        var chLeft = CreateMasterButton("◄", "Channel_Left", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
        chLeft.FontSize = 14; chLeft.Width = 36;
        chLeft.Click += (_, _) => ShowMasterButtonDetail("Channel", "Channel Left", 48,
            "Verschiebt die 8 Kanalstreifen um 1 Kanal nach links.");
        ChannelNavPanel.Children.Add(chLeft);

        var chRight = CreateMasterButton("►", "Channel_Right", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
        chRight.FontSize = 14; chRight.Width = 36;
        chRight.Click += (_, _) => ShowMasterButtonDetail("Channel", "Channel Right", 49,
            "Verschiebt die 8 Kanalstreifen um 1 Kanal nach rechts.");
        ChannelNavPanel.Children.Add(chRight);
    }

    // ── Jog Wheel
    private void SetupJogWheel()
    {
        JogWheelButton.Click += (_, _) =>
        {
            MappingPanel.Visibility = Visibility.Collapsed;
            DetailHeader.Text = "Jog Wheel";
            DetailText.Text =
                "Funktion:       Scrub / Shuttle / Navigation\n" +
                "                Dreht durch Timeline-Positionen, Marker etc.\n\n" +
                "MIDI:           CC 88\n" +
                "                CW (rechts):  value = 1\n" +
                "                CCW (links):  value = 65\n" +
                "                Schnelles Drehen: höhere Werte (1..15 / 65..79)\n\n" +
                "Scrub-Button:   Note On #101 (toggle)\n\n" +
                "Hersteller-Doku: Jog Wheel CC 88 (CW: 65, CCW: 1)";
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Hilfsfunktionen: Button-Erzeugung
    // ═══════════════════════════════════════════════════════════════════

    private Button CreateHwButton(string label, Color activeColor, Color inactiveColor, int ch, XTouchButtonType type)
    {
        var btn = new Button
        {
            Content = label,
            Width = 56, Height = 24,
            Margin = new Thickness(0, 2, 0, 2),
            Background = new SolidColorBrush(inactiveColor),
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            BorderThickness = new Thickness(1),
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10, FontWeight = FontWeights.Bold,
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = new ButtonTag(ch, type, activeColor, inactiveColor)
        };
        btn.Template = CreateRoundedButtonTemplate(3);
        btn.Click += (_, _) => ShowButtonDetail(ch, type);
        return btn;
    }

    private record ButtonTag(int Channel, XTouchButtonType Type, Color ActiveColor, Color InactiveColor);

    private Button CreateMasterButton(string label, string key, Color bgColor, Color borderColor)
    {
        var btn = new Button
        {
            Content = label,
            Height = 22, MinWidth = 42,
            Margin = new Thickness(1),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 8, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
            Background = new SolidColorBrush(bgColor),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        btn.Template = CreateRoundedButtonTemplate(2);
        _masterButtons[key] = btn;
        return btn;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Control Templates
    // ═══════════════════════════════════════════════════════════════════

    private static ControlTemplate CreateRoundedButtonTemplate(double cornerRadius)
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
        { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush")
        { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness")
        { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
        border.SetValue(Border.PaddingProperty, new Thickness(2));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(cp);
        template.VisualTree = border;
        return template;
    }

    private static ControlTemplate CreateEncoderTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var grid = new FrameworkElementFactory(typeof(Grid));
        var outerEllipse = new FrameworkElementFactory(typeof(Ellipse));
        outerEllipse.SetValue(Ellipse.FillProperty, new SolidColorBrush(Color.FromRgb(34, 34, 34)));
        outerEllipse.SetValue(Ellipse.StrokeProperty, new SolidColorBrush(Color.FromRgb(85, 85, 85)));
        outerEllipse.SetValue(Ellipse.StrokeThicknessProperty, 2.0);
        grid.AppendChild(outerEllipse);
        var innerEllipse = new FrameworkElementFactory(typeof(Ellipse));
        innerEllipse.SetValue(Ellipse.FillProperty, new SolidColorBrush(Color.FromRgb(51, 51, 51)));
        innerEllipse.SetValue(Ellipse.WidthProperty, 20.0);
        innerEllipse.SetValue(Ellipse.HeightProperty, 20.0);
        grid.AppendChild(innerEllipse);
        template.VisualTree = grid;
        return template;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Events abonnieren
    // ═══════════════════════════════════════════════════════════════════

    private void SubscribeToEvents()
    {
        if (_device == null) return;

        _device.FaderChanged += (_, e) => Dispatcher.BeginInvoke(() => OnFaderUpdate(e.Channel));
        _device.ButtonChanged += (_, e) => Dispatcher.BeginInvoke(() => OnButtonUpdate(e.Channel, e.ButtonType, e.IsPressed));
        _device.EncoderRotated += (_, e) => Dispatcher.BeginInvoke(() => OnEncoderUpdate(e.Channel));
        _device.EncoderPressed += (_, e) => Dispatcher.BeginInvoke(() => OnEncoderPressUpdate(e.Channel, e.IsPressed));
        _device.FaderTouched += (_, e) => Dispatcher.BeginInvoke(() => OnFaderTouchUpdate(e.Channel, e.IsTouched));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Echtzeit-Updates (100ms Timer)
    // ═══════════════════════════════════════════════════════════════════

    private void RefreshAll()
    {
        if (_device == null) return;

        for (int ch = 0; ch < 8 && ch < _device.Channels.Count; ch++)
        {
            var xtCh = _device.Channels[ch];

            // Display
            _displayTop[ch].Text = xtCh.Display.TopRow;
            _displayBottom[ch].Text = xtCh.Display.BottomRow;
            _displayPanels[ch].Background = new SolidColorBrush(XTouchColorToWpf(xtCh.Display.Color, dim: true));
            _displayTop[ch].Foreground = new SolidColorBrush(XTouchColorToWpf(xtCh.Display.Color, dim: false));

            // Encoder Ring + Funktionsname auf Knob
            _encoderRings[ch].Value = xtCh.Encoder.RingPosition;
            if (xtCh.Encoder.HasFunctions && xtCh.Encoder.ActiveFunction != null)
            {
                _encoderButtons[ch].Content = new TextBlock
                {
                    Text = xtCh.Encoder.ActiveFunction.Name,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 180, 0)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 7, FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            // Fader
            _faderSliders[ch].Value = xtCh.Fader.Position;
            double db = FaderControl.PositionToDb(xtCh.Fader.Position);
            _faderDbLabels[ch].Text = db <= -65 ? "-∞ dB" : $"{db:F1} dB";

            // Touch-Indikator
            _touchIndicators[ch].Fill = new SolidColorBrush(
                xtCh.Fader.IsTouched ? Color.FromRgb(0, 200, 255) : Color.FromRgb(40, 40, 40));

            // Level Meter
            _levelMeters[ch].Value = xtCh.LevelMeter.Level;
            _levelMeters[ch].Foreground = new SolidColorBrush(
                xtCh.LevelMeter.Level > 10 ? Color.FromRgb(255, 60, 30)
                : xtCh.LevelMeter.Level > 7 ? Color.FromRgb(255, 200, 0)
                : Color.FromRgb(0, 200, 80));

            // Buttons LED-State
            UpdateButtonVisual(_recButtons[ch], xtCh.GetButton(XTouchButtonType.Rec).LedState);
            UpdateButtonVisual(_soloButtons[ch], xtCh.GetButton(XTouchButtonType.Solo).LedState);
            UpdateButtonVisual(_muteButtons[ch], xtCh.GetButton(XTouchButtonType.Mute).LedState);
            UpdateButtonVisual(_selectButtons[ch], xtCh.GetButton(XTouchButtonType.Select).LedState);
        }

        // View-Button Text synchronisieren
        if (_bridge != null)
        {
            ViewSwitchButton.Content = $"⚙ {_bridge.CurrentViewName}";
        }
    }

    private void UpdateButtonVisual(Button btn, LedState state)
    {
        if (btn.Tag is not ButtonTag tag) return;
        var color = state switch
        {
            LedState.On => tag.ActiveColor,
            LedState.Blink => tag.ActiveColor,
            _ => tag.InactiveColor
        };
        btn.Background = new SolidColorBrush(color);
    }

    private void OnFaderUpdate(int ch)
    {
        if (ch < 0 || ch >= 8 || _device == null) return;
        var xtCh = _device.Channels[ch];
        _faderSliders[ch].Value = xtCh.Fader.Position;
        double db = FaderControl.PositionToDb(xtCh.Fader.Position);
        _faderDbLabels[ch].Text = db <= -65 ? "-∞ dB" : $"{db:F1} dB";
    }

    private void OnButtonUpdate(int ch, XTouchButtonType type, bool pressed) { /* RefreshAll aktualisiert visuell */ }
    private void OnEncoderUpdate(int ch)
    {
        if (ch >= 0 && ch < 8 && _device != null)
            _encoderRings[ch].Value = _device.Channels[ch].Encoder.RingPosition;
    }
    private void OnEncoderPressUpdate(int ch, bool pressed) { }
    private void OnFaderTouchUpdate(int ch, bool touched)
    {
        if (ch >= 0 && ch < 8)
            _touchIndicators[ch].Fill = new SolidColorBrush(
                touched ? Color.FromRgb(0, 200, 255) : Color.FromRgb(40, 40, 40));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Klick → Detail-Panel: Kanalstreifen-Controls
    // ═══════════════════════════════════════════════════════════════════

    private void ShowDisplayDetail(int ch)
    {
        MappingPanel.Visibility = Visibility.Collapsed;
        var xtCh = _device?.Channels[ch];
        string chName = GetConfigName(ch);
        var color = xtCh?.Display.Color ?? XTouchColor.Off;

        DetailHeader.Text = $"LCD Display — Kanal {ch + 1}";
        DetailText.Text =
            $"Obere Zeile:   \"{xtCh?.Display.TopRow.TrimEnd()}\"\n" +
            $"Untere Zeile:  \"{xtCh?.Display.BottomRow.TrimEnd()}\"\n" +
            $"Farbe:         {color}\n" +
            $"Config-Name:   {chName}\n\n" +
            $"Funktion:      Zeigt Kanalnamen (oben) und aktuelle Ansicht/dB-Wert (unten).\n" +
            $"MIDI:          SysEx F0 00 00 66 15 12 <offset> <ASCII> F7\n" +
            $"               SysEx F0 00 00 66 15 72 <8 Farbbytes> F7\n" +
            $"Hersteller:    LCDs — SysEx F0 00 20 32 dd 4C nn cc c1..c14 F7";
    }

    private void ShowEncoderDetail(int ch)
    {
        var enc = _device?.Channels[ch].Encoder;

        // Funktionsliste aufbauen
        string functionInfo;
        if (enc != null && enc.HasFunctions)
        {
            var lines = new List<string>();
            for (int i = 0; i < enc.Functions.Count; i++)
            {
                var fn = enc.Functions[i];
                string marker = i == enc.ActiveFunctionIndex ? " ►" : "  ";
                lines.Add($"{marker} {fn.Name,-7} = {fn.FormatValue(),-10} ({fn.MinValue}..{fn.MaxValue} {fn.Unit}, Step {fn.StepSize})");
            }
            functionInfo =
                $"Aktive Funktion: {enc.ActiveFunction?.Name} = {enc.ActiveFunction?.FormatValue()}\n" +
                $"Drücken:         Schaltet zur nächsten Funktion (zyklisch)\n" +
                $"Drehen:          Ändert Wert der aktiven Funktion\n\n" +
                $"Funktionsliste:\n{string.Join("\n", lines)}";
        }
        else
        {
            functionInfo = "Keine Funktionsliste zugewiesen.\nSteuert den im Encoder Assign gewählten Parameter.";
        }

        DetailHeader.Text = $"Encoder — Kanal {ch + 1}";
        DetailText.Text =
            $"Ring-Position:  {enc?.RingPosition}/15\n" +
            $"Ring-Modus:     {enc?.RingMode}\n" +
            $"Center-LED:     {(enc?.RingLed == true ? "An" : "Aus")}\n" +
            $"Gedrückt:       {(enc?.IsPressed == true ? "Ja" : "Nein")}\n\n" +
            $"{functionInfo}\n\n" +
            $"MIDI Drehen:    CC {80 + ch} (relativ: inc=65, dec=1)\n" +
            $"MIDI Drücken:   Note On #{32 + ch} (vel 127=press, vel 0=release)\n" +
            $"MIDI Ring:      CC {48 + ch} (value = mode×16 + pos [+64 LED])\n" +
            $"Hersteller:     Encoder CC 80..87, Encoder Rings CC 80..87";

        // Mapping-Panel anzeigen
        ShowEncoderMappingPanel(ch);
    }

    private void ShowButtonDetail(int ch, XTouchButtonType type)
    {
        var btn = _device?.Channels[ch].GetButton(type);
        int noteNum = btn?.NoteNumber ?? ((int)type * 8 + ch);

        string function = type switch
        {
            XTouchButtonType.Mute => $"Toggle Mute auf VM-Kanal ({(btn?.LedState == LedState.On ? "MUTED" : "unmuted")})",
            XTouchButtonType.Solo => "Toggle Solo auf VM-Kanal (nur Input-Strips)",
            XTouchButtonType.Rec => "Rec-Arm / benutzerdefiniert",
            XTouchButtonType.Select => "Kanal auswählen",
            _ => "Nicht zugewiesen"
        };

        string ledDesc = btn?.LedState switch
        {
            LedState.On => "An (vel 65..127)",
            LedState.Blink => "Blinken (vel 64)",
            _ => "Aus (vel 0..63)"
        };

        DetailHeader.Text = $"{type} Button — Kanal {ch + 1}";
        DetailText.Text =
            $"LED-Status:     {ledDesc}\n" +
            $"Gedrückt:       {(btn?.IsPressed == true ? "Ja" : "Nein")}\n\n" +
            $"Funktion:       {function}\n\n" +
            $"MIDI Input:     Note On #{noteNum} (push: vel 127, release: vel 0)\n" +
            $"MIDI LED:       Note On #{noteNum} (vel 0..63=off, 64=flash, 65..127=on)\n" +
            $"Note-Formel:    {type}={((int)type)} × 8 + Kanal={ch} = {noteNum}\n" +
            $"Hersteller:     Buttons Note On #0..103";

        // Mapping-Panel anzeigen
        ShowButtonMappingPanel(ch, type);
    }

    private void ShowFaderDetail(int ch)
    {
        var fader = _device?.Channels[ch].Fader;
        int pos = fader?.Position ?? 0;
        double db = FaderControl.PositionToDb(pos);
        string chName = GetConfigName(ch);

        DetailHeader.Text = $"Fader — Kanal {ch + 1} ({chName})";
        DetailText.Text =
            $"Position:       {pos} (Range: -8192 bis +8188)\n" +
            $"dB-Wert:        {(db <= -65 ? "-∞" : $"{db:F1}")} dB (Range: -70 bis +8)\n" +
            $"Berührt:        {(fader?.IsTouched == true ? "Ja" : "Nein")}\n\n" +
            $"Funktion:       Setzt Gain auf VM-Kanal. Werte unter -60 dB → -inf.\n" +
            $"                Bei Touch: dB-Wert im Display anzeigen.\n" +
            $"                Bei Release: Ansichtsname wiederherstellen (nach 2s).\n\n" +
            $"MIDI Position:  Pitchwheel Ch {ch} (14-bit: LSB + MSB = {pos + 8192} unsigned)\n" +
            $"MIDI Touch:     Note On #{110 + ch} (touch: vel 127, release: vel 0)\n" +
            $"MIDI CC Mode:   CC {70 + ch} (value 0..127) — nur im MIDI-Mode\n" +
            $"Hersteller:     Fader CC 70..77, Fader Touch Note #110..117";

        // Mapping-Panel anzeigen
        ShowFaderMappingPanel(ch);
    }

    private void ShowLevelMeterDetail(int ch)
    {
        MappingPanel.Visibility = Visibility.Collapsed;
        var meter = _device?.Channels[ch].LevelMeter;
        int level = meter?.Level ?? 0;

        DetailHeader.Text = $"Level Meter — Kanal {ch + 1}";
        DetailText.Text =
            $"Aktueller Level: {level}/13\n" +
            $"Stufen:          0=Stille, 1..8=Normal, 9..11=Laut, 12..13=Clip\n\n" +
            $"Funktion:        Zeigt den Post-Fader-Pegel des gemappten VM-Kanals.\n" +
            $"                 Wird alle 100ms per Polling aktualisiert.\n\n" +
            $"dB-Skala:        -200→0, -100→1, -50→2, -40→3, -35→4, -30→5,\n" +
            $"                 -25→6, -20→7, -15→8, -10→9, -5→10, 0→11, +5→12\n\n" +
            $"MIDI:            Channel Aftertouch: (Kanal<<4 | Level)\n" +
            $"                 Byte = ({ch}<<4 | {level}) = {(ch << 4) | level}\n" +
            $"Hersteller:      Meter LEDs CC 90..97 (value 0..127)";
    }

    private void ShowMainFaderDetail()
    {
        MappingPanel.Visibility = Visibility.Collapsed;
        DetailHeader.Text = "Main Fader";
        DetailText.Text =
            "Funktion:       Master-Fader. Steuert den Master-Bus-Pegel.\n" +
            "                In Mackie Control: Kanal 9 (Index 8)\n\n" +
            "MIDI Position:  Pitchwheel Ch 8 (14-bit signed)\n" +
            "MIDI Touch:     Note On #118 (touch: vel 127, release: vel 0)\n" +
            "MIDI CC Mode:   CC 78 (value 0..127) — nur im MIDI-Mode\n\n" +
            "Hersteller:     Fader CC 70..78, Fader Touch Note #110..118";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Klick → Detail-Panel: Master Section Buttons
    // ═══════════════════════════════════════════════════════════════════

    private void ShowMasterButtonDetail(string section, string name, int noteNumber, string description)
    {
        MappingPanel.Visibility = Visibility.Collapsed;
        DetailHeader.Text = $"{section} — {name}";
        DetailText.Text =
            $"{description}\n\n" +
            $"MIDI:           Note On #{noteNumber}\n" +
            $"                Push: vel 127, Release: vel 0\n" +
            $"LED-Feedback:   Note On #{noteNumber} (vel 0=off, 64=blink, 127=on)\n\n" +
            $"Mackie Control: Standardisierte Zuordnung im MCU-Protokoll.";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Mapping-Editor: Panel anzeigen und befüllen
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ermittelt den VM-Kanal-Index für einen X-Touch-Kanal.
    /// Verwendet die aktuelle Display-Anzeige (Config-Name) zur Zuordnung,
    /// da die Panel-Ansicht die gleichen Kanäle wie die Bridge zeigt.
    /// </summary>
    private int ResolveVmChannel(int xtChannel)
    {
        // Versuche aus dem Display-Namen den Config-Kanal zu ermitteln
        var displayName = _device?.Channels[xtChannel].Display.TopRow.TrimEnd();
        if (displayName != null && _config != null)
        {
            foreach (var (vmCh, chConfig) in _config.Channels)
            {
                if (chConfig.Name == displayName)
                    return vmCh;
            }
        }

        // Fallback: X-Touch-Kanal = VM-Kanal
        return xtChannel;
    }

    /// <summary>Blendet alle Mapping-Sub-Panels aus.</summary>
    private void HideMappingSubPanels()
    {
        ButtonMappingPanel.Visibility = Visibility.Collapsed;
        FaderMappingPanel.Visibility = Visibility.Collapsed;
        EncoderMappingPanel.Visibility = Visibility.Collapsed;
    }

    /// <summary>Zeigt das Button-Mapping-Panel für einen Kanal und Button-Typ.</summary>
    private void ShowButtonMappingPanel(int xtChannel, XTouchButtonType buttonType)
    {
        if (_config == null || _configService == null)
        {
            MappingPanel.Visibility = Visibility.Collapsed;
            return;
        }

        int vmCh = ResolveVmChannel(xtChannel);
        _selectedVmChannel = vmCh;
        _selectedControlType = "Button";
        _selectedButtonType = buttonType;

        _suppressMappingEvents = true;
        try
        {
            HideMappingSubPanels();

            // ComboBox mit Bool-Parametern befüllen
            var boolParams = VoicemeeterParameterCatalog.GetBoolParameters(vmCh);
            ButtonParamCombo.Items.Clear();
            ButtonParamCombo.Items.Add(new ComboBoxItem { Content = "(nicht zugewiesen)", Tag = "" });

            foreach (var p in boolParams)
            {
                ButtonParamCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{p.DisplayName}  [{p.Parameter}]",
                    Tag = p.Parameter
                });
            }

            // Aktuellen Wert auswählen
            string? currentParam = null;
            if (_config.Mappings.TryGetValue(vmCh, out var mapping))
            {
                string btnKey = buttonType.ToString();
                if (mapping.Buttons.TryGetValue(btnKey, out var btnMap) && btnMap != null)
                    currentParam = btnMap.Parameter;
            }

            ButtonParamCombo.SelectedIndex = 0; // Default: nicht zugewiesen
            if (currentParam != null)
            {
                for (int i = 1; i < ButtonParamCombo.Items.Count; i++)
                {
                    if (ButtonParamCombo.Items[i] is ComboBoxItem item && (string)item.Tag == currentParam)
                    {
                        ButtonParamCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            ButtonMappingPanel.Visibility = Visibility.Visible;
            MappingPanel.Visibility = Visibility.Visible;
        }
        finally
        {
            _suppressMappingEvents = false;
        }
    }

    /// <summary>Zeigt das Fader-Mapping-Panel für einen Kanal.</summary>
    private void ShowFaderMappingPanel(int xtChannel)
    {
        if (_config == null || _configService == null)
        {
            MappingPanel.Visibility = Visibility.Collapsed;
            return;
        }

        int vmCh = ResolveVmChannel(xtChannel);
        _selectedVmChannel = vmCh;
        _selectedControlType = "Fader";

        _suppressMappingEvents = true;
        try
        {
            HideMappingSubPanels();

            // ComboBox mit Float-Parametern befüllen
            var floatParams = VoicemeeterParameterCatalog.GetFloatParameters(vmCh);
            FaderParamCombo.Items.Clear();
            FaderParamCombo.Items.Add(new ComboBoxItem { Content = "(nicht zugewiesen)", Tag = "" });

            foreach (var p in floatParams)
            {
                FaderParamCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{p.DisplayName}  [{p.Parameter}]",
                    Tag = p.Parameter
                });
            }

            // Aktuellen Wert auswählen
            FaderParamCombo.SelectedIndex = 0;
            FaderMinBox.Text = "-60";
            FaderMaxBox.Text = "12";

            if (_config.Mappings.TryGetValue(vmCh, out var mapping) && mapping.Fader != null)
            {
                FaderMinBox.Text = mapping.Fader.Min.ToString();
                FaderMaxBox.Text = mapping.Fader.Max.ToString();

                for (int i = 1; i < FaderParamCombo.Items.Count; i++)
                {
                    if (FaderParamCombo.Items[i] is ComboBoxItem item && (string)item.Tag == mapping.Fader.Parameter)
                    {
                        FaderParamCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            FaderMappingPanel.Visibility = Visibility.Visible;
            MappingPanel.Visibility = Visibility.Visible;
        }
        finally
        {
            _suppressMappingEvents = false;
        }
    }

    /// <summary>Zeigt das Encoder-Mapping-Panel für einen Kanal.</summary>
    private void ShowEncoderMappingPanel(int xtChannel)
    {
        if (_config == null || _configService == null)
        {
            MappingPanel.Visibility = Visibility.Collapsed;
            return;
        }

        int vmCh = ResolveVmChannel(xtChannel);
        _selectedVmChannel = vmCh;
        _selectedControlType = "Encoder";

        _suppressMappingEvents = true;
        try
        {
            HideMappingSubPanels();

            // Funktionsliste befüllen
            RefreshEncoderFunctionList(vmCh);

            // ComboBox mit Float-Parametern befüllen (für "Hinzufügen")
            var floatParams = VoicemeeterParameterCatalog.GetFloatParameters(vmCh);
            EncoderAddParamCombo.Items.Clear();
            foreach (var p in floatParams)
            {
                EncoderAddParamCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{p.DisplayName}  [{p.Parameter}]",
                    Tag = p.Parameter
                });
            }
            if (EncoderAddParamCombo.Items.Count > 0)
                EncoderAddParamCombo.SelectedIndex = 0;

            EncoderAddLabelBox.Text = "";

            EncoderMappingPanel.Visibility = Visibility.Visible;
            MappingPanel.Visibility = Visibility.Visible;
        }
        finally
        {
            _suppressMappingEvents = false;
        }
    }

    /// <summary>Aktualisiert die Encoder-Funktionsliste im Panel.</summary>
    private void RefreshEncoderFunctionList(int vmCh)
    {
        EncoderFunctionList.Items.Clear();

        if (_config?.Mappings.TryGetValue(vmCh, out var mapping) == true)
        {
            foreach (var fn in mapping.EncoderFunctions)
            {
                EncoderFunctionList.Items.Add(new ListBoxItem
                {
                    Content = $"{fn.Label,-7} → {fn.Parameter} ({fn.Min}..{fn.Max}, Step {fn.Step} {fn.Unit})",
                    Tag = fn
                });
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Mapping-Editor: Event-Handler (aus XAML referenziert)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Button-Parameter geändert (ComboBox SelectionChanged).</summary>
    private void OnButtonParamChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents || _config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Button") return;

        var selected = ButtonParamCombo.SelectedItem as ComboBoxItem;
        string paramName = (string)(selected?.Tag ?? "");

        EnsureMapping(_selectedVmChannel);
        var mapping = _config.Mappings[_selectedVmChannel];
        string btnKey = _selectedButtonType.ToString();

        if (string.IsNullOrEmpty(paramName))
        {
            mapping.Buttons[btnKey] = null;
        }
        else
        {
            mapping.Buttons[btnKey] = new ButtonMappingConfig { Parameter = paramName };
        }

        SaveAndReload();
    }

    /// <summary>Button-Zuweisung entfernen (Clear-Button Click).</summary>
    private void OnButtonMappingClear(object sender, RoutedEventArgs e)
    {
        if (_config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Button") return;

        EnsureMapping(_selectedVmChannel);
        string btnKey = _selectedButtonType.ToString();
        _config.Mappings[_selectedVmChannel].Buttons[btnKey] = null;

        _suppressMappingEvents = true;
        ButtonParamCombo.SelectedIndex = 0;
        _suppressMappingEvents = false;

        SaveAndReload();
    }

    /// <summary>Fader-Parameter geändert (ComboBox SelectionChanged).</summary>
    private void OnFaderParamChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMappingEvents || _config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Fader") return;

        var selected = FaderParamCombo.SelectedItem as ComboBoxItem;
        string paramName = (string)(selected?.Tag ?? "");

        if (string.IsNullOrEmpty(paramName))
        {
            // Sofort speichern: Fader-Zuweisung entfernen
            EnsureMapping(_selectedVmChannel);
            _config.Mappings[_selectedVmChannel].Fader = null;
            SaveAndReload();
            return;
        }

        // Min/Max aus Katalog vorausfüllen
        var template = VoicemeeterParameterCatalog.FindTemplate(paramName);
        if (template != null)
        {
            FaderMinBox.Text = template.DefaultMin.ToString();
            FaderMaxBox.Text = template.DefaultMax.ToString();
        }
    }

    /// <summary>Fader-Mapping speichern (Speichern-Button Click).</summary>
    private void OnFaderMappingSave(object sender, RoutedEventArgs e)
    {
        if (_config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Fader") return;

        var selected = FaderParamCombo.SelectedItem as ComboBoxItem;
        string paramName = (string)(selected?.Tag ?? "");

        EnsureMapping(_selectedVmChannel);

        if (string.IsNullOrEmpty(paramName))
        {
            _config.Mappings[_selectedVmChannel].Fader = null;
        }
        else
        {
            if (!double.TryParse(FaderMinBox.Text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double min))
                min = -60;
            if (!double.TryParse(FaderMaxBox.Text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double max))
                max = 12;

            var template = VoicemeeterParameterCatalog.FindTemplate(paramName);
            double step = template?.DefaultStep ?? 0.1;

            _config.Mappings[_selectedVmChannel].Fader = new FaderMappingConfig
            {
                Parameter = paramName,
                Min = min,
                Max = max,
                Step = step
            };
        }

        SaveAndReload();
    }

    /// <summary>Encoder-Funktion hinzufügen (+-Button Click).</summary>
    private void OnEncoderFunctionAdd(object sender, RoutedEventArgs e)
    {
        if (_config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Encoder") return;

        var selected = EncoderAddParamCombo.SelectedItem as ComboBoxItem;
        string paramName = (string)(selected?.Tag ?? "");
        if (string.IsNullOrEmpty(paramName)) return;

        string label = EncoderAddLabelBox.Text.Trim();
        if (string.IsNullOrEmpty(label))
        {
            // Label aus DisplayName ableiten
            var resolved = VoicemeeterParameterCatalog.GetFloatParameters(_selectedVmChannel)
                .FirstOrDefault(p => p.Parameter == paramName);
            label = resolved?.DisplayName ?? "PARAM";
            if (label.Length > 7) label = label[..7];
        }
        else if (label.Length > 7)
        {
            label = label[..7];
        }

        var template = VoicemeeterParameterCatalog.FindTemplate(paramName);

        EnsureMapping(_selectedVmChannel);
        _config.Mappings[_selectedVmChannel].EncoderFunctions.Add(new EncoderFunctionConfig
        {
            Label = label.ToUpperInvariant(),
            Parameter = paramName,
            Min = template?.DefaultMin ?? 0,
            Max = template?.DefaultMax ?? 1,
            Step = template?.DefaultStep ?? 0.5,
            Unit = template?.Unit ?? ""
        });

        RefreshEncoderFunctionList(_selectedVmChannel);
        EncoderAddLabelBox.Text = "";
        SaveAndReload();
    }

    /// <summary>Encoder-Funktion entfernen (−-Button Click).</summary>
    private void OnEncoderFunctionRemove(object sender, RoutedEventArgs e)
    {
        if (_config == null || _configService == null) return;
        if (_selectedVmChannel < 0 || _selectedControlType != "Encoder") return;

        int idx = EncoderFunctionList.SelectedIndex;
        if (idx < 0) return;

        if (_config.Mappings.TryGetValue(_selectedVmChannel, out var mapping) &&
            idx < mapping.EncoderFunctions.Count)
        {
            mapping.EncoderFunctions.RemoveAt(idx);
            RefreshEncoderFunctionList(_selectedVmChannel);
            SaveAndReload();
        }
    }

    // ─── Mapping Helpers ─────────────────────────────────────────────

    /// <summary>Stellt sicher, dass ein Mapping für den VM-Kanal existiert.</summary>
    private void EnsureMapping(int vmChannel)
    {
        if (_config == null) return;
        if (!_config.Mappings.ContainsKey(vmChannel))
        {
            _config.Mappings[vmChannel] = new ControlMappingConfig();
        }
    }

    /// <summary>Speichert die Config und benachrichtigt die Bridge.</summary>
    private void SaveAndReload()
    {
        if (_config == null || _configService == null) return;
        _configService.Save(_config);
        _bridge?.ReloadMappings();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Hilfsfunktionen
    // ═══════════════════════════════════════════════════════════════════

    private string GetConfigName(int ch)
    {
        return _device?.Channels[ch].Display.TopRow.TrimEnd() ?? $"Ch {ch + 1}";
    }

    private static Color XTouchColorToWpf(XTouchColor color, bool dim)
    {
        int factor = dim ? 40 : 255;
        return color switch
        {
            XTouchColor.Red => Color.FromRgb((byte)factor, 0, 0),
            XTouchColor.Green => Color.FromRgb(0, (byte)factor, 0),
            XTouchColor.Yellow => Color.FromRgb((byte)factor, (byte)(factor * 0.85), 0),
            XTouchColor.Blue => Color.FromRgb(0, (byte)(factor * 0.4), (byte)factor),
            XTouchColor.Magenta => Color.FromRgb((byte)factor, 0, (byte)factor),
            XTouchColor.Cyan => Color.FromRgb(0, (byte)factor, (byte)factor),
            XTouchColor.White => Color.FromRgb((byte)(factor * 0.8), (byte)(factor * 0.8), (byte)(factor * 0.8)),
            _ => Color.FromRgb(20, 20, 20)
        };
    }

    // ─── Channel View Editor ────────────────────────────────────────

    private void OnOpenChannelViewEditor(object sender, RoutedEventArgs e)
    {
        if (_config == null || _configService == null) return;

        var dialog = new ChannelViewEditorDialog(_config, _configService, _bridge, _vm)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    // ─── Cleanup ─────────────────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer.Stop();
        base.OnClosed(e);
    }
}
