namespace XTouchVMBridge.Core.Events;

public class EncoderEventArgs : EventArgs
{
    public int Channel { get; }

    public int Ticks { get; }

    public EncoderEventArgs(int channel, int ticks)
    {
        Channel = channel;
        Ticks = ticks;
    }
}
