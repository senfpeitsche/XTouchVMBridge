using XTouchVMBridge.Core.Enums;

namespace XTouchVMBridge.Midi.XTouch;

/// <summary>
/// SysEx-Dekodierung: LCD Display, Segment Display, Mackie Display Text/Color.
/// </summary>
public static partial class MidiMessageDecoder
{
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
