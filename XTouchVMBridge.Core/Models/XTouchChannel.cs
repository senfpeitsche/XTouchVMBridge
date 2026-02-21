using XTouchVMBridge.Core.Hardware;

namespace XTouchVMBridge.Core.Models;

public class XTouchChannel
{
    public int Index { get; }

    public DisplayControl Display { get; }

    public FaderControl Fader { get; }

    public EncoderControl Encoder { get; }

    public LevelMeterControl LevelMeter { get; }

    public IReadOnlyDictionary<Enums.XTouchButtonType, ButtonControl> Buttons { get; }

    public XTouchChannel(int index)
    {
        Index = index;
        Display = new DisplayControl(index);
        Fader = new FaderControl(index);
        Encoder = new EncoderControl(index);
        LevelMeter = new LevelMeterControl(index);

        var buttons = new Dictionary<Enums.XTouchButtonType, ButtonControl>();
        foreach (var buttonType in Enum.GetValues<Enums.XTouchButtonType>())
        {
            buttons[buttonType] = new ButtonControl(index, buttonType);
        }
        Buttons = buttons;
    }

    public ButtonControl GetButton(Enums.XTouchButtonType type) => Buttons[type];
}
