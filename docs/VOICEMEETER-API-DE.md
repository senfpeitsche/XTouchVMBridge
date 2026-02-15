[English](VOICEMEETER-API.md) | [Deutsch](VOICEMEETER-API-DE.md)

# Voicemeeter Remote API -- Parameter-Referenz

Vollstandige Liste aller Voicemeeter-Parameter, die uber die native `VoicemeeterRemote64.dll`
via P/Invoke angesprochen werden konnen. Bezogen auf **Voicemeeter Potato** (8 Strips + 8 Buses).

## Zugriff im C#-Projekt

Alle Parameter werden uber generische String-basierte Getter/Setter angesprochen:

```csharp
// Lesen
VoicemeeterRemote.GetParameterFloat("Strip[0].Gain", out float val);
VoicemeeterRemote.GetParameterStringA("Strip[0].Label", out string label);

// Schreiben
VoicemeeterRemote.SetParameterFloat("Strip[0].Gain", -6.0f);
VoicemeeterRemote.SetParameterStringA("Strip[0].Label", "MyMic");
```

Definiert in: `XTouchVMBridge.Voicemeeter/Native/VoicemeeterRemote.cs`
Gekapselt in: `XTouchVMBridge.Voicemeeter/Services/VoicemeeterService.cs` (`IVoicemeeterService`)

---

## Kanal-Struktur (Potato)

| Index | Typ | Voicemeeter-Name | Prefix |
|---|---|---|---|
| 0 | Physical Strip | Hardware Input 1 | `Strip[0]` |
| 1 | Physical Strip | Hardware Input 2 | `Strip[1]` |
| 2 | Physical Strip | Hardware Input 3 | `Strip[2]` |
| 3 | Physical Strip | Hardware Input 4 | `Strip[3]` |
| 4 | Physical Strip | Hardware Input 5 | `Strip[4]` |
| 5 | Virtual Strip | Virtual Input 1 | `Strip[5]` |
| 6 | Virtual Strip | Virtual Input 2 | `Strip[6]` |
| 7 | Virtual Strip | Virtual Input 3 | `Strip[7]` |
| 8 | Physical Bus | Bus A1 | `Bus[0]` |
| 9 | Physical Bus | Bus A2 | `Bus[1]` |
| 10 | Physical Bus | Bus A3 | `Bus[2]` |
| 11 | Physical Bus | Bus A4 | `Bus[3]` |
| 12 | Physical Bus | Bus A5 | `Bus[4]` |
| 13 | Virtual Bus | Bus B1 | `Bus[5]` |
| 14 | Virtual Bus | Bus B2 | `Bus[6]` |
| 15 | Virtual Bus | Bus B3 | `Bus[7]` |

Im C#-Projekt verwenden wir intern durchgehend 0-15, wobei `VoicemeeterService.IsStrip(ch)`
pruft ob `ch < 8` (Strip) oder `ch >= 8` (Bus mit Index `ch - 8`).

---

## Aktuell implementierte Parameter

Diese Parameter sind bereits im C#-Projekt uber `IVoicemeeterService` oder `EncoderFunction` nutzbar.

| VM-Parameter | C#-Methode / Nutzung | X-Touch Element | Datei:Zeile |
|---|---|---|---|
| `Strip[N].Gain` | `SetGain()` / EncoderFunction "GAIN" | Fader / Encoder | `VoicemeeterService.cs:101` |
| `Bus[N].Gain` | `SetGain()` | Fader | `VoicemeeterService.cs:103` |
| `Strip[N].Mute` | `SetMute()` | MUTE-Button | `VoicemeeterService.cs:111` |
| `Bus[N].Mute` | `SetMute()` | MUTE-Button | `VoicemeeterService.cs:112` |
| `Strip[N].Solo` | `SetSolo()` | SOLO-Button | `VoicemeeterService.cs:125` |
| `Strip[N].EQGain1` | EncoderFunction "LOW" | Encoder | `VoicemeeterBridge.cs:97` |
| `Strip[N].EQGain2` | EncoderFunction "MID" | Encoder | `VoicemeeterBridge.cs:95` |
| `Strip[N].EQGain3` | EncoderFunction "HIGH" | Encoder | `VoicemeeterBridge.cs:94` |
| `Strip[N].Pan_x` | EncoderFunction "PAN" | Encoder | `VoicemeeterBridge.cs:97` |
| Level Type 1 (Strip PostFader) | `GetLevel()` | Level-Meter | `VoicemeeterService.cs:85` |
| Level Type 3 (Bus Output) | `GetLevel()` | Level-Meter | `VoicemeeterService.cs:93` |
| `Command.Restart` | `Restart()` | -- | `VoicemeeterService.cs:72` |
| `Command.Show` | `ShowVoicemeeter()` | Master-Button | `VoicemeeterService.cs:175` |
| `Command.Lock` | `LockGui(bool)` | Master-Button (Toggle) | `VoicemeeterService.cs:181` |
| `MacroButton[N]` (Mode 2) | `TriggerMacroButton(int)` | Master-Button | `VoicemeeterService.cs:187` |

---

## Noch nicht implementierte Parameter

Alle folgenden Parameter konnen sofort uber die bestehenden P/Invoke-Methoden genutzt werden,
ohne die Native-Schicht zu andern. Es muss lediglich `IVoicemeeterService`, `VoicemeeterState`
und/oder die `EncoderFunction`-Registrierung in `VoicemeeterBridge` erweitert werden.

### Strip Parameter (Input-Kanale 0-7)

#### Basis-Parameter

| VM-Parameter | Typ | Wertebereich | Empfohlenes X-Touch Element | Anmerkung |
|---|---|---|---|---|
| `Strip[N].Mono` | float (bool) | 0 / 1 | REC- oder SELECT-Button | Mono-Schaltung |
| `Strip[N].Pan_y` | float | 0.0 -- 1.0 | Encoder | Vertikales Panning |
| `Strip[N].Color_x` | float | -0.5 -- 0.5 | Encoder | Stereo-Enhancer horizontal |
| `Strip[N].Color_y` | float | 0.0 -- 1.0 | Encoder | Stereo-Enhancer vertikal |
| `Strip[N].Audibility` | float | 0.0 -- 10.0 | Encoder | Intelligibility Enhancer |
| `Strip[N].Limit` | float | -40 -- 12 | Encoder | Limiter-Threshold in dB |
| `Strip[N].MC` | float (bool) | 0 / 1 | Button | Mix down to Mono |
| `Strip[N].K` | float | 0 -- 4 | Encoder / Button | Karaoke-Modus |
| `Strip[N].Label` | string | max. 7 Zeichen | Display (bereits via Config) | Kanalname |

#### Bus-Routing (A1-A5, B1-B3)

Bestimmt, an welche Output-Busse ein Strip gesendet wird. Hohe Prioritat fur X-Touch.

| VM-Parameter | Typ | X-Touch Element | Anmerkung |
|---|---|---|---|
| `Strip[N].A1` | float (bool) | Button (REC/SELECT) | Routing zu Bus A1 |
| `Strip[N].A2` | float (bool) | Button (REC/SELECT) | Routing zu Bus A2 |
| `Strip[N].A3` | float (bool) | Button (REC/SELECT) | Routing zu Bus A3 |
| `Strip[N].A4` | float (bool) | Button (REC/SELECT) | Routing zu Bus A4 |
| `Strip[N].A5` | float (bool) | Button (REC/SELECT) | Routing zu Bus A5 |
| `Strip[N].B1` | float (bool) | Button (REC/SELECT) | Routing zu Bus B1 |
| `Strip[N].B2` | float (bool) | Button (REC/SELECT) | Routing zu Bus B2 |
| `Strip[N].B3` | float (bool) | Button (REC/SELECT) | Routing zu Bus B3 |

Implementierungsidee: REC-Buttons im "Routing-Modus" nutzen, bei dem SELECT einen
Kanal auswahlt und die REC-Buttons A1-A5/B1-B3 umschalten (8 Routing-Ziele auf 8 Buttons).

#### FX-Sends

| VM-Parameter | Typ | Wertebereich | X-Touch Element |
|---|---|---|---|
| `Strip[N].Reverb` | float | 0.0 -- 10.0 | Encoder |
| `Strip[N].Delay` | float | 0.0 -- 10.0 | Encoder |
| `Strip[N].Fx1` | float | 0.0 -- 10.0 | Encoder |
| `Strip[N].Fx2` | float | 0.0 -- 10.0 | Encoder |
| `Strip[N].PostReverb` | float (bool) | 0 / 1 | Button | Pre/Post-Fader |
| `Strip[N].PostDelay` | float (bool) | 0 / 1 | Button | Pre/Post-Fader |
| `Strip[N].PostFx1` | float (bool) | 0 / 1 | Button | Pre/Post-Fader |
| `Strip[N].PostFx2` | float (bool) | 0 / 1 | Button | Pre/Post-Fader |

#### Spatial Controls

| VM-Parameter | Typ | Wertebereich | X-Touch Element |
|---|---|---|---|
| `Strip[N].fx_x` | float | -0.5 -- 0.5 | Encoder |
| `Strip[N].fx_y` | float | 0.0 -- 1.0 | Encoder |

### Strip Sub-Objekte (nur Physical Strips 0-4, Potato only)

#### Gate

| VM-Parameter | Typ | Wertebereich | X-Touch Element |
|---|---|---|---|
| `Strip[N].Gate` | float | 0.0 -- 10.0 | Encoder (Knob-Modus) |
| `Strip[N].Gate.Threshold` | float | -60 -- 0 dB | Encoder |
| `Strip[N].Gate.Damping` | float | -60 -- 0 dB | Encoder |
| `Strip[N].Gate.BPSidechain` | float | 100 -- 4000 Hz | Encoder |
| `Strip[N].Gate.Attack` | float | 0.1 -- 1000 ms | Encoder |
| `Strip[N].Gate.Hold` | float | 0 -- 5000 ms | Encoder |
| `Strip[N].Gate.Release` | float | 0.1 -- 5000 ms | Encoder |

Implementierungsidee: Als EncoderFunction-Gruppe registrieren:
```csharp
new EncoderFunction("GATE",  $"Strip[{vmCh}].Gate",               0, 10, 0.5, ""),
new EncoderFunction("G.THR", $"Strip[{vmCh}].Gate.Threshold",    -60,  0, 1.0, "dB"),
new EncoderFunction("G.DMP", $"Strip[{vmCh}].Gate.Damping",      -60,  0, 1.0, "dB"),
new EncoderFunction("G.ATK", $"Strip[{vmCh}].Gate.Attack",       0.1, 1000, 10, "ms"),
new EncoderFunction("G.HLD", $"Strip[{vmCh}].Gate.Hold",           0, 5000, 50, "ms"),
new EncoderFunction("G.RLS", $"Strip[{vmCh}].Gate.Release",      0.1, 5000, 50, "ms"),
```

#### Compressor

| VM-Parameter | Typ | Wertebereich | X-Touch Element |
|---|---|---|---|
| `Strip[N].Comp` | float | 0.0 -- 10.0 | Encoder (Knob-Modus) |
| `Strip[N].Comp.GainIn` | float | -24 -- 24 dB | Encoder |
| `Strip[N].Comp.Threshold` | float | -40 -- 0 dB | Encoder |
| `Strip[N].Comp.Ratio` | float | 1.0 -- 8.0 | Encoder |
| `Strip[N].Comp.Attack` | float | 0.1 -- 200 ms | Encoder |
| `Strip[N].Comp.Release` | float | 0.1 -- 5000 ms | Encoder |
| `Strip[N].Comp.Knee` | float | 0 -- 1.0 | Encoder |
| `Strip[N].Comp.GainOut` | float | -24 -- 24 dB | Encoder |
| `Strip[N].Comp.MakeUp` | float (bool) | 0 / 1 | Button |

Implementierungsidee:
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

| VM-Parameter | Typ | Wertebereich | X-Touch Element |
|---|---|---|---|
| `Strip[N].Denoiser` | float | 0.0 -- 10.0 | Encoder |

Implementierung: `new EncoderFunction("DENOI", $"Strip[{vmCh}].Denoiser", 0, 10, 0.5, "")`

#### EQ

| VM-Parameter | Typ | Wertebereich | X-Touch Element |
|---|---|---|---|
| `Strip[N].EQ.on` | float (bool) | 0 / 1 | Button (REC) |
| `Strip[N].EQ.AB` | float (bool) | 0 / 1 | Button (SELECT) |
| `Strip[N].EQGain1` | float | -12 -- +12 dB | **Bereits implementiert** (LOW) |
| `Strip[N].EQGain2` | float | -12 -- +12 dB | **Bereits implementiert** (MID) |
| `Strip[N].EQGain3` | float | -12 -- +12 dB | **Bereits implementiert** (HIGH) |

#### Gainlayers (Potato only)

Gainlayers erlauben pro-Bus-Lautstarkeregelung pro Strip (8x8 Matrix).

| VM-Parameter | Typ | Wertebereich | X-Touch Element |
|---|---|---|---|
| `Strip[N].GainLayer[0]` | float | -60 -- +12 dB | Fader |
| `Strip[N].GainLayer[1]` | float | -60 -- +12 dB | Fader |
| ... | ... | ... | ... |
| `Strip[N].GainLayer[7]` | float | -60 -- +12 dB | Fader |

Implementierungsidee: Eigene Kanal-Ansicht "Gainlayer" die pro Strip die 8 Gainlayer
auf die 8 Fader mappt.

### Bus Parameter (Output-Kanale, Bus[0]-Bus[7])

#### Basis-Parameter

| VM-Parameter | Typ | Wertebereich | X-Touch Element | Anmerkung |
|---|---|---|---|---|
| `Bus[N].Mono` | float (bool) | 0 / 1 | Button | Mono-Schaltung |
| `Bus[N].Sel` | float (bool) | 0 / 1 | SELECT-Button | Bus-Auswahl (fur SEL-Modus) |
| `Bus[N].Label` | string | max. 7 Zeichen | Display | Busname |

#### EQ

| VM-Parameter | Typ | Wertebereich | X-Touch Element |
|---|---|---|---|
| `Bus[N].EQ.on` | float (bool) | 0 / 1 | REC-Button |
| `Bus[N].EQ.AB` | float (bool) | 0 / 1 | SELECT-Button |

#### FX-Returns

| VM-Parameter | Typ | Wertebereich | X-Touch Element |
|---|---|---|---|
| `Bus[N].ReturnReverb` | float | 0.0 -- 10.0 | Encoder |
| `Bus[N].ReturnDelay` | float | 0.0 -- 10.0 | Encoder |
| `Bus[N].ReturnFx1` | float | 0.0 -- 10.0 | Encoder |
| `Bus[N].ReturnFx2` | float | 0.0 -- 10.0 | Encoder |

#### Monitor

| VM-Parameter | Typ | Wertebereich | X-Touch Element |
|---|---|---|---|
| `Bus[N].Monitor` | float (bool) | 0 / 1 | Button |

#### Bus Modes (exklusiv -- nur einer kann aktiv sein)

| VM-Parameter | Beschreibung |
|---|---|
| `Bus[N].mode.normal` | Standard-Modus |
| `Bus[N].mode.Amix` | A-Mix |
| `Bus[N].mode.Bmix` | B-Mix |
| `Bus[N].mode.Composite` | Composite |
| `Bus[N].mode.TVMix` | TV-Mix |
| `Bus[N].mode.UpMix21` | Upmix 2.1 |
| `Bus[N].mode.UpMix41` | Upmix 4.1 |
| `Bus[N].mode.UpMix61` | Upmix 6.1 |
| `Bus[N].mode.CenterOnly` | Nur Center |
| `Bus[N].mode.LFEOnly` | Nur LFE |
| `Bus[N].mode.RearOnly` | Nur Rear |

Implementierungsidee: Per Encoder durchschalten (Cycle) oder als EncoderFunction-Gruppe.

### Spezielle Methoden / Utility-Parameter

| VM-Parameter | Typ | Beschreibung |
|---|---|---|
| `Strip[N].FadeTo` | Methode (via Set) | Sanftes Faden zu Zielwert |
| `Strip[N].FadeBy` | Methode (via Set) | Relatives Faden |
| `Bus[N].FadeTo` | Methode (via Set) | Sanftes Faden zu Zielwert |
| `Bus[N].FadeBy` | Methode (via Set) | Relatives Faden |
| `Strip[N].AppGain` | Methode | App-spezifische Lautstarkeregelung |
| `Strip[N].AppMute` | Methode | App-spezifische Stummschaltung |
| `Command.Restart` | float (trigger) | Voicemeeter neustarten | **Implementiert** (`Restart()`) |
| `Command.Shutdown` | float (trigger) | Voicemeeter beenden | Noch nicht implementiert |
| `Command.Show` | float (trigger) | Fenster in den Vordergrund | **Implementiert** (`ShowVoicemeeter()`) |
| `Command.Lock` | float (bool) | GUI sperren/entsperren | **Implementiert** (`LockGui(bool)`) |

### Macro Buttons

P/Invoke in `VoicemeeterRemote.cs`, gekapselt in `IVoicemeeterService.TriggerMacroButton(int)`:

```csharp
VoicemeeterRemote.MacroButtonGetStatus(int buttonIndex, out float value, int mode);
VoicemeeterRemote.MacroButtonSetStatus(int buttonIndex, float value, int mode);
```

| Mode | Beschreibung |
|---|---|
| 1 | State (Zustand: 0=aus, 1=an) |
| 2 | State + Trigger (wie physischer Klick) — **Implementiert** |
| 3 | Display (Anzeige-LED) |

**Implementiert**: Master-Buttons können als Macro-Button-Trigger konfiguriert werden
(Aktionstyp `TriggerMacroButton` mit Index 0–79 im Mapping-Editor).
Zusätzlich möglich: REC-Buttons auf Macro-Buttons 0-7 mappen.

### Dirty Flags / Polling

| Methode | C#-Zugriff | Beschreibung |
|---|---|---|
| `VBVMR_IsParametersDirty` | `VoicemeeterRemote.IsParametersDirty()` | Gibt 1 zuruck wenn sich Parameter geandert haben |
| Level Polling | `VoicemeeterRemote.GetLevel(type, index, out val)` | Levels werden direkt gepollt (kein Dirty-Flag) |

Level-Typen fur `GetLevel(type, ...)`:

| Type | Beschreibung | Index-Formel |
|---|---|---|
| 0 | Strip Pre-Fader Input | `strip * 2 + lr` |
| 1 | Strip Post-Fader Input | `strip * 2 + lr` |
| 2 | Strip Post-Mute Input | `strip * 2 + lr` |
| 3 | Bus Output | `bus * 8 + channel` (bis 7.1) |

---

## X-Touch Kapazitat vs. genutzte Parameter

### Ubersicht der freien Kontrollelemente

| X-Touch Element | Verfugbar | Genutzt | Frei | Mogliche Nutzung |
|---|---|---|---|---|
| Fader (8x) | 8 | 8 (Gain) | 0 | -- |
| MUTE-Button (8x) | 8 | 8 (Mute) | 0 | -- |
| SOLO-Button (8x) | 8 | 8 (Solo) | 0 | -- |
| **REC-Button (8x)** | 8 | **0** | **8** | EQ.on, Routing, Macro |
| **SELECT-Button (8x)** | 8 | **0** | **8** | Mono, EQ.AB, Kanalauswahl |
| Encoder (8x) | 8 | 8 (2 Nav + 6 EQ) | per CycleFunction erweiterbar | Comp, Gate, Denoiser, FX |
| Encoder-Funktionen | ~5 pro Encoder | 5 (HIGH/MID/LOW/PAN/GAIN) | beliebig erweiterbar | +Comp, Gate, Denoiser, Reverb |
| Display (16 Felder) | 16 | 16 | dynamisch nutzbar | Parameterwerte |
| Level-Meter (8x) | 8 | 8 | 0 | -- |

### Implementierungsprioritat

**Hoch** (sofort nutzbar, grosser Mehrwert):
- Bus-Routing `Strip[N].A1`-`A5`, `B1`-`B3` auf REC/SELECT-Buttons
- `Strip[N].Denoiser` als EncoderFunction
- `Strip[N].EQ.on` auf REC-Button

**Mittel** (nutzlich, moderater Aufwand):
- `Strip[N].Comp` / `Strip[N].Gate` als EncoderFunction-Gruppen
- `Bus[N].EQ.on` auf REC-Button (im Outputs-View)
- FX-Sends (Reverb, Delay, Fx1, Fx2) als EncoderFunctions
- Macro-Buttons auf REC-Buttons

**Niedrig** (Nische / selten benotigt):
- Bus Modes (normal, Amix, TVMix, etc.)
- Gainlayers
- Spatial Controls (Color_x, Color_y, fx_x, fx_y)
- Karaoke-Modus (`Strip[N].K`)

---

## Erweiterung: Schritt-fur-Schritt

### Neuen Float-Parameter als EncoderFunction hinzufugen

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

Keine weiteren Anderungen notig -- der Encoder-Callback in `OnEncoderRotated` schreibt
den Wert automatisch via `VoicemeeterRemote.SetParameterFloat()`.

### Neuen Bool-Parameter auf Button mappen

1. `IVoicemeeterService` erweitern:
```csharp
void SetParameter(string paramName, float value);
float GetParameter(string paramName);
```

2. `VoicemeeterState` erweitern:
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

4. In `UpdateParameters()` die LED synchronisieren:
```csharp
// REC-Button LED = EQ.on Status
_xtouch.SetButtonLed(xtCh, XTouchButtonType.Rec,
    _vmState.EqOn[vmCh] ? LedState.On : LedState.Off);
```

---

## Quellen

- [Voicemeeter Remote API (PDF)](https://download.vb-audio.com/Download_CABLE/VoicemeeterRemoteAPI.pdf)
- [Voicemeeter Potato User Manual (PDF)](https://vb-audio.com/Voicemeeter/VoicemeeterPotato_UserManual.pdf)
- [voicemeeter-api-python (GitHub)](https://github.com/onyx-and-iris/voicemeeter-api-python)
- [voicemeeter-api (PyPI)](https://pypi.org/project/voicemeeter-api/)
