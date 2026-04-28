namespace XTouchVMBridge.Core.Models;

public enum LedFeedbackMode
{
    Blink,

    Toggle,

    Blinking
}

public enum MasterVmLedSource
{
    ManualFeedback,

    VoicemeeterState
}
public enum MasterButtonActionType
{
    None,

    VmParameter,

    LaunchProgram,

    SendKeys,

    SendText,

    CycleChannelView,

    RestartAudioEngine,

    ShowVoicemeeter,

    LockGui,

    TriggerMacroButton,
    MqttPublish,
    SelectMqttDevice,
    MqttTransport
}

public class MasterButtonActionConfig
{
    public MasterButtonActionType ActionType { get; set; } = MasterButtonActionType.None;

    public string? VmParameter { get; set; }
    public MasterVmLedSource VmLedSource { get; set; } = MasterVmLedSource.ManualFeedback;

    public string? ProgramPath { get; set; }

    public string? ProgramArgs { get; set; }

    public bool KeepLedOnWhileProgramRuns { get; set; }

    public string? KeyCombination { get; set; }

    public string? Text { get; set; }

    public int? MacroButtonIndex { get; set; }

    public string? MqttTopic { get; set; }
    public string? MqttPayloadPressed { get; set; }
    public string? MqttPayloadReleased { get; set; }
    public int MqttQos { get; set; } = 0;
    public bool MqttRetain { get; set; } = false;
    public string? MqttDeviceId { get; set; }
    public string? MqttDeviceCommandTopic { get; set; }
    public string? MqttTransportCommand { get; set; }

    public bool MqttLedEnabled { get; set; } = false;
    public string? MqttLedTopic { get; set; }
    public string? MqttLedPayloadOn { get; set; }
    public string? MqttLedPayloadOff { get; set; }
    public string? MqttLedPayloadBlink { get; set; }
    public string? MqttLedPayloadToggle { get; set; }

    public LedFeedbackMode LedFeedback { get; set; } = LedFeedbackMode.Blink;
}

