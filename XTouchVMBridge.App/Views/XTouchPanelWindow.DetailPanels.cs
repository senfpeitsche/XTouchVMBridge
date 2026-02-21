using System.Windows;
using System.Windows.Media;
using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Hardware;
using Color = System.Windows.Media.Color;

namespace XTouchVMBridge.App.Views;

public partial class XTouchPanelWindow
{
    private void ShowDisplayDetail(int ch)
    {
        _activeDetailType = "Display"; _activeDetailChannel = ch;
        MappingPanel.Visibility = Visibility.Collapsed;
        DetailMidiInfo.Text = "";
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
        _activeDetailType = "Encoder"; _activeDetailChannel = ch;
        DetailMidiInfo.Text = "";
        var enc = _device?.Channels[ch].Encoder;

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

        ShowEncoderMappingPanel(ch);
    }

    private void ShowButtonDetail(int ch, XTouchButtonType type)
    {
        _activeDetailType = "Button"; _activeDetailChannel = ch;
        DetailMidiInfo.Text = "";
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

        ShowButtonMappingPanel(ch, type);
    }

    private void ShowFaderDetail(int ch)
    {
        _activeDetailType = "Fader"; _activeDetailChannel = ch;
        DetailMidiInfo.Text = "";
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

        ShowFaderMappingPanel(ch);
    }

    private void ShowLevelMeterDetail(int ch)
    {
        _activeDetailType = "LevelMeter"; _activeDetailChannel = ch;
        MappingPanel.Visibility = Visibility.Collapsed;

        DetailHeader.Text = $"Level Meter — Kanal {ch + 1}";
        DetailText.Text =
            $"Stufen:          0=Stille, 1..8=Normal, 9..11=Laut, 12..13=Clip\n\n" +
            $"Funktion:        Zeigt den Post-Fader-Pegel des gemappten VM-Kanals.\n" +
            $"                 Wird alle 100ms per Polling aktualisiert.\n\n" +
            $"dB-Skala:        -200→0, -100→1, -50→2, -40→3, -35→4, -30→5,\n" +
            $"                 -25→6, -20→7, -15→8, -10→9, -5→10, 0→11, +5→12\n\n" +
            $"Hersteller:      Meter LEDs CC 90..97 (value 0..127)";
        DetailMidiInfo.Text = ""; // Updated by RefreshDetailLiveValues.
    }

    private void ShowMainFaderDetail()
    {
        _activeDetailType = "MainFader"; _activeDetailChannel = -1;
        MappingPanel.Visibility = Visibility.Collapsed;
        DetailMidiInfo.Text = "";
        DetailHeader.Text = "Main Fader";
        DetailText.Text =
            "Funktion:       Master-Fader. Steuert den Master-Bus-Pegel.\n" +
            "                In Mackie Control: Kanal 9 (Index 8)\n\n" +
            "MIDI Position:  Pitchwheel Ch 8 (14-bit signed)\n" +
            "MIDI Touch:     Note On #118 (touch: vel 127, release: vel 0)\n" +
            "MIDI CC Mode:   CC 78 (value 0..127) — nur im MIDI-Mode\n\n" +
            "Hersteller:     Fader CC 70..78, Fader Touch Note #110..118";
    }
}
