namespace XTouchVMBridge.Core.Enums;

/// <summary>
/// Unterscheidung zwischen Input-Strip und Output-Bus in Voicemeeter.
/// </summary>
public enum ChannelType
{
    /// <summary>Voicemeeter Input Strip (Kanal 0–7).</summary>
    Strip,

    /// <summary>Voicemeeter Output Bus (Kanal 8–15).</summary>
    Bus
}
