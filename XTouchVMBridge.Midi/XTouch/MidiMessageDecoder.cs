using XTouchVMBridge.Core.Enums;

namespace XTouchVMBridge.Midi.XTouch;

/// <summary>
/// Dekodiert rohe MIDI-Bytes in lesbare Debug-Informationen.
/// Basiert auf der offiziellen Behringer X-Touch/X-Touch Extender MIDI Mode Dokumentation.
///
/// Unterstützte Message-Typen (laut Hersteller-Doku):
/// - Buttons:        Note On #0..103  (push: vel 127, release: vel 0)
/// - Button LEDs:    Note On #0..103  (vel 0..63: off, vel 64: flash, vel 65..127: on)
/// - Fader:          CC 70..77(78)    (receive and transmit)
/// - Fader Touch:    Note On #104..111(112) (touch: vel 127, release: vel 0)
/// - Encoder:        CC 80..87        (absolute: 0..127, relative: inc=65, dec=1)
/// - Encoder Rings:  CC 80..87        (value 0..127)
/// - Jog Wheel:      CC 88            (CW: 65, CCW: 1)
/// - Meter LEDs:     CC 90..97        (value 0..127)
/// - Foot Controller:CC 4             (value 0..127)
/// - Foot Switch:    CC 64 (FS1), CC 67 (FS2) (push: vel 127, release: vel 0)
/// - LCDs:           SysEx F0 00 20 32 dd 4C nn cc c1..c14 F7
/// - Segment Disp:   SysEx F0 00 20 32 dd 37 s1..s12 d1 d2 F7
/// </summary>
public static class MidiMessageDecoder
{
    /// <summary>
    /// Ergebnis einer MIDI-Nachrichten-Dekodierung.
    /// </summary>
    public record DecodedMidiMessage(
        /// <summary>Zeitstempel der Nachricht.</summary>
        DateTime Timestamp,
        /// <summary>Richtung: IN (vom Gerät) oder OUT (zum Gerät).</summary>
        MidiDirection Direction,
        /// <summary>Erkannter Control-Typ (Button, Fader, Encoder, etc.).</summary>
        string ControlType,
        /// <summary>Kanal/Index des Controls (falls zutreffend).</summary>
        string ControlId,
        /// <summary>Menschenlesbare Beschreibung des Werts.</summary>
        string Value,
        /// <summary>Was die Anwendung mit dieser Nachricht macht.</summary>
        string Action,
        /// <summary>Rohe MIDI-Bytes als Hex-String.</summary>
        string RawHex
    );

    public enum MidiDirection { In, Out }

    // ─── Haupt-Dekodierung ──────────────────────────────────────────

    /// <summary>
    /// Dekodiert eine eingehende (vom Gerät empfangene) MIDI-Nachricht.
    /// </summary>
    public static DecodedMidiMessage DecodeIncoming(int rawMessage, DateTime? timestamp = null)
    {
        var ts = timestamp ?? DateTime.Now;
        byte status = (byte)(rawMessage & 0xFF);
        byte data1 = (byte)((rawMessage >> 8) & 0xFF);
        byte data2 = (byte)((rawMessage >> 16) & 0xFF);
        string hex = $"{status:X2} {data1:X2} {data2:X2}";

        byte statusType = (byte)(status & 0xF0);
        byte channel = (byte)(status & 0x0F);

        return statusType switch
        {
            0x90 => DecodeNoteOn(ts, data1, data2, hex),
            0x80 => DecodeNoteOff(ts, data1, hex),
            0xB0 => DecodeControlChange(ts, data1, data2, hex),
            0xE0 => DecodePitchWheel(ts, channel, data1, data2, hex),
            0xD0 => DecodeAftertouch(ts, data1, hex),
            _ => new DecodedMidiMessage(ts, MidiDirection.In, "Unbekannt", $"Status {status:X2}",
                $"D1={data1} D2={data2}", "Ignoriert", hex)
        };
    }

    /// <summary>
    /// Dekodiert eine ausgehende (zum Gerät gesendete) MIDI-Nachricht.
    /// </summary>
    public static DecodedMidiMessage DecodeOutgoing(int rawMessage, DateTime? timestamp = null)
    {
        var ts = timestamp ?? DateTime.Now;
        byte status = (byte)(rawMessage & 0xFF);
        byte data1 = (byte)((rawMessage >> 8) & 0xFF);
        byte data2 = (byte)((rawMessage >> 16) & 0xFF);
        string hex = $"{status:X2} {data1:X2} {data2:X2}";

        byte statusType = (byte)(status & 0xF0);
        byte channel = (byte)(status & 0x0F);

        return statusType switch
        {
            0x90 => DecodeButtonLedOutput(ts, data1, data2, hex),
            0xB0 => DecodeCcOutput(ts, data1, data2, hex),
            0xE0 => DecodeFaderOutput(ts, channel, data1, data2, hex),
            0xD0 => DecodeMeterOutput(ts, data1, hex),
            _ => new DecodedMidiMessage(ts, MidiDirection.Out, "Unbekannt", $"Status {status:X2}",
                $"D1={data1} D2={data2}", "Gesendet", hex)
        };
    }

    /// <summary>
    /// Dekodiert eine SysEx-Nachricht (ein- oder ausgehend).
    /// </summary>
    public static DecodedMidiMessage DecodeSysEx(byte[] data, MidiDirection direction, DateTime? timestamp = null)
    {
        var ts = timestamp ?? DateTime.Now;
        string hex = string.Join(" ", data.Select(b => $"{b:X2}"));

        if (data.Length < 7)
            return new DecodedMidiMessage(ts, direction, "SysEx", "Unbekannt", "Zu kurz", "Ignoriert", hex);

        // Prüfe auf Behringer X-Touch Header: F0 00 20 32 dd ...
        if (data[1] == 0x00 && data[2] == 0x20 && data[3] == 0x32)
        {
            byte deviceId = data[4];
            string deviceName = deviceId switch
            {
                0x14 => "X-Touch",
                0x15 => "X-Touch-Ext",
                _ => $"ID {deviceId:X2}"
            };

            byte command = data[5];
            return command switch
            {
                0x4C => DecodeLcdSysEx(ts, direction, data, deviceName, hex),
                0x37 => DecodeSegmentSysEx(ts, direction, data, deviceName, hex),
                _ => new DecodedMidiMessage(ts, direction, "SysEx", deviceName,
                    $"Cmd {command:X2}", "Unbekannt", hex)
            };
        }

        // Mackie SysEx (F0 00 00 66 15 ...)
        if (data.Length >= 6 && data[1] == 0x00 && data[2] == 0x00 && data[3] == 0x66)
        {
            byte cmd = data[5];
            return cmd switch
            {
                MackieProtocol.CmdDisplayText => DecodeMackieDisplayText(ts, direction, data, hex),
                MackieProtocol.CmdDisplayColor => DecodeMackieDisplayColor(ts, direction, data, hex),
                MackieProtocol.CmdHandshake => new DecodedMidiMessage(ts, direction, "SysEx",
                    "Mackie", "Handshake Challenge", "Handshake", hex),
                MackieProtocol.CmdHandshakeResponse => new DecodedMidiMessage(ts, direction, "SysEx",
                    "Mackie", "Handshake Response", "Handshake", hex),
                _ => new DecodedMidiMessage(ts, direction, "SysEx", "Mackie",
                    $"Cmd {cmd:X2}", "Unbekannt", hex)
            };
        }

        return new DecodedMidiMessage(ts, direction, "SysEx", "Unbekannt",
            $"{data.Length} Bytes", "Ignoriert", hex);
    }

    // ─── Eingehende Nachrichten ─────────────────────────────────────

    private static DecodedMidiMessage DecodeNoteOn(DateTime ts, byte note, byte velocity, string hex)
    {
        bool isPressed = velocity > 0;

        // Fader Touch: Note 104..111 (Extender), Main=112
        if (note is >= 104 and <= 112)
        {
            int ch = note - 104;
            string channelName = ch == 8 ? "Main" : $"Kanal {ch + 1}";
            return new DecodedMidiMessage(ts, MidiDirection.In, "Fader Touch",
                channelName, isPressed ? "Berührt" : "Losgelassen",
                isPressed ? "→ FaderTouched" : "→ FaderTouched (release)", hex);
        }

        // Encoder Press: Note 32..39
        if (note is >= 32 and <= 39)
        {
            int ch = note - 32;
            return new DecodedMidiMessage(ts, MidiDirection.In, "Encoder Press",
                $"Kanal {ch + 1}", isPressed ? "Gedrückt" : $"Losgelassen",
                isPressed ? "→ EncoderPressed" : "→ EncoderPressed (release)", hex);
        }

        // Channel Buttons: Note 0..31 (4 Typen × 8 Kanäle)
        if (note <= 103)
        {
            int ch = note % 8;
            int typeIndex = note / 8;
            string buttonName = typeIndex switch
            {
                0 => "REC",
                1 => "SOLO",
                2 => "MUTE",
                3 => "SELECT",
                _ => $"Button #{note}"
            };

            string controlId = typeIndex < 4
                ? $"Kanal {ch + 1} {buttonName}"
                : $"Note #{note}";

            return new DecodedMidiMessage(ts, MidiDirection.In, "Button",
                controlId, isPressed ? "Gedrückt (vel 127)" : "Losgelassen (vel 0)",
                typeIndex < 4 ? $"→ ButtonChanged ({buttonName})" : $"→ Note #{note}", hex);
        }

        return new DecodedMidiMessage(ts, MidiDirection.In, "Note On",
            $"Note #{note}", $"Velocity {velocity}", "Nicht zugeordnet", hex);
    }

    private static DecodedMidiMessage DecodeNoteOff(DateTime ts, byte note, string hex)
    {
        return DecodeNoteOn(ts, note, 0, hex);
    }

    private static DecodedMidiMessage DecodeControlChange(DateTime ts, byte cc, byte value, string hex)
    {
        // Fader: CC 70..77
        if (cc is >= 70 and <= 78)
        {
            int ch = cc - 70;
            return new DecodedMidiMessage(ts, MidiDirection.In, "Fader (MIDI-Mode)",
                $"Kanal {ch + 1}", $"Wert {value}",
                "→ Fader CC (MIDI-Mode)", hex);
        }

        // Encoder: CC 80..87
        if (cc is >= 80 and <= 87)
        {
            int ch = cc - 80;
            string valueDesc;
            if (value == 65)
                valueDesc = "Inkrement (+1)";
            else if (value == 1)
                valueDesc = "Dekrement (-1)";
            else if (value is >= 1 and <= 15)
                valueDesc = $"Rechts +{value}";
            else if (value is >= 65 and <= 79)
                valueDesc = $"Links -{value - 64}";
            else
                valueDesc = $"Absolut {value}";

            return new DecodedMidiMessage(ts, MidiDirection.In, "Encoder",
                $"Kanal {ch + 1}", valueDesc,
                "→ EncoderRotated", hex);
        }

        // Jog Wheel: CC 88
        if (cc == 88)
        {
            string dir = value == 65 ? "CW (rechts)" : value == 1 ? "CCW (links)" : $"Wert {value}";
            return new DecodedMidiMessage(ts, MidiDirection.In, "Jog Wheel",
                "Global", dir, "→ JogWheel (nicht implementiert)", hex);
        }

        // Meter LEDs: CC 90..97
        if (cc is >= 90 and <= 97)
        {
            int ch = cc - 90;
            return new DecodedMidiMessage(ts, MidiDirection.In, "Meter LED",
                $"Kanal {ch + 1}", $"Level {value}/127",
                "→ Meter Update", hex);
        }

        // Foot Controller: CC 4
        if (cc == 4)
        {
            return new DecodedMidiMessage(ts, MidiDirection.In, "Foot Controller",
                "FC", $"Wert {value}/127",
                "→ FootController (nicht implementiert)", hex);
        }

        // Foot Switch: CC 64 (FS1), CC 67 (FS2)
        if (cc is 64 or 67)
        {
            string fs = cc == 64 ? "FS1" : "FS2";
            string state = value >= 64 ? "Gedrückt" : "Losgelassen";
            return new DecodedMidiMessage(ts, MidiDirection.In, "Foot Switch",
                fs, state,
                $"→ FootSwitch {fs} (nicht implementiert)", hex);
        }

        return new DecodedMidiMessage(ts, MidiDirection.In, "Control Change",
            $"CC {cc}", $"Wert {value}",
            "Nicht zugeordnet", hex);
    }

    private static DecodedMidiMessage DecodePitchWheel(DateTime ts, byte channel, byte lsb, byte msb, string hex)
    {
        int position = ((msb << 7) | lsb) - 8192;
        double db = Core.Hardware.FaderControl.PositionToDb(position);
        return new DecodedMidiMessage(ts, MidiDirection.In, "Fader",
            $"Kanal {channel + 1}", $"Pos {position} ({db:F1} dB)",
            "→ FaderChanged", hex);
    }

    private static DecodedMidiMessage DecodeAftertouch(DateTime ts, byte data, string hex)
    {
        int ch = (data >> 4) & 0x07;
        int level = data & 0x0F;
        return new DecodedMidiMessage(ts, MidiDirection.In, "Level Meter",
            $"Kanal {ch + 1}", $"Level {level}/13",
            "Ignoriert (Meter-Feedback)", hex);
    }

    // ─── Ausgehende Nachrichten ─────────────────────────────────────

    private static DecodedMidiMessage DecodeButtonLedOutput(DateTime ts, byte note, byte velocity, string hex)
    {
        string ledState;
        if (velocity == 0)
            ledState = "LED Aus";
        else if (velocity <= 63)
            ledState = $"LED Aus (vel {velocity})";
        else if (velocity == 64)
            ledState = "LED Blinken";
        else
            ledState = $"LED An (vel {velocity})";

        // Button-Name ermitteln
        string controlId;
        if (note <= 103)
        {
            int ch = note % 8;
            int typeIndex = note / 8;
            string buttonName = typeIndex switch
            {
                0 => "REC",
                1 => "SOLO",
                2 => "MUTE",
                3 => "SELECT",
                _ => $"Button #{note}"
            };
            controlId = typeIndex < 4 ? $"Kanal {ch + 1} {buttonName}" : $"Note #{note}";
        }
        else if (note is >= 104 and <= 112)
        {
            controlId = note == 112 ? "Fader Touch Main" : $"Fader Touch {note - 104 + 1}";
        }
        else
        {
            controlId = $"Note #{note}";
        }

        return new DecodedMidiMessage(ts, MidiDirection.Out, "Button LED",
            controlId, ledState, "← SetButtonLed", hex);
    }

    private static DecodedMidiMessage DecodeCcOutput(DateTime ts, byte cc, byte value, string hex)
    {
        // Encoder Ring: CC 48..55 (Mackie) oder CC 80..87 (MIDI-Mode)
        if (cc is >= 48 and <= 55)
        {
            int ch = cc - 48;
            int mode = (value >> 4) & 0x03;
            int pos = value & 0x0F;
            bool led = (value & 0x40) != 0;
            string modeName = mode switch
            {
                0 => "Dot",
                1 => "Pan",
                2 => "Wrap",
                3 => "Spread",
                _ => "?"
            };
            return new DecodedMidiMessage(ts, MidiDirection.Out, "Encoder Ring",
                $"Kanal {ch + 1}", $"Pos {pos}/15, Mode={modeName}, LED={led}",
                "← SetEncoderRing", hex);
        }

        // Meter LEDs: CC 90..97
        if (cc is >= 90 and <= 97)
        {
            int ch = cc - 90;
            return new DecodedMidiMessage(ts, MidiDirection.Out, "Meter LED",
                $"Kanal {ch + 1}", $"Level {value}/127",
                "← SetMeterLed", hex);
        }

        return new DecodedMidiMessage(ts, MidiDirection.Out, "Control Change",
            $"CC {cc}", $"Wert {value}", "← CC gesendet", hex);
    }

    private static DecodedMidiMessage DecodeFaderOutput(DateTime ts, byte channel, byte lsb, byte msb, string hex)
    {
        int position = ((msb << 7) | lsb) - 8192;
        double db = Core.Hardware.FaderControl.PositionToDb(position);
        return new DecodedMidiMessage(ts, MidiDirection.Out, "Fader",
            $"Kanal {channel + 1}", $"Pos {position} ({db:F1} dB)",
            "← SetFader", hex);
    }

    private static DecodedMidiMessage DecodeMeterOutput(DateTime ts, byte data, string hex)
    {
        int ch = (data >> 4) & 0x07;
        int level = data & 0x0F;
        return new DecodedMidiMessage(ts, MidiDirection.Out, "Level Meter",
            $"Kanal {ch + 1}", $"Level {level}/13",
            "← SetLevelMeter", hex);
    }

    // ─── SysEx-Dekodierung ──────────────────────────────────────────

    private static DecodedMidiMessage DecodeLcdSysEx(DateTime ts, MidiDirection dir,
        byte[] data, string device, string hex)
    {
        // F0 00 20 32 dd 4C nn cc c1..c14 F7
        if (data.Length < 9) return Fallback(ts, dir, "LCD", hex);

        int lcdNumber = data[6];
        byte colorByte = data[7];
        int colorIndex = colorByte & 0x07;
        bool invertUpper = (colorByte & 0x10) != 0;
        bool invertLower = (colorByte & 0x20) != 0;

        string colorName = ((XTouchColor)colorIndex).ToString();

        // ASCII-Zeichen extrahieren
        int charCount = Math.Min(14, data.Length - 8 - 1); // -1 für F7
        string text = "";
        if (charCount > 0)
        {
            var chars = new char[charCount];
            for (int i = 0; i < charCount; i++)
                chars[i] = (char)data[8 + i];
            text = new string(chars);
        }

        string upperHalf = text.Length >= 7 ? text[..7] : text;
        string lowerHalf = text.Length > 7 ? text[7..] : "";

        return new DecodedMidiMessage(ts, dir, "LCD Display",
            $"{device} LCD #{lcdNumber + 1}",
            $"Farbe={colorName}, InvU={invertUpper}, InvL={invertLower}, Oben=\"{upperHalf}\", Unten=\"{lowerHalf}\"",
            dir == MidiDirection.Out ? "← LCD Update" : "LCD Daten", hex);
    }

    private static DecodedMidiMessage DecodeSegmentSysEx(DateTime ts, MidiDirection dir,
        byte[] data, string device, string hex)
    {
        // F0 00 20 32 dd 37 s1..s12 d1 d2 F7
        return new DecodedMidiMessage(ts, dir, "Segment Display",
            $"{device} 7-Segment", $"{data.Length - 4} Datenbytes",
            dir == MidiDirection.Out ? "← Segment Update" : "Segment Daten", hex);
    }

    private static DecodedMidiMessage DecodeMackieDisplayText(DateTime ts, MidiDirection dir,
        byte[] data, string hex)
    {
        // F0 00 00 66 15 12 offset text... F7
        if (data.Length < 8) return Fallback(ts, dir, "Display Text", hex);

        int offset = data[6];
        int textLen = data.Length - 8; // -header -offset -F7
        string text = "";
        if (textLen > 0)
        {
            var chars = new char[textLen];
            for (int i = 0; i < textLen; i++)
                chars[i] = (char)data[7 + i];
            text = new string(chars);
        }

        int channel = offset / 7;
        int row = offset >= 56 ? 1 : 0;

        return new DecodedMidiMessage(ts, dir, "Display Text",
            $"Offset {offset} (Ch {channel + 1}, Zeile {row + 1})",
            $"\"{text.TrimEnd()}\"",
            "← SetDisplayText", hex);
    }

    private static DecodedMidiMessage DecodeMackieDisplayColor(DateTime ts, MidiDirection dir,
        byte[] data, string hex)
    {
        // F0 00 00 66 15 72 c0..c7 F7
        if (data.Length < 14) return Fallback(ts, dir, "Display Color", hex);

        var colorNames = new string[8];
        for (int i = 0; i < 8; i++)
        {
            int c = data[6 + i] & 0x07;
            colorNames[i] = ((XTouchColor)c).ToString();
        }

        return new DecodedMidiMessage(ts, dir, "Display Farben",
            "Alle 8 Kanäle", string.Join(", ", colorNames),
            "← SetDisplayColor", hex);
    }

    private static DecodedMidiMessage Fallback(DateTime ts, MidiDirection dir, string type, string hex)
    {
        return new DecodedMidiMessage(ts, dir, type, "?", "Zu kurz / ungültig", "Fehler", hex);
    }
}
