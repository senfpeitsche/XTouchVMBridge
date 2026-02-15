[English](VOICEMEETER-API.md) | [Deutsch](VOICEMEETER-API-DE.md)

# Voicemeeter Remote API -- Parameter reference

Complete list of all Voicemeeter parameters available via the native `VoicemeeterRemote64.dll`
can be addressed via P/Invoke. Based on **Voicemeeter Potato** (8 strips + 8 buses).

## Access in C# project

All parameters are addressed via generic string-based getters/setters:
```csharp
// Lesen
VoicemeeterRemote.GetParameterFloat("Strip[0].Gain", out float val);
VoicemeeterRemote.GetParameterStringA("Strip[0].Label", out string label);

// Schreiben
VoicemeeterRemote.SetParameterFloat("Strip[0].Gain", -6.0f);
VoicemeeterRemote.SetParameterStringA("Strip[0].Label", "MyMic");
```
Defined in: `XTouchVMBridge.Voicemeeter/Native/VoicemeeterRemote.cs`
Encapsulated in: `XTouchVMBridge.Voicemeeter/Services/VoicemeeterService.cs` (`IVoicemeeterService`)

---

## Channel Structure (Potato)

| Index | Type | Voicemeeter name | Prefix |
|---|---|---|---|
| 0 | Physical Strip | Hardware input 1 | `Strip[0]` |
| 1 | Physical Strip | Hardware input 2 | `Strip[1]` |
| 2 | Physical Strip | Hardware input 3 | `Strip[2]` |
| 3 | Physical Strip | Hardware input 4 | `Strip[3]` |
| 4 | Physical Strip | Hardware input 5 | `Strip[4]` |
| 5 | Virtual Strip | Virtual Input 1 | `Strip[5]` |
| 6 | Virtual Strip | Virtual Input 2 | `Strip[6]` |
| 7 | Virtual Strip | Virtual Input 3 | `Strip[7]` |
| 8 | Physical Bus | Bus A1 | `Bus[0]` |
| 9 | Physical Bus | Bus A2 | `Bus[1]` |
| 10 | Physical Bus | Bus A3 | `Bus[2]` |
| 11 | Physical Bus | Bus A4 | `Bus[3]` |
| 12 | Physical Bus | Bus A5 | `Bus[4]` |
| 13 | VirtualBus | Bus B1 | `Bus[5]` |
| 14 | VirtualBus | Bus B2 | `Bus[6]` |
| 15 | VirtualBus | Bus B3 | `Bus[7]` |

In the C# project we use 0-15 internally, where `VoicemeeterService.IsStrip(ch)`
checks whether `ch < 8` (strip) or `ch >= 8` (bus with index `ch - 8`).

---

## Currently implemented parameters

These parameters can already be used in the C# project via `IVoicemeeterService` or `EncoderFunction`.

| VM parameters | C# method / usage | X-Touch Element | File:Line |
|---|---|---|---|
| `Strip[N].Gain` | `SetGain()` / EncoderFunction "GAIN" | Faders/encoders | `VoicemeeterService.cs:101` |
| `Bus[N].Gain` | `SetGain()` | Faders | `VoicemeeterService.cs:103` |
| `Strip[N].Mute` | `SetMute()` | MUTE button | `VoicemeeterService.cs:111` |
| `Bus[N].Mute` | `SetMute()` | MUTE button | `VoicemeeterService.cs:112` |
| `Strip[N].Solo` | `SetSolo()` | SOLO button | `VoicemeeterService.cs:125` |
| `Strip[N].EQGain1` | EncoderFunction "LOW" | encoders | `VoicemeeterBridge.cs:97` |
| `Strip[N].EQGain2` | EncoderFunction "MID" | encoders | `VoicemeeterBridge.cs:95` |
| `Strip[N].EQGain3` | EncoderFunction "HIGH" | encoders | `VoicemeeterBridge.cs:94` |
| `Strip[N].Pan_x` | EncoderFunction "PAN" | encoders | `VoicemeeterBridge.cs:97` |
| Level Type 1 (Strip PostFader) | `GetLevel()` | Level meter | `VoicemeeterService.cs:85` |
| Level Type 3 (Bus Output) | `GetLevel()` | Level meter | `VoicemeeterService.cs:93` |
| `Command.Restart` | `Restart()` | -- | `VoicemeeterService.cs:72` |
| `Command.Show` | `ShowVoicemeeter()` | Master button | `VoicemeeterService.cs:175` |
| `Command.Lock` | `LockGui(bool)` | Master button (toggle) | `VoicemeeterService.cs:181` |
| `MacroButton[N]` (Mode 2) | `TriggerMacroButton(int)` | Master button | `VoicemeeterService.cs:187` |

---

## Parameters not yet implemented

All of the following parameters can be used immediately via the existing P/Invoke methods,
without changing the native layer. All you have to do is `IVoicemeeterService`, `VoicemeeterState`
and/or the `EncoderFunction` registry can be extended to `VoicemeeterBridge`.

### Strip parameters (input channels 0-7)

#### Basic parameters

| VM parameters | Type | Value range | Recommended X-Touch Element | Note |
|---|---|---|---|---|
| `Strip[N].Mono` | float (bool) | 0 / 1 | REC or SELECT button | Mono circuit |
| `Strip[N].Pan_y` | float | 0.0 -- 1.0 | encoders | Vertical panning |
| `Strip[N].Color_x` | float | -0.5 -- 0.5 | encoders | Stereo enhancer horizontal |
| `Strip[N].Color_y` | float | 0.0 -- 1.0 | encoders | Stereo enhancer vertical |
| `Strip[N].Audibility` | float | 0.0 -- 10.0 | encoders | Intelligibility Enhancer |
| `Strip[N].Limit` | float | -40 -- 12 | encoders | Limiter threshold in dB |
| `Strip[N].MC` | float (bool) | 0 / 1 | Button | Mix down to Mono |
| `Strip[N].K` | float | 0 -- 4 | Encoder/Button | Karaoke mode |
| `Strip[N].Label` | string | max. 7 characters | Display (already via Config) | Channel name |

#### Bus routing (A1-A5, B1-B3)

Determines which output buses a strip is sent to. High priority for X-Touch.

| VM parameters | Type | X-Touch Element | Note |
|---|---|---|---|
| `Strip[N].A1` | float (bool) | Button (REC/SELECT) | Routing to bus A1 |
| `Strip[N].A2` | float (bool) | Button (REC/SELECT) | Routing to bus A2 |
| `Strip[N].A3` | float (bool) | Button (REC/SELECT) | Routing to bus A3 |
| `Strip[N].A4` | float (bool) | Button (REC/SELECT) | Routing to bus A4 |
| `Strip[N].A5` | float (bool) | Button (REC/SELECT) | Routing to bus A5 |
| `Strip[N].B1` | float (bool) | Button (REC/SELECT) | Routing to bus B1 |
| `Strip[N].B2` | float (bool) | Button (REC/SELECT) | Routing to bus B2 |
| `Strip[N].B3` | float (bool) | Button (REC/SELECT) | Routing to bus B3 |

Implementation idea: Use REC buttons in "routing mode" where SELECT has a
Select channel and switch the REC buttons A1-A5/B1-B3 (8 routing destinations on 8 buttons).

#### FX sends

| VM parameters | Type | Value range | X-Touch Element |
|---|---|---|---|
| `Strip[N].Reverb` | float | 0.0 -- 10.0 | encoders |
| `Strip[N].Delay` | float | 0.0 -- 10.0 | encoders |
| `Strip[N].Fx1` | float | 0.0 -- 10.0 | encoders |
| `Strip[N].Fx2` | float | 0.0 -- 10.0 | encoders |
| `Strip[N].PostReverb` | float (bool) | 0 / 1 | Button | Pre/Post Fader |
| `Strip[N].PostDelay` | float (bool) | 0 / 1 | Button | Pre/Post Fader |
| `Strip[N].PostFx1` | float (bool) | 0 / 1 | Button | Pre/Post Fader |
| `Strip[N].PostFx2` | float (bool) | 0 / 1 | Button | Pre/Post Fader |

#### Spatial Controls

| VM parameters | Type | Value range | X-Touch Element |
|---|---|---|---|
| `Strip[N].fx_x` | float | -0.5 -- 0.5 | encoders |
| `Strip[N].fx_y` | float | 0.0 -- 1.0 | encoders |

### Strip Sub-Objects (Physical Strips 0-4 only, Potato only)

#### Gate

| VM parameters | Type | Value range | X-Touch Element |
|---|---|---|---|
| `Strip[N].Gate` | float | 0.0 -- 10.0 | Encoder (Knob mode) |
| `Strip[N].Gate.Threshold` | float | -60 -- 0 dB | encoders |
| `Strip[N].Gate.Damping` | float | -60 -- 0 dB | encoders |
| `Strip[N].Gate.BPSidechain` | float | 100 -- 4000 Hz | encoders |
| `Strip[N].Gate.Attack` | float | 0.1 -- 1000 ms | encoders |
| `Strip[N].Gate.Hold` | float | 0 -- 5000 ms | encoders |
| `Strip[N].Gate.Release` | float | 0.1 -- 5000 ms | encoders |

Implementation idea: Register as an EncoderFunction group:
```csharp
new EncoderFunction("GATE",  $"Strip[{vmCh}].Gate",               0, 10, 0.5, ""),
new EncoderFunction("G.THR", $"Strip[{vmCh}].Gate.Threshold",    -60,  0, 1.0, "dB"),
new EncoderFunction("G.DMP", $"Strip[{vmCh}].Gate.Damping",      -60,  0, 1.0, "dB"),
new EncoderFunction("G.ATK", $"Strip[{vmCh}].Gate.Attack",       0.1, 1000, 10, "ms"),
new EncoderFunction("G.HLD", $"Strip[{vmCh}].Gate.Hold",           0, 5000, 50, "ms"),
new EncoderFunction("G.RLS", $"Strip[{vmCh}].Gate.Release",      0.1, 5000, 50, "ms"),
```
#### Compressor

| VM parameters | Type | Value range | X-Touch Element |
|---|---|---|---|
| `Strip[N].Comp` | float | 0.0 -- 10.0 | Encoder (Knob mode) |
| `Strip[N].Comp.GainIn` | float | -24 -- 24 dB | encoders |
| `Strip[N].Comp.Threshold` | float | -40 -- 0 dB | encoders |
| `Strip[N].Comp.Ratio` | float | 1.0 -- 8.0 | encoders |
| `Strip[N].Comp.Attack` | float | 0.1 -- 200 ms | encoders |
| `Strip[N].Comp.Release` | float | 0.1 -- 5000 ms | encoders |
| `Strip[N].Comp.Knee` | float | 0 -- 1.0 | encoders |
| `Strip[N].Comp.GainOut` | float | -24 -- 24 dB | encoders |
| `Strip[N].Comp.MakeUp` | float (bool) | 0 / 1 | Button |

Implementation idea:
```csharp
new EncoderFunction("COMP",  $"Strip[{vmCh}].Comp",              0, 10, 0.5, ""),
new EncoderFunction("C.THR", $"Strip[{vmCh}].Comp.Threshold",  -40,  0, 1.0, "dB"),
new EncoderFunction("C.RAT", $"Strip[{vmCh}].Comp.Ratio",      1.0, 8.0, 0.5, ""),
new EncoderFunction("C.ATK", $"Strip[{vmCh}].Comp.Attack",     0.1, 200, 5.0, "ms"),
new EncoderFunction("C.RLS", $"Strip[{vmCh}].Comp.Release",    0.1, 5000, 50, "ms"),
new EncoderFunction("C.GIN", $"Strip[{vmCh}].Comp.GainIn",     -24, 24, 0.5, "dB"),
new EncoderFunction("C.GOT", $"Strip[{vmCh}].Comp.GainOut",    -24, 24, 0.5, "dB"),
```
#### Denoiser

| VM parameters | Type | Value range | X-Touch Element |
|---|---|---|---|
| `Strip[N].Denoiser` | float | 0.0 -- 10.0 | encoders |

Implementation: `new EncoderFunction("DENOI", $"Strip[{vmCh}].Denoiser", 0, 10, 0.5, "")`

#### EQ

| VM parameters | Type | Value range | X-Touch Element |
|---|---|---|---|
| `Strip[N].EQ.on` | float (bool) | 0 / 1 | Button (REC) |
| `Strip[N].EQ.AB` | float (bool) | 0 / 1 | Button (SELECT) |
| `Strip[N].EQGain1` | float | -12 -- +12 dB | **Already implemented** (LOW) |
| `Strip[N].EQGain2` | float | -12 -- +12 dB | **Already implemented** (MID) |
| `Strip[N].EQGain3` | float | -12 -- +12 dB | **Already implemented** (HIGH) |

#### Gainlayers (Potato only)

Gainlayers allow per-bus volume control per strip (8x8 matrix).

| VM parameters | Type | Value range | X-Touch Element |
|---|---|---|---|
| `Strip[N].GainLayer[0]` | float | -60 -- +12 dB | Faders |
| `Strip[N].GainLayer[1]` | float | -60 -- +12 dB | Faders |
| ... | ... | ... | ... |
| `Strip[N].GainLayer[7]` | float | -60 -- +12 dB | Faders |

Implementation idea: Own channel view "Gainlayer" with the 8 gainlayers per strip
maps to the 8 faders.

### Bus parameters (output channels, bus[0]-bus[7])

#### Basic parameters

| VM parameters | Type | Value range | X-Touch Element | Note |
|---|---|---|---|---|
| `Bus[N].Mono` | float (bool) | 0 / 1 | Button | Mono circuit |
| `Bus[N].Sel` | float (bool) | 0 / 1 | SELECT button | Bus selection (for SEL mode) |
| `Bus[N].Label` | string | max. 7 characters | Display | Bus name |

#### EQ

| VM parameters | Type | Value range | X-Touch Element |
|---|---|---|---|
| `Bus[N].EQ.on` | float (bool) | 0 / 1 | REC button |
| `Bus[N].EQ.AB` | float (bool) | 0 / 1 | SELECT button |

#### FX returns

| VM parameters | Type | Value range | X-Touch Element |
|---|---|---|---|
| `Bus[N].ReturnReverb` | float | 0.0 -- 10.0 | encoders |
| `Bus[N].ReturnDelay` | float | 0.0 -- 10.0 | encoders |
| `Bus[N].ReturnFx1` | float | 0.0 -- 10.0 | encoders |
| `Bus[N].ReturnFx2` | float | 0.0 -- 10.0 | encoders |

#### Monitor

| VM parameters | Type | Value range | X-Touch Element |
|---|---|---|---|
| `Bus[N].Monitor` | float (bool) | 0 / 1 | Button |

#### Bus Modes (exclusive -- only one can be active)

| VM parameters | Description |
|---|---|
| `Bus[N].mode.normal` | Standard mode |
| `Bus[N].mode.Amix` | A Mix |
| `Bus[N].mode.Bmix` | B Mix |
| `Bus[N].mode.Composite` | Composite |
| `Bus[N].mode.TVMix` | TV mix |
| `Bus[N].mode.UpMix21` | Upmix 2.1 |
| `Bus[N].mode.UpMix41` | Upmix 4.1 |
| `Bus[N].mode.UpMix61` | Upmix 6.1 |
| `Bus[N].mode.CenterOnly` | Center only |
| `Bus[N].mode.LFEOnly` | LFE only |
| `Bus[N].mode.RearOnly` | Only Rear |

Implementation idea: Cycle via encoder or as an EncoderFunction group.

### Special methods / utility parameters

| VM parameters | Type | Description |
|---|---|---|
| `Strip[N].FadeTo` | Method (via Set) | Gentle thread to target value |
| `Strip[N].FadeBy` | Method (via Set) | Relative thread |
| `Bus[N].FadeTo` | Method (via Set) | Gentle thread to target value |
| `Bus[N].FadeBy` | Method (via Set) | Relative thread |
| `Strip[N].AppGain` | Method | App-specific volume control |
| `Strip[N].AppMute` | Method | App-specific muting |
| `Command.Restart` | float (trigger) | Restart Voicemeeter | **Implemented** (`Restart()`) |
| `Command.Shutdown` | float (trigger) | Exit Voicemeeter | Not yet implemented |
| `Command.Show` | float (trigger) | Window in the foreground | **Implemented** (`ShowVoicemeeter()`) |
| `Command.Lock` | float (bool) | Lock/unlock GUI | **Implemented** (`LockGui(bool)`) |

### Macro Buttons

P/Invoke in `VoicemeeterRemote.cs`, encapsulated in `IVoicemeeterService.TriggerMacroButton(int)`:
```csharp
VoicemeeterRemote.MacroButtonGetStatus(int buttonIndex, out float value, int mode);
VoicemeeterRemote.MacroButtonSetStatus(int buttonIndex, float value, int mode);
```
| Fashion | Description |
|---|---|
| 1 | State (state: 0=off, 1=on) |
| 2 | State + Trigger (like physical click) — **Implemented** |
| 3 | Display (display LED) |

**Implemented**: Master buttons can be configured as macro button triggers
(Action type `TriggerMacroButton` with index 0-79 in the mapping editor).
Additionally possible: Map REC buttons to macro buttons 0-7.

### Dirty Flags / Polling

| Method | C# Access | Description |
|---|---|---|
| `VBVMR_IsParametersDirty` | `VoicemeeterRemote.IsParametersDirty()` | Returns 1 if parameters have changed |
| Level polling | `VoicemeeterRemote.GetLevel(type, index, out val)` | Levels are polled directly (no dirty flag) |

Level types for `GetLevel(type, ...)`:

| Type | Description | Index formula |
|---|---|---|
| 0 | Strip Pre-Fader Input | `strip * 2 + lr` |
| 1 | Strip Post Fader Input | `strip * 2 + lr` |
| 2 | Strip Post Mute Input | `strip * 2 + lr` |
| 3 | Bus Output | `bus * 8 + channel` (up to 7.1) |

---

## X-Touch capacity vs. used parameters

### Overview of free control elements

| X-Touch Element | Available | Used | Free | Possible usage |
|---|---|---|---|---|
| Faders (8x) | 8 | 8 (Gain) | 0 | -- |
| MUTE button (8x) | 8 | 8 (Mute) | 0 | -- |
| SOLO button (8x) | 8 | 8 (Solo) | 0 | -- |
| **REC button (8x)** | 8 | **0** | **8** | EQ.on, Routing, Macro |
| **SELECT button (8x)** | 8 | **0** | **8** | Mono, EQ.AB, channel selection |
| Encoder (8x) | 8 | 8 (2 Nav + 6 EQ) | expandable via cycle function | Comp, Gate, Denoiser, FX |
| Encoder functions | ~5 per encoder | 5 (HIGH/MID/LOW/PAN/GAIN) | Can be expanded as required | +Comp, Gate, Denoiser, Reverb |
| Display (16 fields) | 16 | 16 | dynamically usable | Parameter values ​​|
| Level meter (8x) | 8 | 8 | 0 | -- |

### Implementation priority

**High** (can be used immediately, great added value):
- Bus routing `Strip[N].A1`-`A5`, `B1`-`B3` on REC/SELECT buttons
- `Strip[N].Denoiser` as EncoderFunction
- `Strip[N].EQ.on` on REC button

**Medium** (useful, moderate effort):
- `Strip[N].Comp` / `Strip[N].Gate` as EncoderFunction groups
- `Bus[N].EQ.on` on REC button (in the outputs view)
- FX sends (Reverb, Delay, Fx1, Fx2) as encoder functions
- Macro buttons on REC buttons

**Low** (niche / rarely needed):
- Bus modes (normal, Amix, TVMix, etc.)
- Gainlayers
- Spatial Controls (Color_x, Color_y, fx_x, fx_y)
- Karaoke mode (`Strip[N].K`)

---

## Extension: step-by-step

### Add new float parameter as EncoderFunction

1. In `VoicemeeterBridge.RegisterEncoderFunctions()`:
```csharp
encoder.AddFunctions(new[]
{
    // ... bestehende Funktionen ...
    new EncoderFunction("DENOI", $"Strip[{vmCh}].Denoiser", 0, 10, 0.5, ""),
    new EncoderFunction("COMP",  $"Strip[{vmCh}].Comp",     0, 10, 0.5, ""),
    new EncoderFunction("GATE",  $"Strip[{vmCh}].Gate",     0, 10, 0.5, ""),
});
```
No further changes necessary -- the encoder callback writes to `OnEncoderRotated`
the value automatically via `VoicemeeterRemote.SetParameterFloat()`.

### Map new bool parameter to button

1. Expand `IVoicemeeterService`:
```csharp
void SetParameter(string paramName, float value);
float GetParameter(string paramName);
```
2. Expand `VoicemeeterState`:
```csharp
public bool[] EqOn { get; } = new bool[VoicemeeterState.TotalChannels];
public bool[,] Routing { get; } = new bool[StripCount, 8]; // A1-A5, B1-B3
```
3. In `VoicemeeterBridge.OnButtonChanged()`:
```csharp
case XTouchButtonType.Rec:
    // EQ ein/aus toggeln
    string eqParam = _vm.IsStrip(vmCh)
        ? $"Strip[{vmCh}].EQ.on"
        : $"Bus[{vmCh - 8}].EQ.on";
    float current = _vm.GetParameter(eqParam);
    _vm.SetParameter(eqParam, current > 0.5f ? 0f : 1f);
    break;
```
4. Synchronize the LED in `UpdateParameters()`:
```csharp
// REC-Button LED = EQ.on Status
_xtouch.SetButtonLed(xtCh, XTouchButtonType.Rec,
    _vmState.EqOn[vmCh] ? LedState.On : LedState.Off);
```
---

## Sources

- [Voicemeeter Remote API (PDF)](https://download.vb-audio.com/Download_CABLE/VoicemeeterRemoteAPI.pdf)
- [Voicemeeter Potato User Manual (PDF)](https://vb-audio.com/Voicemeeter/VoicemeeterPotato_UserManual.pdf)
- [voicemeeter-api-python (GitHub)](https://github.com/onyx-and-iris/voicemeeter-api-python)
- [voicemeeter-api (PyPI)](https://pypi.org/project/voicemeeter-api/)
