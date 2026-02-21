using XTouchVMBridge.Core.Enums;

namespace XTouchVMBridge.Core.Models;

public class ChannelViewConfig
{
    public string Name { get; set; } = "";

    public int[] Channels { get; set; } = Array.Empty<int>();

    public XTouchColor?[]? ChannelColors { get; set; }

    public int? MainFaderChannel { get; set; }

    public XTouchColor? GetChannelColor(int stripIndex)
    {
        if (ChannelColors == null || stripIndex < 0 || stripIndex >= ChannelColors.Length)
            return null;
        return ChannelColors[stripIndex];
    }
}
