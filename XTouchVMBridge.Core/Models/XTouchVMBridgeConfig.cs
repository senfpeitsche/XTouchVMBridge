namespace XTouchVMBridge.Core.Models;

public class XTouchVMBridgeConfig
{
    public const string SectionName = "XTouchVMBridge";
    public int ConfigVersion { get; set; }

    public Dictionary<int, ChannelConfig> Channels { get; set; } = new();

    public string VoicemeeterApiType { get; set; } = "potato";

    public string? VoicemeeterDllPath { get; set; }

    public int DeviceMonitorIntervalMs { get; set; } = 5000;

    public bool EnableXTouch { get; set; } = true;

    public Dictionary<int, ControlMappingConfig> Mappings { get; set; } = new();

    public List<ChannelViewConfig> ChannelViews { get; set; } = new();

    public Dictionary<int, MasterButtonActionConfig> MasterButtonActions { get; set; } = new();

    public int SegmentDisplayCycleButton { get; set; } = 52;

    public MqttConfig Mqtt { get; set; } = new();
}
