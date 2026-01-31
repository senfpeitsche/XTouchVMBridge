namespace AudioManager.Midi.XTouch;

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
