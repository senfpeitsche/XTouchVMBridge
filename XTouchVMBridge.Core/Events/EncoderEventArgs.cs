namespace XTouchVMBridge.Core.Events;

/// <summary>
/// Event-Daten wenn ein Encoder gedreht wird.
/// </summary>
public class EncoderEventArgs : EventArgs
{
    /// <summary>Kanal-Index (0–7).</summary>
    public int Channel { get; }

    /// <summary>Anzahl Ticks (positiv = rechts, negativ = links).</summary>
    public int Ticks { get; }

    public EncoderEventArgs(int channel, int ticks)
    {
        Channel = channel;
        Ticks = ticks;
    }
}
