namespace XTouchVMBridge.Core.Hardware;

public abstract class HardwareControlBase
{
    public int Channel { get; }

    public string ControlId { get; }

    protected HardwareControlBase(int channel, string controlId)
    {
        Channel = channel;
        ControlId = controlId;
    }
}
