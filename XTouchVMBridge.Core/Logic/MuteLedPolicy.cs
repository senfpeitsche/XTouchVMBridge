using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Models;

namespace XTouchVMBridge.Core.Logic;

public static class MuteLedPolicy
{
    public static LedState Resolve(int vmChannel, VoicemeeterState state, bool anySoloActive)
    {
        bool isMuted = vmChannel >= 0 &&
                       vmChannel < state.Mutes.Length &&
                       state.Mutes[vmChannel];

        if (vmChannel >= VoicemeeterState.StripCount || !anySoloActive)
            return isMuted ? LedState.On : LedState.Off;

        bool isSoloStrip = vmChannel >= 0 &&
                           vmChannel < VoicemeeterState.StripCount &&
                           state.Solos[vmChannel];
        if (isSoloStrip)
            return isMuted ? LedState.On : LedState.Off;

        return isMuted ? LedState.On : LedState.Blink;
    }
}
