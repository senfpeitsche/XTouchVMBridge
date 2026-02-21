using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Events;
using XTouchVMBridge.Core.Hardware;
using Microsoft.Extensions.Logging;
using NAudio.Midi;

namespace XTouchVMBridge.Midi.XTouch;

public partial class XTouchDevice
{
    private void OnMidiMessageReceived(object? sender, MidiInMessageEventArgs e)
    {
        try
        {
            _consecutiveErrors = 0;

            byte status = (byte)(e.MidiEvent.CommandCode);
            int rawMsg = e.RawMessage;
            byte data1 = (byte)((rawMsg >> 8) & 0xFF);
            byte data2 = (byte)((rawMsg >> 16) & 0xFF);
            byte channel = (byte)(e.MidiEvent.Channel - 1); // NAudio ist 1-basiert

            var hookArgs = new MidiMessageEventArgs(new[] { (byte)(rawMsg & 0xFF), data1, data2 });
            RawMidiReceived?.Invoke(this, hookArgs);
            if (hookArgs.Handled) return;

            switch (e.MidiEvent.CommandCode)
            {
                case MidiCommandCode.PitchWheelChange:
                    HandleFader(channel, data1, data2);
                    break;

                case MidiCommandCode.ControlChange:
                    HandleEncoder(data1, data2);
                    break;

                case MidiCommandCode.NoteOn:
                    HandleNoteOn(data1, data2);
                    break;

                case MidiCommandCode.NoteOff:
                    HandleNoteOff(data1);
                    break;

                case MidiCommandCode.ChannelAfterTouch:
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei MIDI-Nachrichtenverarbeitung.");
        }
    }

    private void HandleFader(int channel, byte lsb, byte msb)
    {
        if (channel > MackieProtocol.ChannelCount) return;

        int position = ((msb << 7) | lsb) - 8192;
        double db = FaderControl.PositionToDb(position);

        if (channel < MackieProtocol.ChannelCount)
        {
            _channels[channel].Fader.Position = position;
        }

        FaderChanged?.Invoke(this, new FaderEventArgs(channel, position, db));
    }

    private void HandleEncoder(byte cc, byte value)
    {
        int encoderIndex = cc - MackieProtocol.CcEncoderBase;
        if (encoderIndex is < 0 or >= MackieProtocol.ChannelCount) return;

        int ticks = MackieProtocol.DecodeEncoderTicks(value);
        EncoderRotated?.Invoke(this, new EncoderEventArgs(encoderIndex, ticks));
    }

    private void HandleNoteOn(byte note, byte velocity)
    {
        bool isPressed = velocity > 0;

        _logger.LogDebug("NoteOn empfangen: Note={Note}, Velocity={Velocity}, Pressed={Pressed}", note, velocity, isPressed);

        if (note is >= MackieProtocol.NoteEncoderPressBase and < MackieProtocol.NoteEncoderPressBase + MackieProtocol.ChannelCount)
        {
            int ch = note - MackieProtocol.NoteEncoderPressBase;
            if (isPressed)
            {
                _noteTimers[note] = DateTime.UtcNow;
                _channels[ch].Encoder.IsPressed = true;
            }

            var timePressed = GetTimePressed(note, isPressed);
            if (!isPressed) _channels[ch].Encoder.IsPressed = false;

            EncoderPressed?.Invoke(this, new EncoderPressEventArgs(ch, isPressed, timePressed));
            return;
        }

        if (note is >= MackieProtocol.NoteFaderTouchBase and < MackieProtocol.NoteFaderTouchBase + MackieProtocol.ChannelCount)
        {
            int ch = note - MackieProtocol.NoteFaderTouchBase;
            if (isPressed) _noteTimers[note] = DateTime.UtcNow;

            _channels[ch].Fader.IsTouched = isPressed;
            var timePressed = GetTimePressed(note, isPressed);

            _logger.LogDebug("Fader Touch: Note={Note}, Channel={Ch}, Pressed={Pressed}", note, ch, isPressed);
            FaderTouched?.Invoke(this, new FaderTouchEventArgs(ch, isPressed, timePressed));
            return;
        }

        if (note == MackieProtocol.NoteFaderTouchBase + MackieProtocol.ChannelCount)
        {
            if (isPressed) _noteTimers[note] = DateTime.UtcNow;
            _mainFaderTouched = isPressed;
            var timePressed = GetTimePressed(note, isPressed);

            FaderTouched?.Invoke(this, new FaderTouchEventArgs(8, isPressed, timePressed));
            return;
        }

        if (note < 32)
        {
            int ch = note % MackieProtocol.ChannelCount;
            var buttonType = (XTouchButtonType)(note / MackieProtocol.ChannelCount);

            if (isPressed) _noteTimers[note] = DateTime.UtcNow;

            var btn = _channels[ch].GetButton(buttonType);
            btn.IsPressed = isPressed;
            var timePressed = GetTimePressed(note, isPressed);

            ButtonChanged?.Invoke(this, new ButtonEventArgs(ch, buttonType, isPressed, timePressed));
        }
        else if (note >= 40)
        {
            if (isPressed) _noteTimers[note] = DateTime.UtcNow;
            var timePressed = GetTimePressed(note, isPressed);
            MasterButtonChanged?.Invoke(this, new MasterButtonEventArgs(note, isPressed));
        }
    }

    private void HandleNoteOff(byte note)
    {
        HandleNoteOn(note, 0);
    }

    private TimeSpan GetTimePressed(byte note, bool isPressed)
    {
        if (isPressed || !_noteTimers.TryGetValue(note, out var startTime))
            return TimeSpan.Zero;

        _noteTimers.Remove(note);
        return DateTime.UtcNow - startTime;
    }


    private void OnMidiError(object? sender, MidiInMessageEventArgs e)
    {
        _consecutiveErrors++;
        _logger.LogWarning("MIDI-Fehler empfangen: {Message} (Fehler #{Count})", e.RawMessage, _consecutiveErrors);

        if (_consecutiveErrors >= 3)
        {
            _logger.LogError("Zu viele MIDI-Fehler — Verbindung wird als getrennt markiert.");
            HandleConnectionLost();
        }
    }
}
