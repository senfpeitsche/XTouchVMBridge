using XTouchVMBridge.Core.Enums;

namespace XTouchVMBridge.Core.Models;

public class ChannelConfig
{
    public string Name { get; set; } = "       ";

    public string Type { get; set; } = string.Empty;

    public XTouchColor Color { get; set; } = XTouchColor.Off;
}
