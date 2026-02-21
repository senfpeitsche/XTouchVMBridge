namespace XTouchVMBridge.Core.Models;

public class ControlMappingConfig
{
    public FaderMappingConfig? Fader { get; set; }

    public Dictionary<string, ButtonMappingConfig?> Buttons { get; set; } = new();

    public List<EncoderFunctionConfig> EncoderFunctions { get; set; } = new();
}

public class FaderMappingConfig
{
    public string Parameter { get; set; } = "";

    public double Min { get; set; } = -60;

    public double Max { get; set; } = 12;

    public double Step { get; set; } = 0.1;
}

public class ButtonMappingConfig
{
    public const string ChannelRecordActionParameter = "__xtvm_record_channel__";

    public string Parameter { get; set; } = "";

    public ButtonActionType ActionType { get; set; } = ButtonActionType.VmParameter;

    public MqttButtonPublishConfig? MqttPublish { get; set; }

    public MqttButtonLedReceiveConfig? MqttLedReceive { get; set; }
}

public enum ButtonActionType
{
    VmParameter = 0,
    MqttPublish = 1
}

public class MqttButtonPublishConfig
{
    public string Topic { get; set; } = "";
    public string PayloadPressed { get; set; } = "on";
    public string PayloadReleased { get; set; } = "";
    public int Qos { get; set; } = 0;
    public bool Retain { get; set; } = false;
}

public class MqttButtonLedReceiveConfig
{
    public bool Enabled { get; set; } = false;
    public string Topic { get; set; } = "";
    public string PayloadOn { get; set; } = "on";
    public string PayloadOff { get; set; } = "off";
    public string PayloadBlink { get; set; } = "blink";
    public string PayloadToggle { get; set; } = "toggle";
    public bool IgnoreCase { get; set; } = true;
}

public class EncoderFunctionConfig
{
    public string Label { get; set; } = "";

    public string Parameter { get; set; } = "";

    public double Min { get; set; }

    public double Max { get; set; }

    public double Step { get; set; } = 0.5;

    public string Unit { get; set; } = "dB";
}
