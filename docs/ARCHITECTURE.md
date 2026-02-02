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
services.AddSingleton<MasterButtonActionService>();      // Master-Button → Programm/Keys/Text
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
├── CycleFunction()                     // Drücken → nächste Funktion
├── ApplyTicks(int ticks)               // Drehen → Wert der aktiven Funktion ändern
└── SyncRingToActiveFunction()          // Ring-Position an Wert anpassen

EncoderFunction
├── Name: string                        // "HIGH", "MID", "LOW", etc.
├── VmParameter: string                 // "Strip[0].EQGain3"
├── MinValue / MaxValue / StepSize      // Wertebereich + Schrittweite
├── CurrentValue: double                // aktueller Wert
├── ApplyTicks(int ticks) → double      // Wert ändern (mit Clamping)
├── ToRingPosition() → int             // Wert → Ring (0-15)
└── FormatValue() → string             // "3.5dB" (InvariantCulture)
```

Jede Funktion merkt sich ihren eigenen Wert — beim Umschalten bleibt der Zustand erhalten.

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

Der CC-Wert wird in `EncoderControl.CalculateCcValue()` berechnet:
`value = mode * 16 + position [+ 64 wenn LED]`.

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

5. **Editor** in `XTouchPanelWindow.xaml` ein neues Sub-Panel einfügen
   und in `.xaml.cs` die `UpdateMasterActionSubPanels`-Methode erweitern.

### Note-Nummern (Master Section)

| Sektion | Notes | Beispiele |
|---|---|---|
| Encoder Assign | 40-45 | TRACK=40, SEND=41, PAN=42, PLUG-IN=43, EQ=44, INST=45 |
| NAME/VALUE | 52-53 | NAME=52, VALUE=53 |
| Function Keys | 54-61 | F1=54, F2=55, ..., F8=61 |
| Global View | 62-69 | MIDI=62, INPUTS=63, AUDIO=64, ... |
| Transport | 91-95 | REW=91, FF=92, STOP=93, PLAY=94, REC=95 |
| SMPTE/BEATS | 113-114 | SMPTE=113, BEATS=114 |

---

## 7-Segment-Display (SegmentDisplayService)

Der `SegmentDisplayService` ist ein `BackgroundService` der das 12-stellige 7-Segment-Display ansteuert.

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
