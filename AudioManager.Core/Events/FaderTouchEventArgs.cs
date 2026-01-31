namespace AudioManager.Core.Events;

/// <summary>
/// Event-Daten wenn ein Fader berührt oder losgelassen wird.
/// </summary>
public class FaderTouchEventArgs : EventArgs
{
    /// <summary>Kanal-Index (0–7).</summary>
    public int Channel { get; }

    /// <summary>True = berührt, False = losgelassen.</summary>
    public bool IsTouched { get; }

    /// <summary>Wie lange der Fader berührt war (nur bei Release).</summary>
    public TimeSpan TimePressed { get; }

    public FaderTouchEventArgs(int channel, bool isTouched, TimeSpan timePressed = default)
    {
        Channel = channel;
        IsTouched = isTouched;
        TimePressed = timePressed;
    }
}
