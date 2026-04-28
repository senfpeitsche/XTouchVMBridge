using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Events;
using XTouchVMBridge.Core.Models;

namespace XTouchVMBridge.Core.Interfaces;

public interface IMidiDevice : IDisposable
{
    bool IsConnected { get; }

    int ChannelCount { get; }

    IReadOnlyList<XTouchChannel> Channels { get; }

    IReadOnlyDictionary<int, LedState> MasterButtonLedStates { get; }

    string? SelectedDeviceName { get; set; }

    bool IsMainFaderTouched { get; }


    event EventHandler<FaderEventArgs>? FaderChanged;

    event EventHandler<EncoderEventArgs>? EncoderRotated;

    event EventHandler<EncoderPressEventArgs>? EncoderPressed;

    event EventHandler<ButtonEventArgs>? ButtonChanged;

    event EventHandler<FaderTouchEventArgs>? FaderTouched;

    event EventHandler<MidiMessageEventArgs>? RawMidiReceived;

    event EventHandler<MasterButtonEventArgs>? MasterButtonChanged;


    void SetFader(int channel, int position);

    void SetFaderDb(int channel, double db);

    void SetButtonLed(int channel, XTouchButtonType button, LedState state);

    void SetMasterButtonLed(int noteNumber, LedState state);

    void SetEncoderRing(int channel, int value, XTouchEncoderRingMode mode, bool led = false);

    void SetLevelMeter(int channel, int level);

    void SetDisplayText(int channel, int row, string text);

    void SetDisplayColor(int channel, XTouchColor color);

    void SetAllDisplayColors(XTouchColor[] colors);

    void SetSegmentDisplay(string text);

    Task ConnectAsync(CancellationToken cancellationToken = default);

    void Disconnect();

    IReadOnlyList<string> ListDevices();

    bool IsDeviceStillPresent();

    event EventHandler<bool>? ConnectionStateChanged;
}
