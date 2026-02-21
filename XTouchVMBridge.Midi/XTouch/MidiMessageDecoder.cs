using XTouchVMBridge.Core.Enums;

namespace XTouchVMBridge.Midi.XTouch;

/// <summary>
/// Human-readable decoder for incoming/outgoing X-Touch MIDI messages.
/// </summary>
public static partial class MidiMessageDecoder
{
    public record DecodedMidiMessage(
        DateTime Timestamp,
        MidiDirection Direction,
        string ControlType,
        string ControlId,
        string Value,
        string Action,
        string RawHex
    );

    public enum MidiDirection { In, Out }


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

    public static DecodedMidiMessage DecodeSysEx(byte[] data, MidiDirection direction, DateTime? timestamp = null)
    {
        var ts = timestamp ?? DateTime.Now;
        string hex = string.Join(" ", data.Select(b => $"{b:X2}"));

        if (data.Length < 7)
            return new DecodedMidiMessage(ts, direction, "SysEx", "Unbekannt", "Zu kurz", "Ignoriert", hex);

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


    private static DecodedMidiMessage DecodeNoteOn(DateTime ts, byte note, byte velocity, string hex)
    {
        bool isPressed = velocity > 0;

        if (note is >= 104 and <= 112)
        {
            int ch = note - 104;
            string channelName = ch == 8 ? "Main" : $"Kanal {ch + 1}";
            return new DecodedMidiMessage(ts, MidiDirection.In, "Fader Touch",
                channelName, isPressed ? "Berührt" : "Losgelassen",
                isPressed ? "→ FaderTouched" : "→ FaderTouched (release)", hex);
        }

        if (note is >= 32 and <= 39)
        {
            int ch = note - 32;
            return new DecodedMidiMessage(ts, MidiDirection.In, "Encoder Press",
                $"Kanal {ch + 1}", isPressed ? "Gedrückt" : $"Losgelassen",
                isPressed ? "→ EncoderPressed" : "→ EncoderPressed (release)", hex);
        }

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
        if (cc is >= 70 and <= 78)
        {
            int ch = cc - 70;
            return new DecodedMidiMessage(ts, MidiDirection.In, "Fader (MIDI-Mode)",
                $"Kanal {ch + 1}", $"Wert {value}",
                "→ Fader CC (MIDI-Mode)", hex);
        }

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

        if (cc == 88)
        {
            string dir = value == 65 ? "CW (rechts)" : value == 1 ? "CCW (links)" : $"Wert {value}";
            return new DecodedMidiMessage(ts, MidiDirection.In, "Jog Wheel",
                "Global", dir, "→ JogWheel (nicht implementiert)", hex);
        }

        if (cc is >= 90 and <= 97)
        {
            int ch = cc - 90;
            return new DecodedMidiMessage(ts, MidiDirection.In, "Meter LED",
                $"Kanal {ch + 1}", $"Level {value}/127",
                "→ Meter Update", hex);
        }

        if (cc == 4)
        {
            return new DecodedMidiMessage(ts, MidiDirection.In, "Foot Controller",
                "FC", $"Wert {value}/127",
                "→ FootController (nicht implementiert)", hex);
        }

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
}
