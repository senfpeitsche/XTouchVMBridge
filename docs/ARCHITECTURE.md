# Architektur & Erweiterbarkeit

## Übersicht

```
                    ┌─────────────────────────────────────────────┐
                    │         XTouchVMBridge.App (WPF)              │
                    │   TrayIcon, LogWindow, MidiDebugWindow,     │
                    │   XTouchPanelWindow                         │
                    │   AudioDeviceMonitor, ScreenLockDetector,    │
                    │   MasterButtonActionService,                 │
                    │   SegmentDisplayService                      │
                    └──────────────┬──────────────────────────────┘
                                   │ DI (Microsoft.Extensions.Hosting)
                    ┌──────────────┼──────────────────────┐
                    │              │                       │
        ┌───────────▼──┐   ┌──────▼───────────┐   ┌──────▼───────────┐
        │ XTouchVMBridge │   │  XTouchVMBridge     │   │  XTouchVMBridge    │
        │    .Midi     │   │   .Voicemeeter    │   │     .Core        │
        │              │   │                   │   │                  │
        │ XTouchDevice │   │ VoicemeeterBridge │   │ Enums, Models    │
        │ MackieProto  │   │ VoicemeeterSvc    │   │ Interfaces       │
        │ MidiDecoder  │   │ ConfigService     │   │ Hardware Controls│
        │               │   │ Native P/Invoke   │   │ Events           │
        └──────────────┘   └──────────────────┘   └──────────────────┘
```

## Abhängigkeiten zwischen den Projekten

```
XTouchVMBridge.Core       ← keine Abhängigkeiten (Fundament)
XTouchVMBridge.Midi       ← Core
XTouchVMBridge.Voicemeeter← Core
XTouchVMBridge.App        ← Core, Midi, Voicemeeter
XTouchVMBridge.Tests      ← Core, Midi, Voicemeeter
```

Die Richtung ist immer "nach unten": App kennt alles, Core kennt niemanden.
Midi und Voicemeeter kennen sich gegenseitig nicht — die Verbindung läuft über Interfaces aus Core.

## Dependency Injection

In `App.xaml.cs` werden alle Services registriert:

```csharp
services.AddSingleton<IMidiDevice, XTouchDevice>();    // Core-Interface → Midi-Implementierung
services.AddSingleton<IVoicemeeterService, VoicemeeterService>();
services.AddSingleton<IScreenLockDetector, ScreenLockDetector>();
services.AddHostedService<AudioDeviceMonitorService>(); // Hintergrund-Thread + X-Touch Reconnect
services.AddHostedService<VoicemeeterBridge>();          // 100ms Polling-Loop
services.AddSingleton<MasterButtonActionService>();      // Master-Button → Programm/Keys/Text/VM-Commands/Macro
services.AddHostedService<SegmentDisplayService>();      // 7-Segment-Display (Uhrzeit etc.)
services.AddSingleton<TrayIconService>();
```

Um ein anderes MIDI-Gerät zu verwenden: eigene Klasse schreiben die `IMidiDevice` implementiert,
und in der DI-Registrierung austauschen.

## Hardware-Controls Hierarchie

```
HardwareControlBase (abstrakt)
├── FaderControl          — motorisierter Fader mit dB-Konvertierung
├── ButtonControl         — Button mit LED (typisiert per XTouchButtonType)
├── EncoderControl        — Drehencoder mit Push, LED-Ring und Funktionsliste
│   └── EncoderFunction   — einzelne steuerbare Funktion (Name, VM-Parameter, Min/Max/Step)
├── DisplayControl        — LCD-Display (7×2 Zeichen + Farbe)
└── LevelMeterControl     — Pegelanzeige (0–13 Stufen)
```

Jeder Control-Typ kennt seinen Kanal-Index und eine eindeutige `ControlId`.
Alle Controls eines Kanals sind in `XTouchChannel` gebündelt.

### Encoder-Funktionsliste

Jeder `EncoderControl` kann eine Liste von `EncoderFunction`-Objekten halten.
Durch Drücken des Encoders wird zyklisch durch die Funktionsliste geschaltet.
Drehen ändert den Wert der aktiven Funktion.

```
EncoderControl
├── Functions: List<EncoderFunction>    // z.B. [HIGH, MID, LOW, PAN, GAIN]
├── ActiveFunctionIndex: int            // aktuell aktive Funktion
├── RingMode: XTouchEncoderRingMode     // Anzeigemodus (Dot/Pan/Wrap/Spread)
├── CycleFunction()                     // Drücken → nächste Funktion
├── ApplyTicks(int ticks)               // Drehen → Wert der aktiven Funktion ändern
├── SyncRingToActiveFunction()          // Ring-Position an Wert anpassen
└── CalculateCcValue() → byte           // CC-Wert für X-Touch LED-Ring

EncoderFunction
├── Name: string                        // "HIGH", "MID", "LOW", etc.
├── VmParameter: string                 // "Strip[0].EQGain3"
├── MinValue / MaxValue / StepSize      // Wertebereich + Schrittweite
├── CurrentValue: double                // aktueller Wert
├── ApplyTicks(int ticks) → double      // Wert ändern (mit Clamping)
├── ToRingPosition() → int              // Wert → Ring-Position (0-10)
└── FormatValue() → string              // "3.5dB" (InvariantCulture)
```

Jede Funktion merkt sich ihren eigenen Wert — beim Umschalten bleibt der Zustand erhalten.

### Encoder-Display-Verhalten

Bei Drücken/Drehen des Encoders (Hardware) oder Strg+Klick/Mausrad (Panel):
- **Obere Display-Zeile**: Zeigt den Parameter-Namen (z.B. "HIGH", "MID", "PAN")
- **Untere Display-Zeile**: Zeigt den aktuellen Wert (z.B. "0.0dB", "-3.5dB")
- **Nach 5 Sekunden**: Automatisches Zurückschalten auf Kanalname (oben) und View-Name (unten)

### Panel-Encoder-Steuerung

Im X-Touch Panel können Encoder vollständig per Maus bedient werden:
- **Strg+Klick**: Ruft `encoder.CycleFunction()` auf (identisch mit Hardware-Drücken)
- **Mausrad**: Ruft `encoder.ApplyTicks(±1)` auf (identisch mit Hardware-Drehen)
- **Strg+Mausrad**: `ApplyTicks(±5)` für grobe Steuerung
- Aktueller Wert wird aus Voicemeeter gelesen und nach Änderung zurückgeschrieben
- Ring-Position, Display-Text und Hardware werden synchronisiert

### Encoder LED-Ring Synchronisation

Die Encoder-Ringe werden automatisch bei jedem Parameter-Update (`UpdateParameters()`) synchronisiert:
- Liest den aktuellen Wert aus Voicemeeter
- Berechnet die Ring-Position basierend auf Min/Max
- Sendet den korrekten CC-Wert an das X-Touch

---

## Neues Hardware-Control hinzufügen

### Beispiel: Jog Wheel

Das X-Touch hat ein Jog Wheel (CC 88, CW=65, CCW=1), das aktuell nicht als Control modelliert ist.

#### 1. Neues Control in `XTouchVMBridge.Core/Hardware/`

```csharp
// XTouchVMBridge.Core/Hardware/JogWheelControl.cs
namespace XTouchVMBridge.Core.Hardware;

public class JogWheelControl : HardwareControlBase
{
    public JogWheelControl() : base(0, "JogWheel") { }

    /// <summary>Letzte Drehrichtung: +1 CW, -1 CCW, 0 idle.</summary>
    public int LastDirection { get; set; }
}
```

#### 2. Event in `XTouchVMBridge.Core/Events/`

```csharp
// XTouchVMBridge.Core/Events/JogWheelEventArgs.cs
namespace XTouchVMBridge.Core.Events;

public class JogWheelEventArgs : EventArgs
{
    /// <summary>+1 = CW (rechts), -1 = CCW (links).</summary>
    public int Direction { get; }
    public JogWheelEventArgs(int direction) => Direction = direction;
}
```

#### 3. Event in `IMidiDevice` Interface ergänzen

```csharp
// In XTouchVMBridge.Core/Interfaces/IMidiDevice.cs:
event EventHandler<JogWheelEventArgs>? JogWheelTurned;
```

#### 4. In `XTouchDevice` implementieren

```csharp
// In XTouchVMBridge.Midi/XTouch/XTouchDevice.cs:
// 1. Event deklarieren:
public event EventHandler<JogWheelEventArgs>? JogWheelTurned;

// 2. Im HandleEncoder-Switch-Case für CC 88:
private void HandleEncoder(byte cc, byte value)
{
    // Jog Wheel: CC 88
    if (cc == 88)
    {
        int direction = value == 65 ? 1 : value == 1 ? -1 : 0;
        JogWheelTurned?.Invoke(this, new JogWheelEventArgs(direction));
        return;
    }
    // ... bestehender Encoder-Code
}
```

#### 5. In `VoicemeeterBridge` nutzen

```csharp
// In XTouchVMBridge.Voicemeeter/Services/VoicemeeterBridge.cs:
_xtouch.JogWheelTurned += (_, e) =>
{
    // z.B. Master-Volume anpassen
    _logger.LogDebug("Jog Wheel: {Direction}", e.Direction);
};
```

---

## Neuen Button-Typ hinzufügen

### Beispiel: Fünfter Button-Typ "Assign"

Falls das X-Touch um einen zusätzlichen Button-Typ erweitert wird:

#### 1. Enum erweitern

```csharp
// XTouchVMBridge.Core/Enums/XTouchButtonType.cs
public enum XTouchButtonType : byte
{
    Rec = 0,
    Solo = 1,
    Mute = 2,
    Select = 3,
    Assign = 4     // NEU
}
```

Das war's an Code-Änderung. `XTouchChannel` erzeugt automatisch für alle Enum-Werte einen `ButtonControl`:

```csharp
// In XTouchChannel constructor (bereits vorhanden):
foreach (var buttonType in Enum.GetValues<XTouchButtonType>())
{
    buttons[buttonType] = new ButtonControl(index, buttonType);
}
```

Die MIDI Note-Nummer berechnet sich automatisch: `NoteNumber = (int)buttonType * 8 + channel`.
Für Assign wäre das Note 32–39 (aktuell Encoder Press — Kollision prüfen!).

Falls die Note-Berechnung anders sein muss, die Formel in `ButtonControl` anpassen:

```csharp
// XTouchVMBridge.Core/Hardware/ButtonControl.cs
NoteNumber = buttonType == XTouchButtonType.Assign
    ? 40 + channel   // eigene Note-Range
    : (int)buttonType * 8 + channel;
```

---

## Neue Encoder-Funktion hinzufügen

### Beispiel: Denoiser-Stärke als Encoder-Funktion

Um einem Encoder eine neue steuerbare Funktion hinzuzufügen:

#### 1. In `VoicemeeterBridge.RegisterEncoderFunctions()` ergänzen

```csharp
// Bestehende Funktionen für einen Encoder:
encoder.AddFunctions(new[]
{
    new EncoderFunction("HIGH", $"Strip[{vmCh}].EQGain3", -12, 12, 0.5, "dB"),
    new EncoderFunction("MID",  $"Strip[{vmCh}].EQGain2", -12, 12, 0.5, "dB"),
    new EncoderFunction("LOW",  $"Strip[{vmCh}].EQGain1", -12, 12, 0.5, "dB"),
    // NEU: Denoiser-Stärke
    new EncoderFunction("DENOI", $"Strip[{vmCh}].Denoiser", 0, 10, 0.5, ""),
});
```

Die neue Funktion ist sofort per Drücken des Encoders erreichbar.
Das Display zeigt den Wert, der Encoder-Ring die Position.

#### 2. Optional: Eigene EncoderFunction-Subklasse

Für Funktionen mit speziellem Verhalten (z.B. nicht-lineares Mapping):

```csharp
public class LogarithmicEncoderFunction : EncoderFunction
{
    public LogarithmicEncoderFunction(string name, string vmParam)
        : base(name, vmParam, 20, 20000, 1, "Hz") { }

    // Ring-Position logarithmisch berechnen
    public new int ToRingPosition()
    {
        double logNorm = Math.Log(CurrentValue / MinValue) / Math.Log(MaxValue / MinValue);
        return (int)Math.Round(logNorm * 15);
    }
}
```

---

## Neuen Encoder-Ring-Modus hinzufügen

```csharp
// XTouchVMBridge.Core/Enums/XTouchEncoderRingMode.cs
public enum XTouchEncoderRingMode : byte
{
    Dot = 0,
    Pan = 1,
    Wrap = 2,
    Spread = 3,
    // CustomMode = 4  ← Hier ergänzen
}
```

### X-Touch Encoder LED-Ring Mapping (empirisch ermittelt)

Der X-Touch hat 13 LEDs pro Encoder-Ring: L6 L5 L4 L3 L2 L1 [M] R1 R2 R3 R4 R5 R6

Die CC-Werte (CC 48-55) werden wie folgt interpretiert:

| Modus | CC-Bereich | Nutzbare Werte | Beschreibung |
|-------|------------|----------------|--------------|
| Dot (0) | 1-11 | 11 Positionen | Einzelne LED (L5..M..R5) |
| Pan (1) | 17-27 | 11 Positionen | Von Mitte füllend (für EQ: -12dB..0..+12dB) |
| Wrap (2) | 33-43 | 11 Positionen | Von links füllend (für Gain: 0..100%) |
| Spread (3) | 49-54 | 6 Positionen | Symmetrisch von Mitte |

**+64 auf jeden Wert**: Schaltet L6 und R6 (äußere LEDs) zusätzlich ein.

#### Vollständige CC-Wert → LED Zuordnung (ohne +64)

**Mode 0 (Dot) - Einzelne LED:**
| Wert | LEDs |
|------|------|
| 0 | - (aus) |
| 1 | L5 |
| 2 | L4 |
| 3 | L3 |
| 4 | L2 |
| 5 | L1 |
| 6 | M |
| 7 | R1 |
| 8 | R2 |
| 9 | R3 |
| 10 | R4 |
| 11 | R5 |

**Mode 1 (Pan) - Von Mitte füllend:**
| Wert | LEDs |
|------|------|
| 17 | L5 L4 L3 L2 L1 M |
| 18 | L4 L3 L2 L1 M |
| 19 | L3 L2 L1 M |
| 20 | L2 L1 M |
| 21 | L1 M |
| 22 | M |
| 23 | M R1 |
| 24 | M R1 R2 |
| 25 | M R1 R2 R3 |
| 26 | M R1 R2 R3 R4 |
| 27 | M R1 R2 R3 R4 R5 |

**Mode 2 (Wrap) - Von links füllend:**
| Wert | LEDs |
|------|------|
| 33 | L5 |
| 34 | L5 L4 |
| 35 | L5 L4 L3 |
| 36 | L5 L4 L3 L2 |
| 37 | L5 L4 L3 L2 L1 |
| 38 | L5 L4 L3 L2 L1 M |
| 39 | L5 L4 L3 L2 L1 M R1 |
| 40 | L5 L4 L3 L2 L1 M R1 R2 |
| 41 | L5 L4 L3 L2 L1 M R1 R2 R3 |
| 42 | L5 L4 L3 L2 L1 M R1 R2 R3 R4 |
| 43 | L5 L4 L3 L2 L1 M R1 R2 R3 R4 R5 |

**Mode 3 (Spread) - Symmetrisch von Mitte:**
| Wert | LEDs |
|------|------|
| 49 | M |
| 50 | L1 M R1 |
| 51 | L2 L1 M R1 R2 |
| 52 | L3 L2 L1 M R1 R2 R3 |
| 53 | L4 L3 L2 L1 M R1 R2 R3 R4 |
| 54 | L5 L4 L3 L2 L1 M R1 R2 R3 R4 R5 |

**Mit +64 (L6 und R6 zusätzlich an):**
Alle obigen Werte + 64 schalten zusätzlich die äußeren LEDs L6 und R6 ein.
Beispiel: Wert 86 (= 22 + 64) = L6 M R6

Die Berechnung in `EncoderControl.CalculateCcValue()`:
```csharp
case XTouchEncoderRingMode.Pan:
    baseValue = Math.Clamp(_ringPosition, 0, 10) + 17;  // Position 0-10 → Wert 17-27
    break;
```

---

## Neue Kanal-Ansicht hinzufügen

In `VoicemeeterBridge` die `_channelViews`-Liste erweitern:

```csharp
private readonly List<ChannelView> _channelViews = new()
{
    new("Home",    new[] { 3, 4, 5, 6, 7, 9, 10, 12 }),
    new("Outputs", new[] { 8, 9, 10, 11, 12, 13, 14, 15 }),
    new("Inputs",  new[] { 0, 1, 2, 3, 4, 5, 6, 7 }),
    new("Custom",  new[] { 0, 3, 5, 7, 9, 11, 13, 15 }),  // NEU
};
```

Die Ansicht wird automatisch per Encoder 1 erreichbar.

### Per-View Display-Farben

Jede Channel View kann pro Strip eine eigene Display-Farbe definieren, die die globale Kanalfarbe überschreibt.

```csharp
// In ChannelViewConfig (Core/Models/ChannelViewConfig.cs):
public XTouchColor?[]? ChannelColors { get; set; }

public XTouchColor? GetChannelColor(int stripIndex)
{
    if (ChannelColors == null || stripIndex < 0 || stripIndex >= ChannelColors.Length)
        return null;
    return ChannelColors[stripIndex];
}
```

Die Farb-Auswertung in `VoicemeeterBridge.UpdateDisplays()`:

```csharp
// View-Farbe hat Priorität vor globaler Kanalfarbe
var viewColor = ChannelViews[_currentViewIndex].GetChannelColor(xtCh);
if (viewColor.HasValue)
    colors[xtCh] = viewColor.Value;
else if (_config.Channels.TryGetValue(vmCh, out var chConfig))
    colors[xtCh] = chConfig.Color;
```

Der Channel View Editor (`ChannelViewEditorDialog`) zeigt pro Strip eine Farb-ComboBox
mit farbigen Rechteck-Vorschauen. `null`-Einträge werden als "—" dargestellt (globale Farbe).

---

## Strg+Klick-Steuerung im X-Touch Panel

Das X-Touch Panel unterstützt Strg+Klick als Direkt-Steuerung für alle Controls:

### Master-Buttons

```csharp
// OnMasterButtonClick: Strg+Klick → Aktion oder LED-Toggle
if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
{
    // 1. Konfigurierte Aktion ausführen (z.B. Media-Keys)
    if (_masterButtonActionService?.ExecuteAction(noteNumber) == true)
        return;
    // 2. Fallback: LED toggeln (On/Off) + MIDI-Note ans X-Touch senden
    _masterButtonLedState.TryGetValue(noteNumber, out bool isOn);
    _masterButtonLedState[noteNumber] = !isOn;
    _device?.SetMasterButtonLed(noteNumber, !isOn ? LedState.On : LedState.Off);
    // PanelView-Button visuell aktualisieren (sender ist der WPF-Button)
    return;
}
```

`ExecuteAction()` gibt `bool` zurück — `true` wenn eine Aktion konfiguriert und ausgeführt wurde.
Ohne konfigurierte Aktion wird die LED getoggelt (On/Off) statt immer auf On gesetzt.
Der LED-State wird in `_masterButtonLedState` (Dictionary<int,bool>) gespeichert.

### Kanal-Buttons (REC/SOLO/MUTE/SELECT)

```csharp
// OnHwButtonClick: Strg+Klick → VM-Parameter toggeln oder LED toggeln
if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
{
    ExecuteHwButtonAction(ch, type);
    return;
}
```

`ExecuteHwButtonAction` prüft ob ein VM-Parameter zugewiesen ist:
- **Zugewiesen** (z.B. Mute, Solo): VM-Parameter toggeln (wie bisher)
- **Nicht zugewiesen** (z.B. Rec, Select): LED direkt toggeln über `_manualLedState` Dictionary

```csharp
// Kein Mapping → LED manuell toggeln (Panel + Hardware)
if (!hasMapping)
{
    var key = (ch, type);
    _manualLedState.TryGetValue(key, out bool isOn);
    _manualLedState[key] = !isOn;
    _device.SetButtonLed(ch, type, !isOn ? LedState.On : LedState.Off);
}
```

`GetEffectiveLedState()` prüft in `RefreshAll()` ob ein manueller State vorhanden ist:

```csharp
private LedState GetEffectiveLedState(int ch, XTouchButtonType type, XTouchChannel xtCh)
{
    if (_manualLedState.TryGetValue((ch, type), out bool isOn))
        return isOn ? LedState.On : LedState.Off;
    return xtCh.GetButton(type).LedState;  // Fallback: Hardware-State
}
```

Die VoicemeeterBridge überschreibt nicht-zugewiesene Buttons nicht mehr auf Off
(der else-Zweig in `UpdateParameters()` wurde entfernt).

### Encoder (Strg+Klick + Mausrad)

```csharp
// OnEncoderClick: Strg+Klick → nächste Funktion durchschalten
if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
{
    var fn = encoder.CycleFunction();      // HIGH → MID → LOW → ...
    fn.CurrentValue = _vm.GetParameter(fn.VmParameter);  // Wert aus VM lesen
    encoder.SyncRingToActiveFunction();    // Ring-Position synchronisieren
    _device.SetEncoderRing(ch, ...);       // Hardware aktualisieren
    return;
}
```

```csharp
// OnEncoderMouseWheel: Mausrad → Wert ändern
int ticks = e.Delta > 0 ? 1 : -1;
if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
    ticks *= 5;                            // Strg+Mausrad: 5× gröbere Schritte
var fn = encoder.ApplyTicks(ticks);        // Wert ändern (mit Clamping)
_vm.SetParameter(fn.VmParameter, (float)fn.CurrentValue);  // An VM senden
_device.SetEncoderRing(ch, ...);           // Ring aktualisieren
_device.SetDisplayText(ch, 0, fn.Name);    // Display: Funktionsname
_device.SetDisplayText(ch, 1, fn.FormatValue());  // Display: Wert
```

### Fader (Transparentes Overlay-Pattern)

WPF-Slider mit `IsEnabled = false` empfangen keine Maus-Events (auch nicht Preview/Tunneling).
Lösung: Ein transparentes `Border`-Overlay über dem Slider im selben `Grid`:

```
Grid (faderHost)
├── Slider (IsEnabled=false, IsHitTestVisible=false)  ← visuell
└── Border (Background=Transparent, Cursor=Hand)       ← fängt Maus-Events
```

```csharp
var faderHost = new Grid { Width = 32, Height = 150 };
faderHost.Children.Add(fader);           // Slider (disabled, kein HitTest)

var faderOverlay = new Border { Background = Brushes.Transparent };
faderOverlay.MouseLeftButtonDown += (_, e) => OnFaderMouseDown(ch, e);
faderOverlay.MouseMove += (_, e) => OnFaderMouseMove(ch, e);
faderOverlay.MouseLeftButtonUp += (_, _) => OnFaderMouseUp(ch);
faderHost.Children.Add(faderOverlay);    // Overlay (empfängt Maus-Events)
```

Bei Strg+Klick wird die Mausposition in einen Fader-Wert umgerechnet (`SetFaderFromMousePosition`),
der Slider-Value direkt gesetzt und der dB-Wert an Voicemeeter gesendet.
Mouse-Capture liegt auf dem Overlay, damit Drag-Bewegungen auch außerhalb verfolgt werden.
`RefreshAll()` überspringt den Fader während `_draggingFaderChannel` aktiv ist.

---

## SetMasterButtonLed (MIDI-Ausgabe)

Neue Methode in `IMidiDevice` / `XTouchDevice` zum Senden von Note-On an Master-Section-Buttons:

```csharp
// IMidiDevice:
void SetMasterButtonLed(int noteNumber, LedState state);

// XTouchDevice:
public void SetMasterButtonLed(int noteNumber, LedState state)
{
    byte velocity = state switch { ... };
    SendShortMessage(0x90, (byte)noteNumber, velocity);
}
```

Wird im Panel für Strg+Klick auf nicht-konfigurierte Master-Buttons verwendet,
um die LED zu toggeln und die MIDI-Note ans Gerät zu senden.
Der Toggle-State wird in `_masterButtonLedState` im PanelView gespeichert.

---

## Master-Button-Aktionen

Master-Section-Buttons (Notes 40+) feuern das `MasterButtonChanged`-Event. Der `MasterButtonActionService`
reagiert auf konfigurierte Buttons und führt die zugehörige Aktion aus.

### Neuen Aktionstyp hinzufügen

1. **Enum erweitern** in `Core/Models/MasterButtonActionConfig.cs`:
```csharp
public enum MasterButtonActionType
{
    None, VmParameter, LaunchProgram, SendKeys, SendText,
    CycleChannelView, RestartAudioEngine, ShowVoicemeeter, LockGui, TriggerMacroButton,
    HttpRequest  // NEU
}
```

2. **Config-Felder ergänzen** in `MasterButtonActionConfig`:
```csharp
public string? HttpUrl { get; set; }
public string? HttpMethod { get; set; }
```

3. **Execute-Methode** in `MasterButtonActionService.cs` hinzufügen:
```csharp
private async Task ExecuteHttpRequest(MasterButtonActionConfig config) { ... }
```

4. **Im Switch-Case** aufrufen:
```csharp
case MasterButtonActionType.HttpRequest:
    ExecuteHttpRequest(actionConfig);
    break;
```

5. **Editor** in `XTouchPanelWindow.MappingEditor.cs`:
   - ComboBox-Eintrag hinzufügen in `ShowMasterButtonMappingPanel()`
   - Sub-Panel in `XTouchPanelWindow.xaml` einfügen
   - Visibility in `UpdateMasterActionSubPanels()` steuern
   - Speichern/Laden in `OnMasterActionSave()` / `OnMasterActionClear()`

### LED-Feedback

Jede Master-Button-Aktion hat einen konfigurierbaren LED-Feedback-Modus:

```csharp
public enum LedFeedbackMode
{
    Blink,     // LED blinkt 150ms auf
    Toggle,    // LED wechselt An/Aus bei jedem Druck
    Blinking   // LED blinkt dauerhaft (Hardware-Blink via Mackie Protocol Velocity 2)
}
```

Die `MasterButtonActionConfig` enthält das Feld `LedFeedback` (Default: `Blink`).
Der `MasterButtonActionService` verwaltet Toggle-/Blink-States in einem `Dictionary<int, bool>`.
Der Blinking-Modus nutzt den nativen Hardware-Blink des Mackie-Protokolls (`LedState.Blink = 2`)
und benötigt keine Software-Timer — erneutes Drücken toggelt zwischen Blinken und Aus.

### Note-Nummern (Master Section)

| Sektion | Notes | Beispiele |
|---|---|---|
| Fader Bank | 46-47 | BANK LEFT=46, BANK RIGHT=47 (frei zuweisbar) |
| Channel | 48-49 | CHANNEL LEFT=48, CHANNEL RIGHT=49 |
| Flip | 50 | **Hardcoded: Channel View Cycling** |
| Encoder Assign | 40-45 | TRACK=40, SEND=41, PAN=42, PLUG-IN=43, EQ=44, INST=45 |
| NAME/VALUE | 52-53 | NAME=52, VALUE=53 |
| Function Keys | 54-61 | F1=54, F2=55, ..., F8=61 |
| Global View | 62-69 | MIDI=62, INPUTS=63, AUDIO=64, ... |
| Transport | 91-95 | REW=91, FF=92, STOP=93, PLAY=94, REC=95 |
| SMPTE/BEATS | 113-114 | SMPTE=113, BEATS=114 |

### Flip-Button für Channel View Cycling

Der **Flip-Button (Note 50)** ist fest für das Durchschalten der Channel Views reserviert:
- Drücken wechselt zur nächsten View (Home → Outputs → Inputs → ...)
- Die LED blinkt kurz zur Bestätigung
- Bei View-Wechsel werden die Encoder-Funktionen für die neuen Kanäle neu registriert

Die Fader Bank Left/Right Buttons (Notes 46-47) sind dadurch für andere Aktionen frei zuweisbar.

---

## 7-Segment-Display (SegmentDisplayService)

Der `SegmentDisplayService` ist ein `BackgroundService` der das 12-stellige 7-Segment-Display ansteuert.

### X-Touch 7-Segment Protokoll

Das X-Touch im MCU-Modus verwendet **Mackie Control CC-Nachrichten** für das 7-Segment-Display:

| CC | Digit-Position |
|----|----------------|
| 64 | Rechtestes Digit (12) |
| 65-74 | Digits 11-2 |
| 75 | Linkestes Digit (1) |

**CC-Wert-Format:**
- Bits 0-5: ASCII-Zeichen (nur untere 6 Bits)
- Bit 6 (0x40): Dezimalpunkt aktiv

Beispiel: `'5'` (ASCII 0x35) → CC-Wert 0x35 & 0x3F = 0x35
Beispiel mit Punkt: `'5.'` → CC-Wert 0x35 | 0x40 = 0x75

Die Reihenfolge ist **rechts nach links** — das erste Zeichen im String geht an CC 75 (links),
das letzte an CC 64 (rechts).

### Neuen Anzeige-Modus hinzufügen

1. **Enum erweitern** in `SegmentDisplayService.cs`:
```csharp
public enum SegmentDisplayMode
{
    Time, Date, CpuUsage, Off,
    IpAddress  // NEU
}
```

2. **Format-Methode** hinzufügen:
```csharp
private static string FormatIpAddress()
{
    // Max 12 Zeichen, Punkte werden als Dots gerendert
    return "192.168.1.42";
}
```

3. **Im Switch-Case** aufrufen:
```csharp
SegmentDisplayMode.IpAddress => FormatIpAddress(),
```

Der Modus wird automatisch per Cycle-Button erreichbar.

### 7-Segment-Font

Der Font in `MackieProtocol.SegmentFont` mappt Zeichen auf Segment-Bitmuster.
Unterstützt: 0-9, A-F, H, J, L, P, S, U, Y, n, o, r, t, h, b, c, d, u, -, _, Leerzeichen, °.

Punkte (`.`) und Doppelpunkte (`:`) in Strings werden automatisch als Dot auf dem vorherigen Digit gesetzt.

---

## Neue MIDI-Nachricht im Debug Monitor

Der `MidiMessageDecoder` dekodiert alle Nachrichten. Um einen neuen Typ hinzuzufügen:

1. In `DecodeControlChange()` (für CC) oder `DecodeNoteOn()` (für Notes) einen neuen Case hinzufügen.
2. Der Decoder gibt immer ein `DecodedMidiMessage` Record zurück mit allen Feldern.
3. Das `MidiDebugWindow` zeigt es automatisch an — keine Änderung nötig.
4. Filter im XAML ergänzen (neuer `ComboBoxItem` in `ControlTypeFilter`).

---

## Design Patterns im Projekt

| Pattern | Wo | Zweck |
|---|---|---|
| **Dependency Injection** | `App.xaml.cs` | Alle Services austauschbar, testbar |
| **BackgroundService** | `VoicemeeterBridge`, `AudioDeviceMonitorService`, `SegmentDisplayService` | Managed Threading mit CancellationToken |
| **Observer (Events)** | `IMidiDevice` Events, `MasterButtonChanged`, `ConnectionStateChanged` | Entkopplung MIDI-Input → Business-Logic |
| **Strategy** | `ChannelView`, `ShortcutMode`, `EncoderFunction` | Umschaltbare Kanal-Zuordnungen / Encoder-Funktionen |
| **State Cycling** | `EncoderControl.CycleFunction()` | Zyklisches Durchschalten von Encoder-Funktionen |
| **Factory** | `XTouchChannel` Constructor | Automatische Button-Erzeugung per Enum |
| **Adapter** | `VoicemeeterService` | Abstrahiert P/Invoke-Aufrufe |
| **Template Method** | `HardwareControlBase` | Gemeinsame Basis für alle Controls |
| **Scheduler** | `TaskScheduler` (in Bridge) | Verzögerte Aktionen (Display-Reset) |
| **Transparent Overlay** | `XTouchPanelWindow` Fader | Maus-Events über deaktiviertem WPF-Control abfangen |
| **Fallback Chain** | `OnMasterButtonClick` | Aktion → LED-Toggle → Detail-Panel |

## Voicemeeter API Parameter

Eine vollstandige Referenz aller Voicemeeter Remote API Parameter -- implementiert und noch
erweiterbar -- findet sich in [VOICEMEETER-API.md](VOICEMEETER-API.md).

Dort dokumentiert:
- Alle aktuell genutzten Parameter mit Datei:Zeile-Referenzen
- Alle noch nicht implementierten Parameter (Strip, Bus, Gate, Comp, Denoiser, FX, Routing, Modes)
- Empfohlene X-Touch-Zuordnungen und Implementierungsprioritat
- Schritt-fur-Schritt-Anleitungen zur Erweiterung (EncoderFunction, Button-Mapping)
- X-Touch Kapazitat vs. genutzte Parameter (freie REC/SELECT-Buttons)

---

## X-Touch MIDI-Protokoll (empirisch ermittelt)

Das X-Touch im **MCU-Modus** (Mackie Control Universal) verwendet folgende Protokolle:

### SysEx-Prefix

```
Mackie Control Main:     F0 00 00 66 14 ...
Mackie Control Extended: F0 00 00 66 15 ...
```

Das X-Touch (nicht Extender) verwendet Device-ID **0x14** (MCU Main).

### Handshake (wichtig für Initialisierung)

Beim Verbinden muss ein Handshake gesendet werden:
```
F0 00 00 66 14 13 00 F7
```

Ohne diesen Handshake reagieren LCDs und andere Displays möglicherweise nicht.

### Initialisierung (SendInitialization)

Beim Verbinden werden alle Controls auf einen definierten Ausgangszustand gesetzt:

1. **Handshake** (SysEx)
2. **Faders** auf Mittelposition
3. **Encoder-Ringe** löschen (CC 48-55 → 0)
4. **Channel-Button-LEDs** aus (Notes 0–31 → velocity 0)
5. **Master-Section-Button-LEDs** aus (Notes 40–103 → velocity 0)
6. **Display-Farben** auf Weiß
7. **Display-Text** löschen

Notes 32–39 (Encoder Press) und 104+ (Fader Touch) werden nicht resettet,
da diese nur Input-Events sind und keine LEDs haben.

### LCD-Displays (8 × 2 Zeilen à 7 Zeichen)

**Mackie Control Format (funktioniert zuverlässig):**
```
F0 00 00 66 14 12 [offset] [ASCII-Daten...] F7
```
- Offset 0-55: Obere Zeile (8 Kanäle × 7 Zeichen)
- Offset 56-111: Untere Zeile

**Display-Farben:**
```
F0 00 00 66 14 72 [c0] [c1] [c2] [c3] [c4] [c5] [c6] [c7] F7
```
- Farben 0-7: Black, Red, Green, Yellow, Blue, Magenta, Cyan, White

### Encoder LED-Ringe (CC 48-55)

Siehe Abschnitt "X-Touch Encoder LED-Ring Mapping" oben.

### 7-Segment-Display (CC 64-75)

Siehe Abschnitt "X-Touch 7-Segment Protokoll" oben.

### Level Meter (Channel Aftertouch)

```
D0 [channel << 4 | level]
```
- Channel: 0-7 (obere 4 Bits)
- Level: 0-13 (untere 4 Bits)

### Fader (Pitchbend)

```
E[channel] [LSB] [MSB]
```
- 14-Bit Wert, signiert: -8192 bis +8191
- Channel 0-7: Strip-Fader, Channel 8: Main Fader

### Test-Skripte

Im Projektverzeichnis befinden sich Python-Testskripte zur Protokoll-Analyse:
- `test_lcd.py` - LCD-Display Tests
- `test_segment.py` - 7-Segment-Display Tests
- `test_encoder_ring.py` - Encoder LED-Ring Tests

Diese Skripte erfordern `mido` und helfen beim Debugging von MIDI-Kommunikation.

---

## NuGet-Abhängigkeiten

| Paket | Projekt | Zweck |
|---|---|---|
| NAudio 2.2.1 | Midi, App | MIDI I/O, Audio-Device-Enumeration |
| Hardcodet.NotifyIcon.Wpf 1.1.0 | App | System-Tray-Icon |
| InputSimulatorCore 1.0.5 | App | Keyboard-Simulation (SendKeys, SendText) |
| Microsoft.Extensions.Hosting 8.0.1 | App | DI, BackgroundService, Logging |
| Serilog + Sinks | App | Strukturiertes Logging in Datei + Console |
| Microsoft.Extensions.Logging.Abstractions | Midi, Voicemeeter | ILogger<T> Interface |
| Microsoft.Extensions.Hosting.Abstractions | Voicemeeter | BackgroundService |
