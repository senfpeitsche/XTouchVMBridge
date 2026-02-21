namespace XTouchVMBridge.Midi.XTouch;

/// <summary>
/// Protocol constants and message builders for Mackie/Behringer MIDI communication
/// used by X-Touch and X-Touch Extender devices.
/// </summary>
public static class MackieProtocol
{
    // Mackie SysEx headers for main/extended device variants.
    public static readonly byte[] SysExPrefix = { 0xF0, 0x00, 0x00, 0x66, 0x14 };

    public static readonly byte[] SysExPrefixExtended = { 0xF0, 0x00, 0x00, 0x66, 0x15 };

    public const byte SysExEnd = 0xF7;

    // Core display/handshake command IDs.
    public const byte CmdDisplayText = 0x12;

    public const byte CmdDisplayColor = 0x72;

    public const byte CmdHandshake = 0x13;

    public const byte CmdHandshakeResponse = 0x14;

    // Note ranges used by strip buttons and touch states.
    public const int ChannelCount = 8;

    public const int NoteRecBase = 0;

    public const int NoteSoloBase = 8;

    public const int NoteMuteBase = 16;

    public const int NoteSelectBase = 24;

    public const int NoteEncoderPressBase = 32;

    public const int NoteFaderTouchBase = 104;

    // Continuous controller ranges for encoders.
    public const int CcEncoderBase = 16;

    public const int CcEncoderRingBase = 48;

    // LED velocity conventions used by Mackie note messages.
    public const byte VelocityOff = 0;

    public const byte VelocityBlink = 1;

    public const byte VelocityOn = 127;

    // Scribble strip layout: 8 channels, 2 rows, 7 chars each.
    public const int CharsPerChannel = 7;

    public const int TotalDisplayChars = 112;

    public const int SecondRowOffset = 56;

    // Behringer SysEx constants for segment/LCD displays.
    public static readonly byte[] BehringerSysExBase = { 0xF0, 0x00, 0x20, 0x32 };

    public const byte DeviceIdXTouch = 0x14;

    public const byte DeviceIdXTouchExt = 0x15;

    public const byte CmdSegmentDisplay = 0x37;

    public const byte CmdLcdDisplay = 0x4C;

    public const int SegmentDigitCount = 12;

    public const int CcSegmentDisplayBase = 64;

    /// <summary>
    /// Encodes one character for the segment display value stream.
    /// Dot/colon markers are encoded by setting bit 6.
    /// </summary>
    public static byte CharToSegmentCcValue(char c, bool dot = false)
    {
        byte value = (byte)(c & 0x3F);

        if (dot)
            value |= 0x40;

        return value;
    }

    /// <summary>
    /// Converts user text to 12 segment values plus inline dot handling.
    /// </summary>
    public static byte[] TextToSegmentCcValues(string text)
    {
        var values = new byte[SegmentDigitCount];
        var chars = new char[SegmentDigitCount];
        var dots = new bool[SegmentDigitCount];

        for (int i = 0; i < SegmentDigitCount; i++)
            chars[i] = ' ';

        int digitIdx = 0;
        for (int i = 0; i < text.Length && digitIdx < SegmentDigitCount; i++)
        {
            char c = text[i];

            if ((c == '.' || c == ':') && digitIdx > 0)
            {
                dots[digitIdx - 1] = true;
                continue;
            }

            chars[digitIdx] = c;
            digitIdx++;
        }

        for (int i = 0; i < SegmentDigitCount; i++)
            values[i] = CharToSegmentCcValue(chars[i], dots[i]);

        return values;
    }

    /// <summary>
    /// Character-to-segment map for the Behringer segment display.
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
    /// Builds a complete segment SysEx message with digit and dot masks.
    /// </summary>
    public static byte[] BuildSegmentDisplayMessage(byte[] segments, byte dots1, byte dots2, byte deviceId = DeviceIdXTouchExt)
    {
        if (segments.Length != SegmentDigitCount)
            throw new ArgumentException($"Genau {SegmentDigitCount} Segment-Bytes erwartet.", nameof(segments));

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
    /// Converts plain text into the segment payload tuple used by BuildSegmentDisplayMessage.
    /// </summary>
    public static (byte[] segments, byte dots1, byte dots2) TextToSegments(string text)
    {
        var segments = new byte[SegmentDigitCount];
        byte dots1 = 0, dots2 = 0;
        int digitIdx = 0;

        for (int i = 0; i < text.Length && digitIdx < SegmentDigitCount; i++)
        {
            char c = text[i];

            if ((c == '.' || c == ':') && digitIdx > 0)
            {
                int dotDigit = digitIdx; // 1-based (display digit 1 = digitIdx 1)
                if (dotDigit <= 7)
                    dots1 |= (byte)(1 << (dotDigit - 1));
                else if (dotDigit <= 12)
                    dots2 |= (byte)(1 << (dotDigit - 8));
                continue;
            }

            char upper = char.ToUpperInvariant(c);
            if (SegmentFont.TryGetValue(c, out byte seg))
                segments[digitIdx] = seg;
            else if (SegmentFont.TryGetValue(upper, out byte segU))
                segments[digitIdx] = segU;
            else
                segments[digitIdx] = 0x00; // Blank for unknown characters

            digitIdx++;
        }

        return (segments, dots1, dots2);
    }


    /// <summary>
    /// Builds a per-channel LCD scribble message (7 chars top + 7 chars bottom).
    /// </summary>
    public static byte[] BuildLcdMessage(int lcdNumber, string topRow, string bottomRow,
        byte color = 7, bool invertTop = false, bool invertBottom = false,
        byte deviceId = DeviceIdXTouchExt)
    {
        var data = new byte[23];

        BehringerSysExBase.CopyTo(data, 0);  // F0 00 20 32
        data[4] = deviceId;                   // dd
        data[5] = CmdLcdDisplay;              // 4C
        data[6] = (byte)lcdNumber;            // nn

        byte colorByte = (byte)(color & 0x07);
        if (invertTop) colorByte |= 0x10;
        if (invertBottom) colorByte |= 0x20;
        data[7] = colorByte;                  // cc

        string top = topRow.Length >= 7 ? topRow[..7] : topRow.PadRight(7);
        string bottom = bottomRow.Length >= 7 ? bottomRow[..7] : bottomRow.PadRight(7);

        for (int i = 0; i < 7; i++)
        {
            char c = top[i];
            data[8 + i] = (byte)(c is >= ' ' and <= '~' ? c : ' ');
        }

        for (int i = 0; i < 7; i++)
        {
            char c = bottom[i];
            data[15 + i] = (byte)(c is >= ' ' and <= '~' ? c : ' ');
        }

        data[22] = SysExEnd;  // F7
        return data;
    }

    /// <summary>
    /// LCD color IDs accepted by BuildLcdMessage.
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


    // Peak meter level range used by X-Touch.
    public const int MaxLevelMeter = 13;


    /// <summary>
    /// Builds a Mackie global display text write at a specific buffer offset.
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
    /// Builds a display color message for all 8 channel scribbles.
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
    /// Returns linear display buffer offset for a channel/row position.
    /// </summary>
    public static int GetDisplayOffset(int channel, int row)
    {
        return row * SecondRowOffset + channel * CharsPerChannel;
    }

    /// <summary>
    /// Decodes relative encoder tick values from Mackie CC data bytes.
    /// </summary>
    public static int DecodeEncoderTicks(int ccValue)
    {
        if (ccValue is >= 1 and <= 15)
            return ccValue; // Right turn (positive)
        if (ccValue is >= 65 and <= 79)
            return -(ccValue - 64); // Left turn (negative)
        return 0;
    }

    /// <summary>
    /// Generates handshake response bytes from challenge payload.
    /// </summary>
    public static byte[] GenerateResponseCode(byte[] challenge)
    {
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
