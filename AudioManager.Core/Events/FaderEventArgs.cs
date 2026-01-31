namespace AudioManager.Core.Events;

/// <summary>
/// Event-Daten wenn ein Fader bewegt wird.
/// </summary>
public class FaderEventArgs : EventArgs
{
    /// <summary>Kanal-Index (0–7).</summary>
    public int Channel { get; }

    /// <summary>Raw-Position (-8192 bis +8188).</summary>
    public int Position { get; }

    /// <summary>Position in dB (-70 bis +8).</summary>
    public double Db { get; }

    public FaderEventArgs(int channel, int position, double db)
    {
        Channel = channel;
        Position = position;
        Db = db;
    }
}
