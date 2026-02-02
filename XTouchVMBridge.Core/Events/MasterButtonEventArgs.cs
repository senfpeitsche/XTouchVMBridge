namespace XTouchVMBridge.Core.Events;

/// <summary>
/// Event-Daten wenn ein Master-Section-Button gedrückt oder losgelassen wird.
/// Master-Buttons sind alle Buttons außerhalb der 8 Kanalstreifen (MIDI Notes 40–95).
/// </summary>
public class MasterButtonEventArgs : EventArgs
{
    /// <summary>MIDI Note Number des Buttons (40–95).</summary>
    public int NoteNumber { get; }

    /// <summary>True = gedrückt, False = losgelassen.</summary>
    public bool IsPressed { get; }

    public MasterButtonEventArgs(int noteNumber, bool isPressed)
    {
        NoteNumber = noteNumber;
        IsPressed = isPressed;
    }
}
