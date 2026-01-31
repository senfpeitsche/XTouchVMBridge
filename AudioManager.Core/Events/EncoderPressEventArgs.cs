namespace AudioManager.Core.Events;

/// <summary>
/// Event-Daten wenn ein Encoder gedrückt oder losgelassen wird.
/// </summary>
public class EncoderPressEventArgs : EventArgs
{
    /// <summary>Kanal-Index (0–7).</summary>
    public int Channel { get; }

    /// <summary>True = gedrückt, False = losgelassen.</summary>
    public bool IsPressed { get; }

    /// <summary>Wie lange der Encoder gedrückt war (nur bei Release).</summary>
    public TimeSpan TimePressed { get; }

    public EncoderPressEventArgs(int channel, bool isPressed, TimeSpan timePressed = default)
    {
        Channel = channel;
        IsPressed = isPressed;
        TimePressed = timePressed;
    }
}
