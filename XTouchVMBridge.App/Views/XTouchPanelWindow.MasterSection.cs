using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XTouchVMBridge.Core.Enums;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;

namespace XTouchVMBridge.App.Views;

public partial class XTouchPanelWindow
{
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
        BuildNavigationButtons();
        BuildScrubButton();
        SetupJogWheel();
    }

    private void BuildEncoderAssignButtons()
    {
        var names = new[] { "TRACK", "PAN", "EQ", "SEND", "PLUG-IN", "INST" };
        var notes = new[] { 40, 42, 44, 41, 43, 45 }; // Mackie Control note numbers
        for (int i = 0; i < names.Length; i++)
        {
            int noteNum = notes[i];
            var btn = CreateMasterButton(names[i], $"EncoderAssign_{names[i]}", Color.FromRgb(35, 35, 35), Color.FromRgb(80, 80, 80));
            RegisterMasterButtonVisual(btn, noteNum, Color.FromRgb(80, 80, 80), Color.FromRgb(35, 35, 35));
            btn.Click += (s, _) => OnMasterButtonClick(s,"Encoder Assign", names[Array.IndexOf(notes, noteNum)], noteNum,
                "Wählt den Parameter für alle 8 Encoder.\nDrehen der Encoder ändert den gewählten Parameter pro Kanal.");
            EncoderAssignPanel.Children.Add(btn);
        }
    }

    private void BuildDisplayModeButtons()
    {
        var displayItems = new (string Name, int Note, string Desc)[]
        {
            ("NAME", 52, "Zeigt Kanalnamen im Display"),
            ("VALUE", 53, "Zeigt Parameterwert im Display")
        };
        foreach (var (name, note, desc) in displayItems)
        {
            var btn = CreateMasterButton(name, $"Display_{name}", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
            RegisterMasterButtonVisual(btn, note, Color.FromRgb(70, 70, 70), Color.FromRgb(35, 35, 35));
            btn.Click += (s, _) => OnMasterButtonClick(s,"Display Mode", name, note, desc);
            DisplayModePanel.Children.Add(btn);
        }

        var timecodeItems = new (string Name, int Note, string Desc)[]
        {
            ("SMPTE", 113, "Timecode: SMPTE-Format (HH:MM:SS:FF)"),
            ("BEATS", 114, "Timecode: Bars/Beats-Format")
        };
        foreach (var (name, note, desc) in timecodeItems)
        {
            var btn = CreateMasterButton(name, $"Timecode_{name}", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
            RegisterMasterButtonVisual(btn, note, Color.FromRgb(70, 70, 70), Color.FromRgb(35, 35, 35));
            btn.Click += (s, _) => OnMasterButtonClick(s,"Timecode Mode", name, note, desc);
            TimecodeModePanelXaml.Children.Add(btn);
        }
    }

    private void BuildGlobalViewButtons()
    {
        var gvBtn = CreateMasterButton("G.VIEW", "GlobalView_Main", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
        RegisterMasterButtonVisual(gvBtn, 51, Color.FromRgb(70, 70, 70), Color.FromRgb(35, 35, 35));
        gvBtn.Click += (s, _) => OnMasterButtonClick(s, "Global View", "Global View", 51,
            "Global View — Aktiviert/deaktiviert den Global-View-Modus.\nZeigt alle Kanäle unabhängig vom Typ.");
        GlobalViewPanel.Children.Add(gvBtn);

        var names = new[] { "MIDI TR", "INPUTS", "AUDIO", "A.INST", "AUX", "BUSES", "OUTPUTS", "USER" };
        var notes = new[] { 62, 63, 64, 65, 66, 67, 68, 69 };
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var btn = CreateMasterButton(names[i], $"GlobalView_{names[i]}", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
            RegisterMasterButtonVisual(btn, notes[idx], Color.FromRgb(70, 70, 70), Color.FromRgb(35, 35, 35));
            btn.Click += (s, _) => OnMasterButtonClick(s,"Global View", names[idx], notes[idx],
                $"Globale Ansicht: {names[idx]}.\nFiltert die Kanalstreifen nach Typ.");
            GlobalViewPanel.Children.Add(btn);
        }
    }

    private void BuildFunctionButtons()
    {
        for (int i = 0; i < 8; i++)
        {
            int idx = i;
            string label = $"F{i + 1}";
            int noteNum = 54 + i; // F1=54..F8=61
            var btn = CreateMasterButton(label, $"Function_{label}", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
            RegisterMasterButtonVisual(btn, noteNum, Color.FromRgb(70, 70, 70), Color.FromRgb(35, 35, 35));
            btn.Click += (s, _) => OnMasterButtonClick(s,"Function", label, noteNum,
                $"Funktion {idx + 1} — benutzerdefinierbar.\nZuweisung hängt von der DAW/Anwendung ab.");
            FunctionPanel.Children.Add(btn);
        }
    }

    private void BuildModifyButtons()
    {
        var items = new (string Name, int Note)[] { ("SHIFT", 70), ("OPTION", 71), ("CONTROL", 72), ("ALT", 73) };
        foreach (var (name, note) in items)
        {
            var btn = CreateMasterButton(name, $"Modify_{name}", Color.FromRgb(30, 30, 45), Color.FromRgb(60, 60, 100));
            RegisterMasterButtonVisual(btn, note, Color.FromRgb(60, 60, 100), Color.FromRgb(30, 30, 45));
            btn.Click += (s, _) => OnMasterButtonClick(s,"Modify", name, note,
                $"Modifier-Taste '{name}'.\nKombiniert mit anderen Tasten für erweiterte Funktionen.");
            ModifyPanel.Children.Add(btn);
        }
    }

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
            RegisterMasterButtonVisual(btn, note, Color.FromRgb(60, 90, 60), Color.FromRgb(30, 40, 30));
            btn.Click += (s, _) => OnMasterButtonClick(s,"Automation", name, note,
                $"Automation-Modus '{name}'.\nSteuert den Automation-Modus der DAW.");
            AutomationPanel.Children.Add(btn);
        }
    }

    private void BuildUtilityButtons()
    {
        var items = new (string Name, int Note)[] { ("SAVE", 80), ("UNDO", 81), ("CANCEL", 82), ("ENTER", 83) };
        foreach (var (name, note) in items)
        {
            var btn = CreateMasterButton(name, $"Util_{name}", Color.FromRgb(40, 35, 25), Color.FromRgb(90, 80, 50));
            RegisterMasterButtonVisual(btn, note, Color.FromRgb(90, 80, 50), Color.FromRgb(40, 35, 25));
            btn.Click += (s, _) => OnMasterButtonClick(s,"Utility", name, note,
                $"Utility-Taste '{name}'.\nWird von der DAW zugewiesen.");
            UtilityPanel.Children.Add(btn);
        }
    }

    private void BuildTransportButtons()
    {
        var topItems = new (string Name, int Note)[]
        {
            ("MARKER", 84), ("NUDGE", 85), ("CYCLE", 86), ("DROP", 87),
            ("REPLACE", 88), ("CLICK", 89), ("SOLO", 90)
        };
        foreach (var (name, note) in topItems)
        {
            var btn = CreateMasterButton(name, $"Transport_{name}", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
            RegisterMasterButtonVisual(btn, note, Color.FromRgb(70, 70, 70), Color.FromRgb(35, 35, 35));
            btn.Click += (s, _) => OnMasterButtonClick(s,"Transport", name, note,
                $"Transport-Taste '{name}'.\nSteuert die DAW-Transport-Funktionen.");
            TransportTopPanel.Children.Add(btn);
        }

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
            RegisterMasterButtonVisual(btn, note, activeColor, Color.FromRgb(40, 40, 40));
            btn.Click += (s, _) => OnMasterButtonClick(s,"Transport", name, note,
                $"Transport: {name} ({symbol})\nMIDI: Note On #{note}");
            _masterButtons[$"Transport_{name}"] = btn;
            TransportButtonPanel.Children.Add(btn);
        }
    }

    private void BuildFaderBankChannelButtons()
    {
        var fbLeft = CreateMasterButton("◄", "FaderBank_Left", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
        RegisterMasterButtonVisual(fbLeft, 46, Color.FromRgb(70, 70, 70), Color.FromRgb(35, 35, 35));
        fbLeft.FontSize = 14; fbLeft.Width = 36;
        fbLeft.Click += (s, _) => OnMasterButtonClick(s,"Fader Bank", "Bank Left", 46,
            "Fader Bank Links — Frei zuweisbar.\n(Channel View Cycling erfolgt über FLIP-Button)");
        FaderBankPanel.Children.Add(fbLeft);
        _masterButtons["FaderBank_Left"] = fbLeft;

        var fbRight = CreateMasterButton("►", "FaderBank_Right", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
        RegisterMasterButtonVisual(fbRight, 47, Color.FromRgb(70, 70, 70), Color.FromRgb(35, 35, 35));
        fbRight.FontSize = 14; fbRight.Width = 36;
        fbRight.Click += (s, _) => OnMasterButtonClick(s,"Fader Bank", "Bank Right", 47,
            "Fader Bank Rechts — Frei zuweisbar.\n(Channel View Cycling erfolgt über FLIP-Button)");
        FaderBankPanel.Children.Add(fbRight);
        _masterButtons["FaderBank_Right"] = fbRight;

        var chLeft = CreateMasterButton("◄", "Channel_Left", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
        RegisterMasterButtonVisual(chLeft, 48, Color.FromRgb(70, 70, 70), Color.FromRgb(35, 35, 35));
        chLeft.FontSize = 14; chLeft.Width = 36;
        chLeft.Click += (s, _) => OnMasterButtonClick(s,"Channel", "Channel Left", 48,
            "Channel Links — Frei zuweisbar.");
        ChannelNavPanel.Children.Add(chLeft);
        _masterButtons["Channel_Left"] = chLeft;

        var chRight = CreateMasterButton("►", "Channel_Right", Color.FromRgb(35, 35, 35), Color.FromRgb(70, 70, 70));
        RegisterMasterButtonVisual(chRight, 49, Color.FromRgb(70, 70, 70), Color.FromRgb(35, 35, 35));
        chRight.FontSize = 14; chRight.Width = 36;
        chRight.Click += (s, _) => OnMasterButtonClick(s,"Channel", "Channel Right", 49,
            "Channel Rechts — Frei zuweisbar.");
        ChannelNavPanel.Children.Add(chRight);
        _masterButtons["Channel_Right"] = chRight;
    }

    private void BuildNavigationButtons()
    {
        var navItems = new (string Label, string Name, int Note, string Desc, int Row, int Col)[]
        {
            ("▲",  "Up",     96, "Cursor hoch — Navigation in Listen/Menüs",       0, 1),
            ("◄",  "Left",   98, "Cursor links — Navigation / Zoom out",            1, 0),
            ("●",  "Select", 100, "Zoom/Select — Auswahl bestätigen / Zoom toggle", 1, 1),
            ("►",  "Right",  99, "Cursor rechts — Navigation / Zoom in",            1, 2),
            ("▼",  "Down",   97, "Cursor runter — Navigation in Listen/Menüs",      2, 1)
        };
        foreach (var (label, name, note, desc, row, col) in navItems)
        {
            var bgColor = name == "Select" ? Color.FromRgb(30, 45, 30) : Color.FromRgb(35, 35, 35);
            var borderColor = name == "Select" ? Color.FromRgb(60, 90, 60) : Color.FromRgb(70, 70, 70);
            var btn = CreateMasterButton(label, $"Nav_{name}", bgColor, borderColor);
            RegisterMasterButtonVisual(btn, note, borderColor, bgColor);
            btn.Width = 36;
            btn.Height = 26;
            btn.FontSize = 12;
            btn.Click += (s, _) => OnMasterButtonClick(s, "Navigation", name, note, desc);
            Grid.SetRow(btn, row);
            Grid.SetColumn(btn, col);
            NavigationPanel.Children.Add(btn);
        }
    }

    private void BuildScrubButton()
    {
        RegisterMasterButtonVisual(ScrubButton, 101, Color.FromRgb(70, 70, 70), Color.FromRgb(35, 35, 35));
        ScrubButton.Click += (s, _) => OnMasterButtonClick(s,"Control", "SCRUB", 101,
            "SCRUB-Taste aktiviert den Scrub-Modus für das Jog Wheel.\n" +
            "Im Scrub-Modus: Frame-genaue Audio-Wiedergabe beim Drehen.");
        _masterButtons["Scrub"] = ScrubButton;
    }

    private void SetupJogWheel()
    {
        JogWheelButton.Click += (_, _) =>
        {
            MappingPanel.Visibility = Visibility.Collapsed;
            DetailHeader.Text = "Jog Wheel";
            DetailText.Text =
                "Funktion:       Scrub / Shuttle / Navigation\n" +
                "                Dreht durch Timeline-Positionen, Marker etc.\n\n" +
                "MIDI:           CC 60\n" +
                "                CW (rechts):  value = 1\n" +
                "                CCW (links):  value = 65\n" +
                "                Schnelles Drehen: höhere Werte (1..15 / 65..79)\n\n" +
                "Scrub-Button:   Note On #101 (toggle)\n\n" +
                "Hersteller-Doku: Jog Wheel CC 60";
        };
    }

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

    private void OnMasterButtonClick(object sender, string section, string name, int noteNumber, string description)
    {
        if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
        {
            if (_masterButtonActionService?.ExecuteAction(noteNumber) == true)
                return;

            LedState currentState = _device?.MasterButtonLedStates.TryGetValue(noteNumber, out var ledState) == true
                ? ledState
                : LedState.Off;
            var newState = currentState == LedState.Off ? LedState.On : LedState.Off;

            _device?.SetMasterButtonLed(noteNumber, newState);
            return;
        }

        ShowMasterButtonDetail(section, name, noteNumber, description);
    }

    private void ShowMasterButtonDetail(string section, string name, int noteNumber, string description)
    {
        _activeDetailType = "Master"; _activeDetailChannel = -1;
        DetailMidiInfo.Text = "";
        _selectedMasterButtonNote = noteNumber;
        _selectedControlType = "MasterButton";
        _selectedVmChannel = -1;

        DetailHeader.Text = $"{section} — {name}";
        DetailText.Text =
            $"{description}\n\n" +
            $"MIDI:           Note On #{noteNumber}\n" +
            $"                Push: vel 127, Release: vel 0\n" +
            $"LED-Feedback:   Note On #{noteNumber} (vel 0=off, 64=blink, 127=on)\n\n" +
            $"Mackie Control: Standardisierte Zuordnung im MCU-Protokoll.";

        ShowMasterButtonMappingPanel(noteNumber);
    }
}
