# Architektur & Erweiterbarkeit

## Übersicht

```
                    ┌─────────────────────────────────────────────┐
                    │         AudioManager.App (WPF)              │
                    │   TrayIcon, LogWindow, MidiDebugWindow,     │
                    │   XTouchPanelWindow                         │
                    │   AudioDeviceMonitor, ScreenLockDetector     │
                    └──────────────┬──────────────────────────────┘
                                   │ DI (Microsoft.Extensions.Hosting)
                    ┌──────────────┼──────────────────────┐
                    │              │                       │
        ┌───────────▼──┐   ┌──────▼───────────┐   ┌──────▼───────────┐
        │ AudioManager │   │  AudioManager     │   │  AudioManager    │
        │    .Midi     │   │   .Voicemeeter    │   │     .Core        │
        │              │   │                   │   │                  │
        │ XTouchDevice │   │ VoicemeeterBridge │   │ Enums, Models    │
        │ MackieProto  │   │ VoicemeeterSvc    │   │ Interfaces       │
        │ MidiDecoder  │   │ ConfigService     │   │ Hardware Controls│
        │ FantomHandler│   │ Native P/Invoke   │   │ Events           │
        └──────────────┘   └──────────────────┘   └──────────────────┘
```

## Abhängigkeiten zwischen den Projekten

```
AudioManager.Core       ← keine Abhängigkeiten (Fundament)
AudioManager.Midi       ← Core
AudioManager.Voicemeeter← Core
AudioManager.App        ← Core, Midi, Voicemeeter
AudioManager.Tests      ← Core, Midi, Voicemeeter
```

Die Richtung ist immer "nach unten": App kennt alles, Core kennt niemanden.
Midi und Voicemeeter kennen sich gegenseitig nicht — die Verbindung läuft über Interfaces aus Core.

## Dependency Injection

In `App.xaml.cs` werden alle Services registriert:

```csharp
services.AddSingleton<IMidiDevice, XTouchDevice>();    // Core-Interface → Midi-Implementierung
services.AddSingleton<IVoicemeeterService, VoicemeeterService>();
services.AddSingleton<IScreenLockDetector, ScreenLockDetector>();
services.AddSingleton<FantomMidiHandler>();
services.AddHostedService<AudioDeviceMonitorService>(); // läuft als Hintergrund-Thread
services.AddHostedService<VoicemeeterBridge>();          // 100ms Polling-Loop
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

#### 1. Neues Control in `AudioManager.Core/Hardware/`

```csharp
// AudioManager.Core/Hardware/JogWheelControl.cs
namespace AudioManager.Core.Hardware;

public class JogWheelControl : HardwareControlBase
{
    public JogWheelControl() : base(0, "JogWheel") { }

    /// <summary>Letzte Drehrichtung: +1 CW, -1 CCW, 0 idle.</summary>
    public int LastDirection { get; set; }
}
```

#### 2. Event in `AudioManager.Core/Events/`

```csharp
// AudioManager.Core/Events/JogWheelEventArgs.cs
namespace AudioManager.Core.Events;

public class JogWheelEventArgs : EventArgs
{
    /// <summary>+1 = CW (rechts), -1 = CCW (links).</summary>
    public int Direction { get; }
    public JogWheelEventArgs(int direction) => Direction = direction;
}
```

#### 3. Event in `IMidiDevice` Interface ergänzen

```csharp
// In AudioManager.Core/Interfaces/IMidiDevice.cs:
event EventHandler<JogWheelEventArgs>? JogWheelTurned;
```

#### 4. In `XTouchDevice` implementieren

```csharp
// In AudioManager.Midi/XTouch/XTouchDevice.cs:
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
// In AudioManager.Voicemeeter/Services/VoicemeeterBridge.cs:
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
// AudioManager.Core/Enums/XTouchButtonType.cs
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
// AudioManager.Core/Hardware/ButtonControl.cs
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
// AudioManager.Core/Enums/XTouchEncoderRingMode.cs
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
| **BackgroundService** | `VoicemeeterBridge`, `AudioDeviceMonitorService` | Managed Threading mit CancellationToken |
| **Observer (Events)** | `IMidiDevice` Events | Entkopplung MIDI-Input → Business-Logic |
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
| Microsoft.Extensions.Hosting 8.0.1 | App | DI, BackgroundService, Logging |
| Serilog + Sinks | App | Strukturiertes Logging in Datei + Console |
| Microsoft.Extensions.Logging.Abstractions | Midi, Voicemeeter | ILogger<T> Interface |
| Microsoft.Extensions.Hosting.Abstractions | Voicemeeter | BackgroundService |
