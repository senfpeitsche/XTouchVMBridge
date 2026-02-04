namespace XTouchVMBridge.Midi.XTouch;

/// <summary>
/// Konstanten und Hilfsmethoden für das Mackie Control Extended Protokoll.
/// Zentralisiert alle "Magic Numbers" aus dem Python-Original.
/// </summary>
public static class MackieProtocol
{
    // ─── SysEx Framing ──────────────────────────────────────────────

    /// <summary>SysEx-Prefix für Mackie Control Unit (MCU Main).</summary>
    public static readonly byte[] SysExPrefix = { 0xF0, 0x00, 0x00, 0x66, 0x14 };

    /// <summary>SysEx-Prefix für Mackie Control Unit Extended (MCU Ext).</summary>
    public static readonly byte[] SysExPrefixExtended = { 0xF0, 0x00, 0x00, 0x66, 0x15 };

    /// <summary>SysEx-Suffix.</summary>
    public const byte SysExEnd = 0xF7;

    // ─── SysEx Command Bytes ────────────────────────────────────────

    /// <summary>Display-Text schreiben (gefolgt von Offset + ASCII-Daten).</summary>
    public const byte CmdDisplayText = 0x12;

    /// <summary>Display-Farben setzen (gefolgt von 8 Farb-Bytes).</summary>
    public const byte CmdDisplayColor = 0x72;

    /// <summary>Handshake/Challenge-Response.</summary>
    public const byte CmdHandshake = 0x13;

    /// <summary>Handshake-Antwort.</summary>
    public const byte CmdHandshakeResponse = 0x14;

    // ─── MIDI Channel/Note Mappings ────────────────────────────────

    /// <summary>Kanäle 0–7: Anzahl der physischen Kanäle.</summary>
    public const int ChannelCount = 8;

    /// <summary>MIDI Notes für REC-Buttons (0–7).</summary>
    public const int NoteRecBase = 0;

    /// <summary>MIDI Notes für SOLO-Buttons (8–15).</summary>
    public const int NoteSoloBase = 8;

    /// <summary>MIDI Notes für MUTE-Buttons (16–23).</summary>
    public const int NoteMuteBase = 16;

    /// <summary>MIDI Notes für SELECT-Buttons (24–31).</summary>
    public const int NoteSelectBase = 24;

    /// <summary>MIDI Notes für Encoder-Press (32–39).</summary>
    public const int NoteEncoderPressBase = 32;

    /// <summary>MIDI Notes für Fader-Touch (104–111 für Extender, Main=112).</summary>
    public const int NoteFaderTouchBase = 104;

    // ─── Control Change Mappings ────────────────────────────────────

    /// <summary>CC-Nummern für Encoder-Rotation (16–23).</summary>
    public const int CcEncoderBase = 16;

    /// <summary>CC-Nummern für Encoder-Ring-Anzeige (48–55).</summary>
    public const int CcEncoderRingBase = 48;

    // ─── MIDI Velocity Constants ────────────────────────────────────

    /// <summary>LED aus.</summary>
    public const byte VelocityOff = 0;

    /// <summary>LED blinkt.</summary>
    public const byte VelocityBlink = 1;

    /// <summary>LED an.</summary>
    public const byte VelocityOn = 127;

    // ─── Display Constants ──────────────────────────────────────────

    /// <summary>Maximale Zeichen pro Kanal-Zeile.</summary>
    public const int CharsPerChannel = 7;

    /// <summary>Gesamte Display-Zeichenlänge (8 Kanäle × 7 Zeichen × 2 Zeilen).</summary>
    public const int TotalDisplayChars = 112;

    /// <summary>Offset für die zweite Zeile im Display-Buffer.</summary>
    public const int SecondRowOffset = 56;

    // ─── Segment Display ──────────────────────────────────────────────

    /// <summary>Behringer SysEx-Prefix für Segment Display (ohne Device-ID).</summary>
    public static readonly byte[] BehringerSysExBase = { 0xF0, 0x00, 0x20, 0x32 };

    /// <summary>Device-ID für X-Touch (nicht Extender).</summary>
    public const byte DeviceIdXTouch = 0x14;

    /// <summary>Device-ID für X-Touch Extender.</summary>
    public const byte DeviceIdXTouchExt = 0x15;

    /// <summary>SysEx-Kommando für Segment-Display.</summary>
    public const byte CmdSegmentDisplay = 0x37;

    /// <summary>SysEx-Kommando für LCD-Display (Behringer X-Touch spezifisch).</summary>
    public const byte CmdLcdDisplay = 0x4C;

    /// <summary>Anzahl der 7-Segment-Digits.</summary>
    public const int SegmentDigitCount = 12;

    /// <summary>Basis-CC für 7-Segment-Display (Mackie Control Protocol).</summary>
    public const int CcSegmentDisplayBase = 64;

    /// <summary>
    /// Mackie Control 7-Segment Display.
    /// Das X-Touch im MCU-Modus zeigt ASCII-Zeichen direkt an.
    /// CC-Wert = ASCII-Code des Zeichens (0-127).
    /// Bit 6 (0x40) = Dezimalpunkt aktiv.
    /// </summary>
    public static byte CharToSegmentCcValue(char c, bool dot = false)
    {
        // Direkt ASCII-Code verwenden, auf 0-63 begrenzen (Bit 6 für Dot reserviert)
        byte value = (byte)(c & 0x3F);

        // Bit 6 = Dezimalpunkt
        if (dot)
            value |= 0x40;

        return value;
    }

    /// <summary>
    /// Konvertiert einen String in CC-Werte für das 7-Segment-Display.
    /// Punkte im String werden als Dot-Bit auf dem vorherigen Zeichen gesetzt.
    /// Gibt Array von 12 CC-Werten zurück (für CC 64-75).
    /// </summary>
    public static byte[] TextToSegmentCcValues(string text)
    {
        var values = new byte[SegmentDigitCount];
        var chars = new char[SegmentDigitCount];
        var dots = new bool[SegmentDigitCount];

        // Initialisiere mit Leerzeichen
        for (int i = 0; i < SegmentDigitCount; i++)
            chars[i] = ' ';

        int digitIdx = 0;
        for (int i = 0; i < text.Length && digitIdx < SegmentDigitCount; i++)
        {
            char c = text[i];

            // Punkt/Doppelpunkt als Dot auf vorheriges Digit
            if ((c == '.' || c == ':') && digitIdx > 0)
            {
                dots[digitIdx - 1] = true;
                continue;
            }

            chars[digitIdx] = c;
            digitIdx++;
        }

        // Konvertiere zu CC-Werten
        for (int i = 0; i < SegmentDigitCount; i++)
            values[i] = CharToSegmentCcValue(chars[i], dots[i]);

        return values;
    }

    /// <summary>
    /// 7-Segment-Font: Mappt Zeichen auf Segment-Bitmuster (für Behringer SysEx).
    /// Bit 0=a(oben), 1=b(rechts oben), 2=c(rechts unten), 3=d(unten),
    /// 4=e(links unten), 5=f(links oben), 6=g(mitte).
    /// </summary>
    public static readonly Dictionary<char, byte> SegmentFont = new()
    {
        ['0'] = 0x3F, ['1'] = 0x06, ['2'] = 0x5B, ['3'] = 0x4F,
        ['4'] = 0x66, ['5'] = 0x6D, ['6'] = 0x7D, ['7'] = 0x07,
        ['8'] = 0x7F, ['9'] = 0x6F,
        ['A'] = 0x77, ['b'] = 0x7C, ['C'] = 0x39, ['c'] = 0x58,
        ['d'] = 0x5E, ['E'] = 0x79, ['F'] = 0x71,
        ['H'] = 0x76, ['h'] = 0x74, ['I'] = 0x06, ['J'] = 0x1E,
        ['L'] = 0x38, ['n'] = 0x54, ['o'] = 0x5C, ['P'] = 0x73,
        ['r'] = 0x50, ['S'] = 0x6D, ['t'] = 0x78, ['U'] = 0x3E,
        ['u'] = 0x1C, ['Y'] = 0x6E, ['-'] = 0x40, ['_'] = 0x08,
        [' '] = 0x00, ['°'] = 0x63,
    };

    /// <summary>
    /// Baut eine SysEx-Nachricht für das 7-Segment-Display (Behringer-Format, nicht für X-Touch im MCU-Modus).
    /// </summary>
    /// <param name="segments">12 Segment-Bytes (links nach rechts).</param>
    /// <param name="dots1">Dot-Bits für Display 1–7.</param>
    /// <param name="dots2">Dot-Bits für Display 8–12.</param>
    /// <param name="deviceId">Device-ID (0x14=X-Touch, 0x15=Ext).</param>
    public static byte[] BuildSegmentDisplayMessage(byte[] segments, byte dots1, byte dots2, byte deviceId = DeviceIdXTouchExt)
    {
        if (segments.Length != SegmentDigitCount)
            throw new ArgumentException($"Genau {SegmentDigitCount} Segment-Bytes erwartet.", nameof(segments));

        // F0 00 20 32 dd 37 s1..s12 d1 d2 F7  => 4 + 1 + 1 + 12 + 2 + 1 = 21
        var data = new byte[21];
        BehringerSysExBase.CopyTo(data, 0);
        data[4] = deviceId;
        data[5] = CmdSegmentDisplay;

        for (int i = 0; i < SegmentDigitCount; i++)
            data[6 + i] = segments[i];

        data[18] = dots1;
        data[19] = dots2;
        data[20] = SysExEnd;
        return data;
    }

    /// <summary>
    /// Konvertiert einen String in 7-Segment-Bytes + Dot-Bytes (Behringer SysEx-Format).
    /// Punkte im String werden als Dot auf dem vorherigen Digit gesetzt.
    /// </summary>
    public static (byte[] segments, byte dots1, byte dots2) TextToSegments(string text)
    {
        var segments = new byte[SegmentDigitCount];
        byte dots1 = 0, dots2 = 0;
        int digitIdx = 0;

        for (int i = 0; i < text.Length && digitIdx < SegmentDigitCount; i++)
        {
            char c = text[i];

            // Punkt/Doppelpunkt als Dot auf vorheriges Digit
            if ((c == '.' || c == ':') && digitIdx > 0)
            {
                int dotDigit = digitIdx; // 1-basiert (Display 1 = digitIdx 1)
                if (dotDigit <= 7)
                    dots1 |= (byte)(1 << (dotDigit - 1));
                else if (dotDigit <= 12)
                    dots2 |= (byte)(1 << (dotDigit - 8));
                continue;
            }

            // Zeichen in Segment-Font nachschlagen
            char upper = char.ToUpperInvariant(c);
            if (SegmentFont.TryGetValue(c, out byte seg))
                segments[digitIdx] = seg;
            else if (SegmentFont.TryGetValue(upper, out byte segU))
                segments[digitIdx] = segU;
            else
                segments[digitIdx] = 0x00; // Leer bei unbekannten Zeichen

            digitIdx++;
        }

        return (segments, dots1, dots2);
    }

    // ─── LCD Display (Behringer X-Touch spezifisch) ─────────────────

    /// <summary>
    /// Baut eine SysEx-Nachricht für ein einzelnes LCD-Display (Behringer X-Touch Format).
    /// Format: F0 00 20 32 dd 4C nn cc c1..c14 F7
    /// </summary>
    /// <param name="lcdNumber">LCD Nummer 0-7</param>
    /// <param name="topRow">Text für obere Zeile (max 7 Zeichen)</param>
    /// <param name="bottomRow">Text für untere Zeile (max 7 Zeichen)</param>
    /// <param name="color">Hintergrundfarbe (0-7: black, red, green, yellow, blue, magenta, cyan, white)</param>
    /// <param name="invertTop">Obere Hälfte invertieren</param>
    /// <param name="invertBottom">Untere Hälfte invertieren</param>
    /// <param name="deviceId">Device-ID (0x14=X-Touch, 0x15=Ext)</param>
    public static byte[] BuildLcdMessage(int lcdNumber, string topRow, string bottomRow,
        byte color = 7, bool invertTop = false, bool invertBottom = false,
        byte deviceId = DeviceIdXTouchExt)
    {
        // F0 00 20 32 dd 4C nn cc c1..c14 F7 = 4 + 1 + 1 + 1 + 1 + 14 + 1 = 23 bytes
        var data = new byte[23];

        // Behringer SysEx Header
        BehringerSysExBase.CopyTo(data, 0);  // F0 00 20 32
        data[4] = deviceId;                   // dd
        data[5] = CmdLcdDisplay;              // 4C
        data[6] = (byte)lcdNumber;            // nn

        // Color + Invert flags
        byte colorByte = (byte)(color & 0x07);
        if (invertTop) colorByte |= 0x10;
        if (invertBottom) colorByte |= 0x20;
        data[7] = colorByte;                  // cc

        // Pad strings to 7 characters
        string top = topRow.Length >= 7 ? topRow[..7] : topRow.PadRight(7);
        string bottom = bottomRow.Length >= 7 ? bottomRow[..7] : bottomRow.PadRight(7);

        // c1..c7: upper half
        for (int i = 0; i < 7; i++)
        {
            char c = top[i];
            data[8 + i] = (byte)(c is >= ' ' and <= '~' ? c : ' ');
        }

        // c8..c14: lower half
        for (int i = 0; i < 7; i++)
        {
            char c = bottom[i];
            data[15 + i] = (byte)(c is >= ' ' and <= '~' ? c : ' ');
        }

        data[22] = SysExEnd;  // F7
        return data;
    }

    /// <summary>
    /// LCD-Farben für X-Touch Display.
    /// </summary>
    public static class LcdColor
    {
        public const byte Black = 0;
        public const byte Red = 1;
        public const byte Green = 2;
        public const byte Yellow = 3;
        public const byte Blue = 4;
        public const byte Magenta = 5;
        public const byte Cyan = 6;
        public const byte White = 7;
    }

    // ─── Level Meter ────────────────────────────────────────────────

    /// <summary>Maximaler Level-Meter-Wert.</summary>
    public const int MaxLevelMeter = 13;

    // ─── Hilfsmethoden ──────────────────────────────────────────────

    /// <summary>
    /// Baut eine SysEx-Nachricht für Display-Text.
    /// </summary>
    public static byte[] BuildDisplayTextMessage(int offset, string text)
    {
        var data = new byte[SysExPrefix.Length + 2 + text.Length + 1];
        SysExPrefix.CopyTo(data, 0);
        data[SysExPrefix.Length] = CmdDisplayText;
        data[SysExPrefix.Length + 1] = (byte)offset;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            data[SysExPrefix.Length + 2 + i] = (byte)(c is >= ' ' and <= '~' ? c : ' ');
        }

        data[^1] = SysExEnd;
        return data;
    }

    /// <summary>
    /// Baut eine SysEx-Nachricht für Display-Farben (alle 8 Kanäle).
    /// </summary>
    public static byte[] BuildDisplayColorMessage(byte[] colors)
    {
        if (colors.Length != ChannelCount)
            throw new ArgumentException($"Genau {ChannelCount} Farben erwartet.", nameof(colors));

        var data = new byte[SysExPrefix.Length + 1 + ChannelCount + 1];
        SysExPrefix.CopyTo(data, 0);
        data[SysExPrefix.Length] = CmdDisplayColor;

        for (int i = 0; i < ChannelCount; i++)
            data[SysExPrefix.Length + 1 + i] = colors[i];

        data[^1] = SysExEnd;
        return data;
    }

    /// <summary>
    /// Berechnet den Display-Buffer-Offset für einen Kanal und eine Zeile.
    /// </summary>
    public static int GetDisplayOffset(int channel, int row)
    {
        return row * SecondRowOffset + channel * CharsPerChannel;
    }

    /// <summary>
    /// Dekodiert eine Encoder-Rotation aus einem CC-Wert.
    /// CC-Werte: 1–15 = Rechtsdrehung, 65–79 = Linksdrehung.
    /// </summary>
    public static int DecodeEncoderTicks(int ccValue)
    {
        if (ccValue is >= 1 and <= 15)
            return ccValue; // Rechtsdrehung (positiv)
        if (ccValue is >= 65 and <= 79)
            return -(ccValue - 64); // Linksdrehung (negativ)
        return 0;
    }

    /// <summary>
    /// Erzeugt den Mackie-Handshake-Response-Code.
    /// </summary>
    public static byte[] GenerateResponseCode(byte[] challenge)
    {
        // Mackie Handshake Challenge-Response Algorithmus
        if (challenge.Length < 4) return Array.Empty<byte>();

        return new byte[]
        {
            (byte)(0x7E & (challenge[0] + (challenge[1] ^ 0x0A) - challenge[3])),
            (byte)(0x7E & ((challenge[2] >> 4) ^ (challenge[0] + challenge[3]))),
            (byte)(0x7E & (challenge[3] - (challenge[2] << 2) ^ (challenge[0] | challenge[1]))),
            (byte)(0x7E & (challenge[1] - challenge[3] + (0xF0 ^ (challenge[2] << 4))))
        };
    }
}
