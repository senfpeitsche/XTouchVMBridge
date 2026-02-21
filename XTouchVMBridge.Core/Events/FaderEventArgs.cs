namespace XTouchVMBridge.Core.Events;

public class FaderEventArgs : EventArgs
{
    public int Channel { get; }

    public int Position { get; }

    public double Db { get; }

    public FaderEventArgs(int channel, int position, double db)
    {
        Channel = channel;
        Position = position;
        Db = db;
    }
}
