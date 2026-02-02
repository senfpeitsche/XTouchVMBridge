namespace XTouchVMBridge.Midi.XTouch;

/// <summary>
/// Konstanten und Hilfsmethoden für das Mackie Control Extended Protokoll.
/// Zentralisiert alle "Magic Numbers" aus dem Python-Original.
/// </summary>
public static class MackieProtocol
{
    // ─── SysEx Framing ──────────────────────────────────────────────

    /// <summary>SysEx-Prefix für Mackie X-Touch Extended.</summary>
    public static readonly byte[] SysExPrefix = { 0xF0, 0x00, 0x00, 0x66, 0x15 };

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

    /// <summary>MIDI Notes für Fader-Touch (104–111).</summary>
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

    /// <summary>Anzahl der 7-Segment-Digits.</summary>
    public const int SegmentDigitCount = 12;

    /// <summary>
    /// 7-Segment-Font: Mappt Zeichen auf Segment-Bitmuster.
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
    /// Baut eine SysEx-Nachricht für das 7-Segment-Display.
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
    /// Konvertiert einen String in 7-Segment-Bytes + Dot-Bytes.
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
