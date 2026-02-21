namespace XTouchVMBridge.Core.Events;

public class MasterButtonEventArgs : EventArgs
{
    public int NoteNumber { get; }

    public bool IsPressed { get; }

    public MasterButtonEventArgs(int noteNumber, bool isPressed)
    {
        NoteNumber = noteNumber;
        IsPressed = isPressed;
    }
}
