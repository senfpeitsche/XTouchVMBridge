using AudioManager.Midi.XTouch;

namespace AudioManager.Tests.XTouch;

public class MidiMessageDecoderTests
{
    // ─── Eingehende Nachrichten: Buttons ────────────────────────────

    [Fact]
    public void DecodeIncoming_NoteOn_RecButton_Press()
    {
        // Note On, Note 3 (REC Ch4), Velocity 127
        int raw = 0x90 | (3 << 8) | (127 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal(MidiMessageDecoder.MidiDirection.In, result.Direction);
        Assert.Equal("Button", result.ControlType);
        Assert.Contains("Kanal 4", result.ControlId);
        Assert.Contains("REC", result.ControlId);
        Assert.Contains("Gedrückt", result.Value);
    }

    [Fact]
    public void DecodeIncoming_NoteOn_MuteButton_Release()
    {
        // Note On, Note 18 (MUTE Ch3), Velocity 0 → Release
        int raw = 0x90 | (18 << 8) | (0 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Button", result.ControlType);
        Assert.Contains("Kanal 3", result.ControlId);
        Assert.Contains("MUTE", result.ControlId);
        Assert.Contains("Losgelassen", result.Value);
    }

    [Fact]
    public void DecodeIncoming_NoteOn_SoloButton()
    {
        // Note On, Note 10 (SOLO Ch3), Velocity 127
        int raw = 0x90 | (10 << 8) | (127 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Contains("SOLO", result.ControlId);
        Assert.Contains("Kanal 3", result.ControlId);
    }

    [Fact]
    public void DecodeIncoming_NoteOn_SelectButton()
    {
        // Note On, Note 24 (SELECT Ch1), Velocity 127
        int raw = 0x90 | (24 << 8) | (127 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Contains("SELECT", result.ControlId);
        Assert.Contains("Kanal 1", result.ControlId);
    }

    // ─── Eingehende Nachrichten: Fader ──────────────────────────────

    [Fact]
    public void DecodeIncoming_PitchWheel_Fader()
    {
        // Pitchwheel Ch0: LSB=0, MSB=64 → Position 0
        int raw = 0xE0 | (0 << 8) | (64 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Fader", result.ControlType);
        Assert.Contains("Kanal 1", result.ControlId);
        Assert.Contains("Pos", result.Value);
        Assert.Contains("FaderChanged", result.Action);
    }

    // ─── Eingehende Nachrichten: Fader Touch ────────────────────────

    [Fact]
    public void DecodeIncoming_NoteOn_FaderTouch()
    {
        // Note On, Note 110 (Fader Touch Ch1), Velocity 127
        int raw = 0x90 | (110 << 8) | (127 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Fader Touch", result.ControlType);
        Assert.Contains("Kanal 1", result.ControlId);
        Assert.Contains("Berührt", result.Value);
    }

    [Fact]
    public void DecodeIncoming_NoteOn_FaderTouch_Release()
    {
        // Note On, Note 113 (Fader Touch Ch4), Velocity 0
        int raw = 0x90 | (113 << 8) | (0 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Fader Touch", result.ControlType);
        Assert.Contains("Kanal 4", result.ControlId);
        Assert.Contains("Losgelassen", result.Value);
    }

    // ─── Eingehende Nachrichten: Encoder ────────────────────────────

    [Fact]
    public void DecodeIncoming_CC_Encoder_Increment()
    {
        // CC 82 (Encoder Ch3), Value 65 (increment)
        int raw = 0xB0 | (82 << 8) | (65 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Encoder", result.ControlType);
        Assert.Contains("Kanal 3", result.ControlId);
        Assert.Contains("Inkrement", result.Value);
    }

    [Fact]
    public void DecodeIncoming_CC_Encoder_Decrement()
    {
        // CC 80 (Encoder Ch1), Value 1 (decrement)
        int raw = 0xB0 | (80 << 8) | (1 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Encoder", result.ControlType);
        Assert.Contains("Kanal 1", result.ControlId);
        Assert.Contains("Dekrement", result.Value);
    }

    // ─── Eingehende Nachrichten: Encoder Press ──────────────────────

    [Fact]
    public void DecodeIncoming_NoteOn_EncoderPress()
    {
        // Note On, Note 35 (Encoder Press Ch4), Velocity 127
        int raw = 0x90 | (35 << 8) | (127 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Encoder Press", result.ControlType);
        Assert.Contains("Kanal 4", result.ControlId);
        Assert.Contains("Gedrückt", result.Value);
    }

    // ─── Eingehende Nachrichten: Jog Wheel ──────────────────────────

    [Fact]
    public void DecodeIncoming_CC_JogWheel_CW()
    {
        // CC 88 (Jog Wheel), Value 65 (CW)
        int raw = 0xB0 | (88 << 8) | (65 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Jog Wheel", result.ControlType);
        Assert.Contains("CW", result.Value);
    }

    [Fact]
    public void DecodeIncoming_CC_JogWheel_CCW()
    {
        // CC 88 (Jog Wheel), Value 1 (CCW)
        int raw = 0xB0 | (88 << 8) | (1 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Jog Wheel", result.ControlType);
        Assert.Contains("CCW", result.Value);
    }

    // ─── Eingehende Nachrichten: Foot Switch ────────────────────────

    [Fact]
    public void DecodeIncoming_CC_FootSwitch_FS1()
    {
        // CC 64 (FS1), Value 127 (pushed)
        int raw = 0xB0 | (64 << 8) | (127 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Foot Switch", result.ControlType);
        Assert.Contains("FS1", result.ControlId);
        Assert.Contains("Gedrückt", result.Value);
    }

    [Fact]
    public void DecodeIncoming_CC_FootSwitch_FS2()
    {
        // CC 67 (FS2), Value 0 (released)
        int raw = 0xB0 | (67 << 8) | (0 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Foot Switch", result.ControlType);
        Assert.Contains("FS2", result.ControlId);
        Assert.Contains("Losgelassen", result.Value);
    }

    // ─── Eingehende Nachrichten: Foot Controller ────────────────────

    [Fact]
    public void DecodeIncoming_CC_FootController()
    {
        // CC 4 (Foot Controller), Value 100
        int raw = 0xB0 | (4 << 8) | (100 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Foot Controller", result.ControlType);
        Assert.Contains("FC", result.ControlId);
    }

    // ─── Eingehende Nachrichten: Meter LEDs ─────────────────────────

    [Fact]
    public void DecodeIncoming_CC_MeterLed()
    {
        // CC 93 (Meter Ch4), Value 80
        int raw = 0xB0 | (93 << 8) | (80 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Meter LED", result.ControlType);
        Assert.Contains("Kanal 4", result.ControlId);
    }

    // ─── Eingehende Nachrichten: Fader CC (MIDI-Mode) ───────────────

    [Fact]
    public void DecodeIncoming_CC_Fader_MidiMode()
    {
        // CC 72 (Fader Ch3 MIDI-Mode), Value 100
        int raw = 0xB0 | (72 << 8) | (100 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Fader (MIDI-Mode)", result.ControlType);
        Assert.Contains("Kanal 3", result.ControlId);
    }

    // ─── Ausgehende Nachrichten ─────────────────────────────────────

    [Fact]
    public void DecodeOutgoing_ButtonLed_On()
    {
        // Note On, Note 16 (MUTE Ch1), Velocity 127 (On)
        int raw = 0x90 | (16 << 8) | (127 << 16);
        var result = MidiMessageDecoder.DecodeOutgoing(raw);

        Assert.Equal(MidiMessageDecoder.MidiDirection.Out, result.Direction);
        Assert.Equal("Button LED", result.ControlType);
        Assert.Contains("MUTE", result.ControlId);
        Assert.Contains("LED An", result.Value);
    }

    [Fact]
    public void DecodeOutgoing_ButtonLed_Flash()
    {
        // Note On, Note 8 (SOLO Ch1), Velocity 64 (Flash)
        int raw = 0x90 | (8 << 8) | (64 << 16);
        var result = MidiMessageDecoder.DecodeOutgoing(raw);

        Assert.Contains("Blinken", result.Value);
    }

    [Fact]
    public void DecodeOutgoing_ButtonLed_Off()
    {
        // Note On, Note 0 (REC Ch1), Velocity 0 (Off)
        int raw = 0x90 | (0 << 8) | (0 << 16);
        var result = MidiMessageDecoder.DecodeOutgoing(raw);

        Assert.Contains("LED Aus", result.Value);
    }

    [Fact]
    public void DecodeOutgoing_EncoderRing()
    {
        // CC 50 (Encoder Ring Ch3), Value = Pan mode, pos 7
        int raw = 0xB0 | (50 << 8) | (0x17 << 16); // mode=1 (Pan), pos=7
        var result = MidiMessageDecoder.DecodeOutgoing(raw);

        Assert.Equal("Encoder Ring", result.ControlType);
        Assert.Contains("Kanal 3", result.ControlId);
        Assert.Contains("Pan", result.Value);
    }

    [Fact]
    public void DecodeOutgoing_Fader()
    {
        // Pitchwheel Ch2: position 0 → LSB=0, MSB=64
        int raw = 0xE2 | (0 << 8) | (64 << 16);
        var result = MidiMessageDecoder.DecodeOutgoing(raw);

        Assert.Equal("Fader", result.ControlType);
        Assert.Contains("Kanal 3", result.ControlId);
    }

    [Fact]
    public void DecodeOutgoing_LevelMeter()
    {
        // Aftertouch: Ch3, Level 5 → data = (3 << 4) | 5 = 53
        int raw = 0xD0 | (53 << 8);
        var result = MidiMessageDecoder.DecodeOutgoing(raw);

        Assert.Equal("Level Meter", result.ControlType);
        Assert.Contains("Kanal 4", result.ControlId); // (53>>4)&7 = 3 → Kanal 4 (1-basiert)
        Assert.Contains("Level 5", result.Value);
    }

    // ─── SysEx-Dekodierung ──────────────────────────────────────────

    [Fact]
    public void DecodeSysEx_Lcd_XTouchExt()
    {
        // F0 00 20 32 15 4C 02 03 41 42 43 44 45 46 47 61 62 63 64 65 66 67 F7
        // LCD #2, Color=Yellow(3), 14 chars "ABCDEFGabcdefg"
        byte[] data = {
            0xF0, 0x00, 0x20, 0x32, 0x15, 0x4C, 0x02, 0x03,
            0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47,  // upper: ABCDEFG
            0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67,  // lower: abcdefg
            0xF7
        };
        var result = MidiMessageDecoder.DecodeSysEx(data, MidiMessageDecoder.MidiDirection.Out);

        Assert.Equal("LCD Display", result.ControlType);
        Assert.Contains("X-Touch-Ext", result.ControlId);
        Assert.Contains("LCD #3", result.ControlId); // 0-indexed → +1
        Assert.Contains("Yellow", result.Value);
        Assert.Contains("ABCDEFG", result.Value);
        Assert.Contains("abcdefg", result.Value);
    }

    [Fact]
    public void DecodeSysEx_MackieDisplayText()
    {
        // F0 00 00 66 15 12 00 48 65 6C 6C 6F F7
        // Mackie Display Text at offset 0: "Hello"
        byte[] data = { 0xF0, 0x00, 0x00, 0x66, 0x15, 0x12, 0x00, 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0xF7 };
        var result = MidiMessageDecoder.DecodeSysEx(data, MidiMessageDecoder.MidiDirection.Out);

        Assert.Equal("Display Text", result.ControlType);
        Assert.Contains("Hello", result.Value);
    }

    [Fact]
    public void DecodeSysEx_MackieDisplayColor()
    {
        // F0 00 00 66 15 72 01 02 03 04 05 06 07 00 F7
        byte[] data = { 0xF0, 0x00, 0x00, 0x66, 0x15, 0x72, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x00, 0xF7 };
        var result = MidiMessageDecoder.DecodeSysEx(data, MidiMessageDecoder.MidiDirection.Out);

        Assert.Equal("Display Farben", result.ControlType);
        Assert.Contains("Red", result.Value);
        Assert.Contains("Green", result.Value);
        Assert.Contains("White", result.Value);
    }

    [Fact]
    public void DecodeSysEx_TooShort_ReturnsFallback()
    {
        byte[] data = { 0xF0, 0x00, 0xF7 };
        var result = MidiMessageDecoder.DecodeSysEx(data, MidiMessageDecoder.MidiDirection.In);

        Assert.Equal("SysEx", result.ControlType);
        Assert.Contains("Zu kurz", result.Value);
    }

    // ─── Richtung und Zeitstempel ───────────────────────────────────

    [Fact]
    public void DecodeIncoming_SetsDirectionIn()
    {
        int raw = 0x90 | (0 << 8) | (127 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);
        Assert.Equal(MidiMessageDecoder.MidiDirection.In, result.Direction);
    }

    [Fact]
    public void DecodeOutgoing_SetsDirectionOut()
    {
        int raw = 0x90 | (0 << 8) | (127 << 16);
        var result = MidiMessageDecoder.DecodeOutgoing(raw);
        Assert.Equal(MidiMessageDecoder.MidiDirection.Out, result.Direction);
    }

    [Fact]
    public void DecodeIncoming_UsesProvidedTimestamp()
    {
        var ts = new DateTime(2025, 6, 15, 12, 30, 0);
        int raw = 0x90 | (0 << 8) | (127 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw, ts);
        Assert.Equal(ts, result.Timestamp);
    }

    [Fact]
    public void DecodeIncoming_RawHex_IsFormatted()
    {
        int raw = 0x90 | (0x10 << 8) | (0x7F << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);
        Assert.Equal("90 10 7F", result.RawHex);
    }
}
