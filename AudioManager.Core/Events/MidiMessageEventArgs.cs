namespace AudioManager.Core.Events;

/// <summary>
/// Event-Daten für rohe MIDI-Nachrichten (Direct Hook).
/// Ermöglicht es, MIDI-Nachrichten abzufangen bevor sie verarbeitet werden.
/// </summary>
public class MidiMessageEventArgs : EventArgs
{
    /// <summary>Rohe MIDI-Bytes.</summary>
    public byte[] Data { get; }

    /// <summary>
    /// Setze auf true um die Nachricht zu konsumieren
    /// (verhindert weitere Verarbeitung).
    /// </summary>
    public bool Handled { get; set; }

    public MidiMessageEventArgs(byte[] data)
    {
        Data = data;
    }
}
