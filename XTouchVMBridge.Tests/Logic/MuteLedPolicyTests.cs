using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Logic;
using XTouchVMBridge.Core.Models;

namespace XTouchVMBridge.Tests.Logic;

public class MuteLedPolicyTests
{
    [Fact]
    public void Resolve_NoSolo_ReturnsMuteState()
    {
        var state = new VoicemeeterState();
        state.Mutes[1] = true;

        var led = MuteLedPolicy.Resolve(1, state, anySoloActive: false);

        Assert.Equal(LedState.On, led);
    }

    [Fact]
    public void Resolve_NonSoloStrip_WhenAnySoloActive_Blinks()
    {
        var state = new VoicemeeterState();
        state.Solos[2] = true;

        var led = MuteLedPolicy.Resolve(1, state, anySoloActive: true);

        Assert.Equal(LedState.Blink, led);
    }

    [Fact]
    public void Resolve_SoloStrip_WhenAnySoloActive_DoesNotBlink()
    {
        var state = new VoicemeeterState();
        state.Solos[2] = true;

        var led = MuteLedPolicy.Resolve(2, state, anySoloActive: true);

        Assert.Equal(LedState.Off, led);
    }

    [Fact]
    public void Resolve_BusChannel_IgnoresSoloMasking()
    {
        var state = new VoicemeeterState();
        state.Solos[2] = true;

        var led = MuteLedPolicy.Resolve(10, state, anySoloActive: true);

        Assert.Equal(LedState.Off, led);
    }
}
