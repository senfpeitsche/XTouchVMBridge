# AudioManager (C#)

Windows-Anwendung zur Steuerung von Voicemeeter Potato via Behringer X-Touch (Full / Extender).
System-Tray-App mit Audio-Device-Monitoring, Roland Fantom MIDI-Filterung und Screen-Lock-Schutz.

Portiert vom originalen Python-Projekt in ein C# .NET 8 Projekt.

## Voraussetzungen

- .NET 8 SDK (oder neuer)
- Windows 10/11
- Voicemeeter Potato (installiert, `VoicemeeterRemote64.dll` muss im Systempfad sein)
- Behringer X-Touch oder X-Touch Extender (USB, im Mackie Control Modus)
- Optional: Roland Fantom-06 + LoopMIDI ("FANTOM filterd" Port)

## Build & Start

```bash
cd AudioManagerCSharp
dotnet build AudioManager.slnx
dotnet run --project AudioManager.App
```

## Tests

```bash
dotnet test AudioManager.Tests
```

Aktuell 99 Tests: Hardware-Controls, EncoderFunction/CycleLogic, MackieProtocol, MidiMessageDecoder, XTouchChannel-Model.

## Solution-Struktur

```
AudioManagerCSharp/
├── AudioManager.slnx                  # Solution (5 Projekte)
│
├── AudioManager.Core/                 # Shared: Enums, Interfaces, Models, Hardware
│   ├── Enums/                         # XTouchColor, XTouchButtonType, LedState, ...
│   ├── Events/                        # FaderEventArgs, ButtonEventArgs, ...
│   ├── Hardware/                      # FaderControl, ButtonControl, EncoderControl, EncoderFunction, ...
│   ├── Interfaces/                    # IMidiDevice, IVoicemeeterService, ...
│   └── Models/                        # XTouchChannel, AudioManagerConfig, ...
│
├── AudioManager.Midi/                 # MIDI-Kommunikation (NAudio)
│   ├── XTouch/                        # XTouchDevice, MackieProtocol, MidiMessageDecoder
│   └── Fantom/                        # FantomMidiHandler
│
├── AudioManager.Voicemeeter/          # Voicemeeter-Integration
│   ├── Native/                        # VoicemeeterRemote P/Invoke
│   └── Services/                      # VoicemeeterService, VoicemeeterBridge, Config
│
├── AudioManager.App/                  # WPF-Anwendung (Entry Point)
│   ├── Services/                      # TrayIcon, AudioDeviceMonitor, ScreenLock
│   └── Views/                         # LogWindow, MidiDebugWindow, XTouchPanelWindow
│
└── AudioManager.Tests/                # xUnit Tests
    ├── Hardware/                      # Fader, Display, Encoder, EncoderFunction, LevelMeter
    ├── XTouch/                        # MackieProtocol, MidiMessageDecoder
    └── Models/                        # XTouchChannel
```

## Konfiguration

Beim ersten Start wird `config.json` erzeugt. Darin werden pro Kanal (0-15) Name, Typ und Farbe definiert:

```json
{
  "voicemeeterApiType": "potato",
  "enableXTouch": true,
  "enableFantom": true,
  "channels": {
    "0": { "name": "WaveMIC", "type": "Hardware Input 1", "color": "green" },
    "1": { "name": "RiftMIC", "type": "Hardware Input 2", "color": "green" },
    ...
  }
}
```

- Kanalnamen: max. 7 Zeichen (ASCII), werden auf dem X-Touch LCD angezeigt
- Farben: `off`, `red`, `green`, `yellow`, `blue`, `magenta`, `cyan`, `white`
- Kanäle 0-7: Voicemeeter Input Strips
- Kanäle 8-15: Voicemeeter Output Buses

## Dokumentation

- [ARCHITECTURE.md](docs/ARCHITECTURE.md) -- Projektstruktur, DI, Design Patterns, Erweiterbarkeit
- [VOICEMEETER-API.md](docs/VOICEMEETER-API.md) -- Vollstandige Voicemeeter Remote API Parameter-Referenz (implementiert + erweiterbar)
- [MIGRATION.md](docs/MIGRATION.md) -- Zuordnung Python-Original zu C#-Implementierung

## Features

- **X-Touch (Full)**: 8 Kanalstreifen + Main Fader + Master Section (Transport, Encoder Assign, Function, Jog Wheel, etc.)
- **Fader, Buttons, Encoder**: Rec/Solo/Mute/Select, motorisierte Fader, LCD-Displays, Level-Meter
- **Encoder-Funktionsliste**: Jeder Encoder kann mehrere Funktionen haben (z.B. HIGH/MID/LOW EQ, PAN, GAIN).
  Drücken schaltet zyklisch durch die Funktionsliste, Drehen ändert den Wert der aktiven Funktion.
  Aktuelle Funktion und Wert werden im Display und auf dem Encoder-Knob angezeigt.
- **Voicemeeter Bridge**: Echtzeit-Steuerung von Gain, Mute, Solo; Level-Meter-Feedback
- **Kanal-Ansichten**: Home / Outputs / Inputs, umschaltbar per Encoder 1
- **Shortcut-Modi**: Desktop / VR Audio-Routing, umschaltbar per Encoder 3
- **Audio-Device-Monitor**: Erkennt USB-Geräteänderungen, startet Voicemeeter neu
- **Screen-Lock-Schutz**: Blockiert X-Touch-Eingabe bei gesperrtem Bildschirm
- **Fantom MIDI-Filter**: Leitet Note On/Off und CC vom Fantom-06 an LoopMIDI weiter
- **MIDI Debug Monitor**: Echtzeit-Anzeige aller MIDI-Nachrichten (Tray-Menu)
- **X-Touch Panel**: Interaktive visuelle Darstellung der X-Touch Oberfläche (Tray-Menu).
  Zeigt alle Controls in Echtzeit, Klick auf ein Control zeigt MIDI-Details und zugeordnete Funktion.
- **Log-Fenster**: Rolling-Log mit Level-Filter (Tray-Menu / Doppelklick)

## MIDI Debug Monitor

Öffnet sich über das Tray-Menü unter "MIDI Debug Monitor". Zeigt alle ein- und ausgehenden MIDI-Nachrichten des X-Touch in Echtzeit an.

Jede Nachricht wird anhand der Behringer X-Touch MIDI-Dokumentation dekodiert und zeigt:
- Zeitstempel, Richtung (IN/OUT), Control-Typ, Kanal/ID, Wert, resultierende Aktion, rohe Hex-Bytes

Filter: Richtung (IN/OUT/SysEx), Control-Typ, Kanal 1-8.

Siehe auch: `Document_BE_X-TOUCH-X-TOUCH-EXTENDER-MIDI-Mode-Implementation.pdf` im Projektordner.

## X-Touch Panel

Interaktive visuelle Darstellung der vollständigen X-Touch Oberfläche, erreichbar via Tray-Menu "X-Touch Panel":

- **Links**: 8 Kanalstreifen (LCD, Encoder + Ring, REC/SOLO/MUTE/SELECT, Fader, Level-Meter, Touch) + Main Fader
- **Rechts**: Master Section (Encoder Assign, Display/Assignment, Global View, Function F1-F8,
  Modify/Automation/Utility, Transport mit Rewind/Forward/Stop/Play/Record, Fader Bank/Channel Navigation, Jog Wheel)
- **Echtzeit-Updates**: 100ms Timer + Events vom MIDI-Device
- **Klick-Detail**: Jedes Control zeigt im Detail-Panel: aktueller Zustand, Encoder-Funktionsliste mit
  aktivem Modus (z.B. ">HIGH = 3.5dB"), MIDI-Protokoll-Details, Hersteller-Doku-Referenzen

## Encoder-Funktionsliste

Die Encoder (Drehregler) unterstützen eine konfigurierbare Liste von Funktionen pro Kanal.
Standardmäßig sind für Encoder 2, 4-8 folgende Funktionen registriert:

| Funktion | Parameter | Bereich | Schrittweite |
|---|---|---|---|
| HIGH | EQGain3 | -12..+12 dB | 0.5 dB |
| MID | EQGain2 | -12..+12 dB | 0.5 dB |
| LOW | EQGain1 | -12..+12 dB | 0.5 dB |
| PAN | Pan_x | -0.5..+0.5 | 0.05 |
| GAIN | Gain | -60..+12 dB | 0.5 dB |

- **Drücken**: Schaltet zur nächsten Funktion (HIGH → MID → LOW → PAN → GAIN → HIGH ...)
- **Drehen**: Ändert den Wert der aktiven Funktion
- **Display**: Zeigt kurzzeitig den neuen Funktionsnamen, dann den Wert, dann ">FUNKTIONSNAME"
- **Encoder-Ring**: Position zeigt den aktuellen Wert relativ zum Bereich (0-15 LEDs)

Encoder 1 bleibt für Ansichtswechsel, Encoder 3 für Shortcut-Modus.
