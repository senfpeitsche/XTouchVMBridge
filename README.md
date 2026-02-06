# XTouchVMBridge (C#)

Windows-Anwendung zur Steuerung von Voicemeeter Potato via Behringer X-Touch (Full / Extender).
System-Tray-App mit Audio-Device-Monitoring und Screen-Lock-Schutz.

Portiert vom originalen Python-Projekt in ein C# .NET 8 Projekt.

## Voraussetzungen

- .NET 8 SDK (oder neuer)
- Windows 10/11
- Voicemeeter Potato (installiert, `VoicemeeterRemote64.dll` muss im Systempfad sein)
- Behringer X-Touch oder X-Touch Extender (USB, im Mackie Control Modus)

## Build & Start

```bash
cd XTouchVMBridgeCSharp
dotnet build XTouchVMBridge.slnx
dotnet run --project XTouchVMBridge.App
```

## Tests

```bash
dotnet test XTouchVMBridge.Tests
```

Aktuell 99 Tests: Hardware-Controls, EncoderFunction/CycleLogic, MackieProtocol, MidiMessageDecoder, XTouchChannel-Model.

## Solution-Struktur

```
XTouchVMBridgeCSharp/
├── XTouchVMBridge.slnx                  # Solution (5 Projekte)
│
├── XTouchVMBridge.Core/                 # Shared: Enums, Interfaces, Models, Hardware
│   ├── Enums/                         # XTouchColor, XTouchButtonType, LedState, ...
│   ├── Events/                        # FaderEventArgs, ButtonEventArgs, ...
│   ├── Hardware/                      # FaderControl, ButtonControl, EncoderControl, EncoderFunction, ...
│   ├── Interfaces/                    # IMidiDevice, IVoicemeeterService, ...
│   └── Models/                        # XTouchChannel, XTouchVMBridgeConfig, ...
│
├── XTouchVMBridge.Midi/                 # MIDI-Kommunikation (NAudio)
│   └── XTouch/                        # XTouchDevice, MackieProtocol, MidiMessageDecoder
│
├── XTouchVMBridge.Voicemeeter/          # Voicemeeter-Integration
│   ├── Native/                        # VoicemeeterRemote P/Invoke
│   └── Services/                      # VoicemeeterService, VoicemeeterBridge, Config
│
├── XTouchVMBridge.App/                  # WPF-Anwendung (Entry Point)
│   ├── Services/                      # TrayIcon, AudioDeviceMonitor, ScreenLock,
│   │                                  # MasterButtonActionService, SegmentDisplayService
│   └── Views/                         # LogWindow, MidiDebugWindow, XTouchPanelWindow
│
└── XTouchVMBridge.Tests/                # xUnit Tests
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
  "segmentDisplayCycleButton": 113,
  "channels": {
    "0": { "name": "WaveMIC", "type": "Hardware Input 1", "color": "green" },
    "1": { "name": "RiftMIC", "type": "Hardware Input 2", "color": "green" },
    ...
  },
  "masterButtonActions": {
    "54": { "actionType": "LaunchProgram", "programPath": "notepad.exe" },
    "55": { "actionType": "SendKeys", "keyCombination": "Ctrl+Shift+M" },
    "56": { "actionType": "SendText", "text": "Hallo Welt" },
    "91": { "actionType": "SendKeys", "keyCombination": "MediaPrev" },
    "92": { "actionType": "SendKeys", "keyCombination": "MediaNext" },
    "93": { "actionType": "SendKeys", "keyCombination": "MediaStop" },
    "94": { "actionType": "SendKeys", "keyCombination": "MediaPlay" }
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
- **MIDI Debug Monitor**: Echtzeit-Anzeige aller MIDI-Nachrichten (Tray-Menu)
- **X-Touch Panel**: Interaktive visuelle Darstellung der X-Touch Oberfläche (Tray-Menu).
  Zeigt alle Controls in Echtzeit, Klick auf ein Control zeigt MIDI-Details und zugeordnete Funktion.
  - **Strg+Klick auf Master-Buttons**: Führt die konfigurierte Aktion aus (z.B. Media-Keys).
    Ohne konfigurierte Aktion wird die LED getoggelt (On/Off) und die MIDI-Note ans X-Touch gesendet.
  - **Strg+Klick auf Kanal-Buttons** (REC/SOLO/MUTE/SELECT): Toggelt den zugeordneten Voicemeeter-Parameter.
    Nicht-zugewiesene Buttons toggeln ihre LED direkt (On/Off).
  - **Strg+Klick auf Encoder**: Schaltet durch die Funktionsliste (z.B. HIGH → MID → LOW → PAN → GAIN).
  - **Mausrad auf Encoder**: Ändert den Wert der aktiven Funktion. Strg+Mausrad = 5× gröbere Schritte.
  - **Strg+Klick auf Fader**: Fader per Mausbewegung steuern (Drag), Wert wird in Echtzeit an Voicemeeter gesendet.
- **Per-View Display-Farben**: Jede Channel View kann eigene Display-Farben pro Strip definieren,
  die die globale Kanalfarbe überschreiben. Konfigurierbar im Channel View Editor.
- **Log-Fenster**: Rolling-Log mit Level-Filter (Tray-Menu / Doppelklick)
- **X-Touch Geräteauswahl**: Unterstützung für X-Touch und X-Touch Extender, wählbar im Tray-Menu
- **Auto-Reconnect**: Automatische Wiederverbindung bei Gerätetrennung (alle 5 Sekunden)
- **Verbindungsstatus**: Anzeige im Tray-Tooltip und Kontextmenü ("X-Touch: Verbunden/Getrennt")
- **Master-Button-Aktionen**: F1-F8 und andere Master-Buttons können konfiguriert werden für:
  - Windows-Programme starten (mit Argumenten)
  - Tastenkombinationen senden (z.B. Ctrl+Shift+M, Alt+F4)
  - Media-Keys senden (MediaPlay, MediaNext, MediaPrev, MediaStop)
  - Text senden (via Zwischenablage + Ctrl+V)
  - Voicemeeter-Parameter toggeln
- **7-Segment-Display**: Timecode-Anzeige zeigt Uhrzeit, Datum oder Speicherverbrauch.
  SMPTE-Button (Note 113) schaltet zwischen Modi um.

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
- **Strg+Klick-Steuerung**: Alle Controls können per Strg+Klick direkt bedient werden:
  - Master-Buttons: konfigurierte Aktion ausführen, oder LED toggeln (On/Off)
  - Kanal-Buttons (REC/SOLO/MUTE/SELECT): zugeordneten VM-Parameter toggeln, oder LED toggeln bei nicht-zugewiesenen Buttons
  - Encoder: durch zugewiesene Funktionen cyclen (HIGH → MID → LOW → PAN → GAIN → ...)
  - Fader: per Maus-Drag steuern (transparentes Overlay über dem deaktivierten Slider)
- **Mausrad-Steuerung** auf Encodern: Wert der aktiven Funktion ändern, Strg+Mausrad für 5× gröbere Schritte

## Channel View Editor

Channel Views können im X-Touch Panel über den Mapping-Editor bearbeitet werden.
Pro View werden 8 Voicemeeter-Kanäle auf die physischen X-Touch-Strips gemappt.

- **Kanal-Zuordnung**: Jeder Strip kann einem beliebigen VM-Kanal (0-15) zugewiesen werden
- **Display-Farben**: Pro Strip kann eine eigene Display-Farbe festgelegt werden,
  die die globale Kanalfarbe überschreibt. Verfügbare Farben: Off, Red, Green, Yellow, Blue, Magenta, Cyan, White.
  Wird keine Farbe gesetzt ("—"), gilt die globale Kanalfarbe.

In der `config.json` unter `channelViews`:

```json
"channelViews": [
  {
    "name": "Home",
    "channels": [3, 4, 5, 6, 7, 9, 10, 12],
    "channelColors": ["green", "green", "cyan", "cyan", "cyan", "yellow", "yellow", "magenta"]
  },
  {
    "name": "Outputs",
    "channels": [8, 9, 10, 11, 12, 13, 14, 15],
    "channelColors": null
  }
]
```

`channelColors`: Array mit 8 Einträgen (je Strip) oder `null` für globale Farben.
Einzelne Einträge können `null` sein um die globale Farbe für diesen Strip beizubehalten.

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

- **Drücken** (Hardware): Schaltet zur nächsten Funktion (HIGH → MID → LOW → PAN → GAIN → HIGH ...)
- **Drehen** (Hardware): Ändert den Wert der aktiven Funktion
- **Strg+Klick** (Panel): Schaltet zur nächsten Funktion (identisch mit Hardware-Drücken)
- **Mausrad** (Panel): Ändert den Wert der aktiven Funktion (±1 Step pro Notch)
- **Strg+Mausrad** (Panel): Grobe Steuerung (±5 Steps pro Notch)
- **Display**: Zeigt kurzzeitig den neuen Funktionsnamen, dann den Wert, dann ">FUNKTIONSNAME"
- **Encoder-Ring**: Position zeigt den aktuellen Wert relativ zum Bereich (0-10 LEDs)

Encoder 1 bleibt für Ansichtswechsel, Encoder 3 für Shortcut-Modus.

## Master-Button-Aktionen

Die Master-Section-Buttons (F1-F8, Transport, Utility, etc.) können im X-Touch Panel mit
benutzerdefinierten Aktionen belegt werden. Klick auf einen Master-Button im Panel zeigt den
Mapping-Editor mit folgenden Aktionstypen:

| Aktionstyp | Beschreibung | Konfigurationsfelder |
|---|---|---|
| **VM-Parameter toggeln** | Bool-Parameter in Voicemeeter umschalten | VM-Parameter (z.B. `Strip[0].Mute`) |
| **Programm starten** | Windows-Programm ausführen | Programmpfad + optionale Argumente |
| **Tastenkombination** | Keyboard-Shortcut simulieren | Kombination (z.B. `Ctrl+Shift+M`, `Alt+F4`, `F5`) |
| **Text senden** | Text via Zwischenablage einfügen | Beliebiger Text |

Unterstützte Modifier: `Ctrl`, `Alt`, `Shift`, `Win`. Unterstützte Sondertasten: `F1`-`F24`,
`Enter`, `Escape`, `Tab`, `Space`, `Delete`, `Home`, `End`, `PageUp`, `PageDown`, Pfeiltasten,
`VolumeUp`, `VolumeDown`, `Mute`, `MediaPlay`, `MediaNext`, `MediaPrev`, `MediaStop`, etc.

In der `config.json` unter `masterButtonActions` (Key = MIDI Note-Nummer):

```json
"masterButtonActions": {
  "54": { "actionType": "LaunchProgram", "programPath": "C:\\Windows\\notepad.exe", "programArgs": "" },
  "55": { "actionType": "SendKeys", "keyCombination": "Ctrl+Shift+M" },
  "56": { "actionType": "SendText", "text": "Hallo Welt" },
  "57": { "actionType": "VmParameter", "vmParameter": "Strip[0].Mute" }
}
```

Note-Nummern für Function-Buttons: F1=54, F2=55, ..., F8=61.
Transport-Buttons: REW=91, FF=92, STOP=93, PLAY=94, REC=95.

### Media-Fernbedienung (z.B. YouTube in Vivaldi/Chrome)

Die Transport-Buttons können als Media-Keys konfiguriert werden, um Browser-Mediaplayer zu steuern:

```json
"masterButtonActions": {
  "91": { "actionType": "SendKeys", "keyCombination": "MediaPrev" },
  "92": { "actionType": "SendKeys", "keyCombination": "MediaNext" },
  "93": { "actionType": "SendKeys", "keyCombination": "MediaStop" },
  "94": { "actionType": "SendKeys", "keyCombination": "MediaPlay" }
}
```

Die Media-Keys werden vom Betriebssystem an den aktiven Mediaplayer weitergeleitet
(z.B. YouTube im Browser, Spotify, VLC).

## 7-Segment-Display (Timecode-Anzeige)

Das 12-stellige 7-Segment-Display auf dem X-Touch zeigt standardmäßig die **Uhrzeit** an.
Per SMPTE-Button (Note 113) kann zwischen folgenden Modi gewechselt werden:

| Modus | Anzeige | Update-Intervall |
|---|---|---|
| **Time** (Standard) | `HH.MM.SS` | 500ms |
| **Date** | `dd.MM.YYYY` | 10s |
| **Memory** | Speicherverbrauch in MB | 2s |
| **Off** | Display leer | - |

Der Cycle-Button kann in der Config angepasst werden:

```json
"segmentDisplayCycleButton": 113
```

`0` = Cycle-Funktion deaktiviert. Das Display kommuniziert über Behringer-eigene SysEx-Nachrichten
(`F0 00 20 32 dd 37 ...`) mit automatischer Device-ID-Erkennung (X-Touch=0x14, Ext=0x15).

## Credits & Danksagung

Dieses Projekt ist eine vollständige Neuentwicklung in C# / .NET 8, basierend auf dem originalen
Python-Projekt **audiomanager** von [TheRedNet](https://github.com/TheRedNet):

- **Original-Repository**: [github.com/TheRedNet/audiomanager](https://github.com/TheRedNet/audiomanager)
- **Original-Sprache**: Python (XTouchVM.py, XTouchLib.py, audiomanager.pyw)
- **Portierung**: C# / WPF / .NET 8 mit Claude Code (Anthropic)

Die Kernidee — Voicemeeter Potato über das Behringer X-Touch im Mackie Control Modus zu steuern —
stammt aus dem Python-Original. Die C#-Portierung erweitert das Konzept um eine modulare Architektur
mit Dependency Injection, umfangreiche Unit-Tests, eine grafische Oberfläche (WPF) mit interaktivem
X-Touch Panel, MIDI Debug Monitor und konfigurierbaren Master-Button-Aktionen.
