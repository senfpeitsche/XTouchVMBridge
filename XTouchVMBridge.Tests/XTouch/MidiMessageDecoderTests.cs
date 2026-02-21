using XTouchVMBridge.Midi.XTouch;

namespace XTouchVMBridge.Tests.XTouch;

public class MidiMessageDecoderTests
{

    [Fact]
    public void DecodeIncoming_NoteOn_RecButton_Press()
    {
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
        int raw = 0x90 | (10 << 8) | (127 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Contains("SOLO", result.ControlId);
        Assert.Contains("Kanal 3", result.ControlId);
    }

    [Fact]
    public void DecodeIncoming_NoteOn_SelectButton()
    {
        int raw = 0x90 | (24 << 8) | (127 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Contains("SELECT", result.ControlId);
        Assert.Contains("Kanal 1", result.ControlId);
    }


    [Fact]
    public void DecodeIncoming_PitchWheel_Fader()
    {
        int raw = 0xE0 | (0 << 8) | (64 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Fader", result.ControlType);
        Assert.Contains("Kanal 1", result.ControlId);
        Assert.Contains("Pos", result.Value);
        Assert.Contains("FaderChanged", result.Action);
    }


    [Fact]
    public void DecodeIncoming_NoteOn_FaderTouch()
    {
        int raw = 0x90 | (110 << 8) | (127 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Fader Touch", result.ControlType);
        Assert.Contains("Kanal 7", result.ControlId);
        Assert.Contains("Berührt", result.Value);
    }

    [Fact]
    public void DecodeIncoming_NoteOn_FaderTouch_Release()
    {
        int raw = 0x90 | (113 << 8) | (0 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Note On", result.ControlType);
        Assert.Contains("Note #113", result.ControlId);
        Assert.Contains("Velocity 0", result.Value);
    }


    [Fact]
    public void DecodeIncoming_CC_Encoder_Increment()
    {
        int raw = 0xB0 | (82 << 8) | (65 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Encoder", result.ControlType);
        Assert.Contains("Kanal 3", result.ControlId);
        Assert.Contains("Inkrement", result.Value);
    }

    [Fact]
    public void DecodeIncoming_CC_Encoder_Decrement()
    {
        int raw = 0xB0 | (80 << 8) | (1 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Encoder", result.ControlType);
        Assert.Contains("Kanal 1", result.ControlId);
        Assert.Contains("Dekrement", result.Value);
    }


    [Fact]
    public void DecodeIncoming_NoteOn_EncoderPress()
    {
        int raw = 0x90 | (35 << 8) | (127 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Encoder Press", result.ControlType);
        Assert.Contains("Kanal 4", result.ControlId);
        Assert.Contains("Gedrückt", result.Value);
    }


    [Fact]
    public void DecodeIncoming_CC_JogWheel_CW()
    {
        int raw = 0xB0 | (88 << 8) | (65 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Jog Wheel", result.ControlType);
        Assert.Contains("CW", result.Value);
    }

    [Fact]
    public void DecodeIncoming_CC_JogWheel_CCW()
    {
        int raw = 0xB0 | (88 << 8) | (1 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Jog Wheel", result.ControlType);
        Assert.Contains("CCW", result.Value);
    }


    [Fact]
    public void DecodeIncoming_CC_FootSwitch_FS1()
    {
        int raw = 0xB0 | (64 << 8) | (127 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Foot Switch", result.ControlType);
        Assert.Contains("FS1", result.ControlId);
        Assert.Contains("Gedrückt", result.Value);
    }

    [Fact]
    public void DecodeIncoming_CC_FootSwitch_FS2()
    {
        int raw = 0xB0 | (67 << 8) | (0 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Foot Switch", result.ControlType);
        Assert.Contains("FS2", result.ControlId);
        Assert.Contains("Losgelassen", result.Value);
    }


    [Fact]
    public void DecodeIncoming_CC_FootController()
    {
        int raw = 0xB0 | (4 << 8) | (100 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Foot Controller", result.ControlType);
        Assert.Contains("FC", result.ControlId);
    }


    [Fact]
    public void DecodeIncoming_CC_MeterLed()
    {
        int raw = 0xB0 | (93 << 8) | (80 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Meter LED", result.ControlType);
        Assert.Contains("Kanal 4", result.ControlId);
    }


    [Fact]
    public void DecodeIncoming_CC_Fader_MidiMode()
    {
        int raw = 0xB0 | (72 << 8) | (100 << 16);
        var result = MidiMessageDecoder.DecodeIncoming(raw);

        Assert.Equal("Fader (MIDI-Mode)", result.ControlType);
        Assert.Contains("Kanal 3", result.ControlId);
    }


    [Fact]
    public void DecodeOutgoing_ButtonLed_On()
    {
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
        int raw = 0x90 | (8 << 8) | (64 << 16);
        var result = MidiMessageDecoder.DecodeOutgoing(raw);

        Assert.Contains("Blinken", result.Value);
    }

    [Fact]
    public void DecodeOutgoing_ButtonLed_Off()
    {
        int raw = 0x90 | (0 << 8) | (0 << 16);
        var result = MidiMessageDecoder.DecodeOutgoing(raw);

        Assert.Contains("LED Aus", result.Value);
    }

    [Fact]
    public void DecodeOutgoing_EncoderRing()
    {
        int raw = 0xB0 | (50 << 8) | (0x17 << 16); // mode=1 (Pan), pos=7
        var result = MidiMessageDecoder.DecodeOutgoing(raw);

        Assert.Equal("Encoder Ring", result.ControlType);
        Assert.Contains("Kanal 3", result.ControlId);
        Assert.Contains("Pan", result.Value);
    }

    [Fact]
    public void DecodeOutgoing_Fader()
    {
        int raw = 0xE2 | (0 << 8) | (64 << 16);
        var result = MidiMessageDecoder.DecodeOutgoing(raw);

        Assert.Equal("Fader", result.ControlType);
        Assert.Contains("Kanal 3", result.ControlId);
    }

    [Fact]
    public void DecodeOutgoing_LevelMeter()
    {
        int raw = 0xD0 | (53 << 8);
        var result = MidiMessageDecoder.DecodeOutgoing(raw);

        Assert.Equal("Level Meter", result.ControlType);
        Assert.Contains("Kanal 4", result.ControlId); // (53>>4)&7 = 3 → Kanal 4 (1-basiert)
        Assert.Contains("Level 5", result.Value);
    }


    [Fact]
    public void DecodeSysEx_Lcd_XTouchExt()
    {
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
        byte[] data = { 0xF0, 0x00, 0x00, 0x66, 0x15, 0x12, 0x00, 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0xF7 };
        var result = MidiMessageDecoder.DecodeSysEx(data, MidiMessageDecoder.MidiDirection.Out);

        Assert.Equal("Display Text", result.ControlType);
        Assert.Contains("Hello", result.Value);
    }

    [Fact]
    public void DecodeSysEx_MackieDisplayColor()
    {
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
