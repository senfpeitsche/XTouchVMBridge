namespace XTouchVMBridge.Core.Events;

public class MidiMessageEventArgs : EventArgs
{
    public byte[] Data { get; }

    public bool Handled { get; set; }

    public MidiMessageEventArgs(byte[] data)
    {
        Data = data;
    }
}
