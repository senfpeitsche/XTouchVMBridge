# Migration: Python → C#

Dieses Dokument beschreibt die Zuordnung von Python-Dateien, -Klassen und -Konzepten
zu den entsprechenden C#-Implementierungen.

## Datei-Mapping

| Python-Datei | C#-Entsprechung | Projekt |
|---|---|---|
| `XTouchLibTypes.py` | `Core/Enums/` (XTouchColor, XTouchButtonType, LedState, XTouchEncoderRingMode) | Core |
| `XTouchLibTypes.py` (XTouchState) | `Core/Models/XTouchChannel.cs` + `Core/Hardware/*` | Core |
| `XTouchLib.py` (XTouch) | `Midi/XTouch/XTouchDevice.cs` | Midi |
| `XTouchLib.py` (Protokoll-Konstanten) | `Midi/XTouch/MackieProtocol.cs` | Midi |
| `XTouchLib2.py` | In `XTouchDevice` integriert (nicht separates Modul) | Midi |
| `XTouchLib2Channel.py` | `Core/Hardware/` (FaderControl, ButtonControl, EncoderControl, etc.) | Core |
| `XTouchVM.py` (App) | `Voicemeeter/Services/VoicemeeterBridge.cs` | Voicemeeter |
| `XTouchVM.py` (Scheduler) | `Voicemeeter/Services/VoicemeeterBridge.cs` (TaskScheduler inner class) | Voicemeeter |
| `XTouchVM.py` (ScreenLockDetector) | `App/Services/ScreenLockDetector.cs` + `ScreenLockMidiFilter.cs` | App |
| `XTouchVM.py` (level_interpolation) | `Core/Hardware/LevelMeterControl.cs` (DbToLevel) | Core |
| `XTouchVMinterface.py` (VMInterfaceFunctions) | `Voicemeeter/Services/VoicemeeterService.cs` | Voicemeeter |
| `XTouchVMinterface.py` (VMState) | `Core/Models/VoicemeeterState.cs` | Core |
| `XtouchVMconfig.py` | `Voicemeeter/Services/ConfigurationService.cs` | Voicemeeter |
| `audiomanager.pyw` (main) | `App/App.xaml.cs` (OnStartup) | App |
| `audiomanager.pyw` (TrayIcon) | `App/Services/TrayIconService.cs` | App |
| `audiomanager.pyw` (AudioDeviceMonitor) | `App/Services/AudioDeviceMonitorService.cs` | App |
| `audiomanager.pyw` (FantomMidiHandler) | `Midi/Fantom/FantomMidiHandler.cs` | Midi |
| `audiomanager.pyw` (LogWindow) | `App/Views/LogWindow.xaml` + `.xaml.cs` | App |
| `audiomanager.pyw` (Notificator) | Noch nicht portiert (via Toast-Notification-Paket machbar) | — |
| `islocked.py` | `App/Services/ScreenLockDetector.cs` | App |
| — (neu) | `Core/Hardware/EncoderFunction.cs` | Core |
| — (neu) | `Midi/XTouch/MidiMessageDecoder.cs` | Midi |
| — (neu) | `App/Views/MidiDebugWindow.xaml` + `.xaml.cs` | App |
| — (neu) | `App/Views/XTouchPanelWindow.xaml` + `.xaml.cs` | App |

## Konzept-Mapping: Python → C#

### Threading

| Python | C# |
|---|---|
| `threading.Thread(target=fn, daemon=True)` | `BackgroundService` / `Task.Run` mit `CancellationToken` |
| `self.running = False` (Stop-Flag) | `CancellationToken` / `stoppingToken.IsCancellationRequested` |
| `time.sleep(0.1)` | `await Task.Delay(100, stoppingToken)` |
| Thread + while-loop | `BackgroundService.ExecuteAsync()` |

### Callbacks → Events

| Python | C# |
|---|---|
| `fader_callback=fn` (Konstruktor-Parameter) | `event EventHandler<FaderEventArgs> FaderChanged` |
| `self.callback(channel, db, pos)` | `FaderChanged?.Invoke(this, new FaderEventArgs(ch, pos, db))` |
| `change_callback(fader=fn)` | Event subscriben: `device.FaderChanged += OnFader;` |

### State-Management

| Python | C# |
|---|---|
| `XTouchState` mit Properties + Validierung | `XTouchChannel` mit `HardwareControlBase`-Controls |
| `XTouchStateUnchecked` (ohne Validierung) | Nicht portiert — `XTouchChannel` validiert immer |
| `@property` + `@setter` | C# Properties mit `get/set` |
| `__eq__` / `copy()` | `record`-Typen wo nötig |

### Konfiguration

| Python | C# |
|---|---|
| `json.load/dump` | `System.Text.Json.JsonSerializer` |
| `config['channels']['0']` (dict-Zugriff) | `config.Channels[0]` (typisiert: `ChannelConfig`) |
| Manuelle Validierung in `load_config()` | `ConfigurationService.ValidateConfig()` |

### Logging

| Python | C# |
|---|---|
| `logging.getLogger(__name__)` | `ILogger<T>` via Dependency Injection |
| `ExceptionLoggingMeta` (Metaclass) | Nicht portiert — `try/catch` in `BackgroundService` reicht |
| `coloredlogs` (Terminal-Ausgabe) | `Serilog.Sinks.Console` |
| `logfile.log` (Datei) | `Serilog.Sinks.File` (Rolling, 7 Tage) |

### MIDI-Bibliothek

| Python (mido + python-rtmidi) | C# (NAudio) |
|---|---|
| `mido.open_input("X-Touch-Ext")` | `new MidiIn(deviceIndex)` |
| `mido.open_output("X-Touch-Ext")` | `new MidiOut(deviceIndex)` |
| `inport.callback = fn` | `midiIn.MessageReceived += OnMessage` |
| `outport.send(msg)` | `midiOut.Send(rawMessage)` |
| `mido.Message('pitchwheel', channel=0, pitch=1000)` | `SendShortMessage(0xE0, lsb, msb)` |
| `mido.Message('note_on', note=16, velocity=127)` | `SendShortMessage(0x90, 16, 127)` |
| `mido.Message('control_change', control=48, value=23)` | `SendShortMessage(0xB0, 48, 23)` |
| SysEx: `mido.Message('sysex', data=[...])` | `midiOut.SendBuffer(byte[])` |

### Voicemeeter-API

| Python (voicemeeterlib) | C# (P/Invoke) |
|---|---|
| `voicemeeterlib.api("potato")` | `VoicemeeterRemote.Login()` |
| `vm.strip[0].gain` | `VoicemeeterRemote.GetParameterFloat("Strip[0].Gain", out val)` |
| `vm.strip[0].gain = -6.0` | `VoicemeeterRemote.SetParameterFloat("Strip[0].Gain", -6.0f)` |
| `vm.bus[2].mute = True` | `VoicemeeterRemote.SetParameterFloat("Bus[2].Mute", 1.0f)` |
| `vm.strip[0].solo` | `VoicemeeterRemote.GetParameterFloat("Strip[0].Solo", out val)` |
| `vm.strip[0].denoiser.knob` | `VoicemeeterRemote.GetParameterFloat("Strip[0].Denoiser", out val)` |
| `vm.strip[0].comp.knob` | `VoicemeeterRemote.GetParameterFloat("Strip[0].Comp", out val)` |
| `vm.strip[0].gate.knob` | `VoicemeeterRemote.GetParameterFloat("Strip[0].Gate", out val)` |
| `vm.strip[0].A1` | `VoicemeeterRemote.GetParameterFloat("Strip[0].A1", out val)` |
| `vm.strip[0].eq.on` | `VoicemeeterRemote.GetParameterFloat("Strip[0].EQ.on", out val)` |
| `vm.bus[0].mode.normal` | `VoicemeeterRemote.GetParameterFloat("Bus[0].mode.normal", out val)` |
| `vm.bus[0].returnreverb` | `VoicemeeterRemote.GetParameterFloat("Bus[0].ReturnReverb", out val)` |
| `vm.pdirty` | `VoicemeeterRemote.IsParametersDirty()` |
| `vm.ldirty` | Nicht direkt verfugbar -- Level werden per Polling gelesen |
| `vm.command.restart()` | `VoicemeeterRemote.SetParameterFloat("Command.Restart", 1.0f)` |
| `vm.apply({...})` | Nicht portiert -- Parameter einzeln setzen |

Fur eine vollstandige Referenz aller verfugbaren Parameter siehe [VOICEMEETER-API.md](VOICEMEETER-API.md).

### UI-Frameworks

| Python | C# |
|---|---|
| `pystray.Icon` | `Hardcodet.Wpf.TaskbarNotification.TaskbarIcon` |
| `customtkinter.CTk` | WPF (`Window`) |
| `CTkTextbox` | `TextBox` (WPF) |
| `win11toast` | Noch nicht portiert — `Microsoft.Toolkit.Uwp.Notifications` geplant |
| `PIL.Image` (Icon-Erzeugung) | `System.Drawing.Bitmap` |

## MIDI-Protokoll Referenz

Das X-Touch Extender kommuniziert über das Mackie Control Extended Protokoll.
Alle Magic Numbers sind in `Midi/XTouch/MackieProtocol.cs` zentralisiert.

Die offizielle Behringer-Dokumentation liegt als PDF im Projekt:
`Document_BE_X-TOUCH-X-TOUCH-EXTENDER-MIDI-Mode-Implementation.pdf`

### Nachrichten-Typen (Referenz aus der Hersteller-Doku)

| Typ | MIDI-Message | Parameter | Richtung |
|---|---|---|---|
| **Buttons** | Note On #0..103 | push: vel 127, release: vel 0 | IN |
| **Button LEDs** | Note On #0..103 | vel 0..63: off, vel 64: flash, vel 65..127: on | OUT |
| **Fader** | CC 70..77 | value 0..127 (MIDI-Mode) | IN/OUT |
| **Fader** | Pitchwheel Ch 0..7 | 14-bit (-8192..+8191) (MC-Mode) | IN/OUT |
| **Fader Touch** | Note On #110..117 | touch: vel 127, release: vel 0 | IN |
| **Encoder** | CC 80..87 | absolute: 0..127, relative: inc=65, dec=1 | IN |
| **Encoder Rings** | CC 80..87 | value 0..127 | OUT |
| **Jog Wheel** | CC 88 | CW: 65, CCW: 1 | IN |
| **Meter LEDs** | CC 90..97 | value 0..127 | OUT |
| **Foot Controller** | CC 4 | value 0..127 | IN |
| **Foot Switch** | CC 64 (FS1), CC 67 (FS2) | push: 127, release: 0 | IN |
| **LCDs** | SysEx F0 00 20 32 dd 4C nn cc c1..c14 F7 | dd=DeviceID, nn=LCD#, cc=Farbe, c1..c14=ASCII | OUT |
| **Segment Display** | SysEx F0 00 20 32 dd 37 s1..s12 d1 d2 F7 | 7-Segment-Daten + Dots | OUT |

### Button Note-Nummern (MC-Mode)

| Button-Typ | Kanal 1 | Kanal 2 | ... | Kanal 8 | Formel |
|---|---|---|---|---|---|
| REC | 0 | 1 | ... | 7 | `0 * 8 + ch` |
| SOLO | 8 | 9 | ... | 15 | `1 * 8 + ch` |
| MUTE | 16 | 17 | ... | 23 | `2 * 8 + ch` |
| SELECT | 24 | 25 | ... | 31 | `3 * 8 + ch` |
| Encoder Press | 32 | 33 | ... | 39 | `32 + ch` |
| Fader Touch | 110 | 111 | ... | 117 | `110 + ch` |

Im C#-Code: `ButtonControl.NoteNumber = (int)buttonType * 8 + channel`.

### Encoder Ring CC-Wert Aufbau

```
Bits:  6    5-4     3-0
      LED   Mode    Position
       │     │        │
       │     │        └── 0–15 (Ring-Position)
       │     └────────── 0=Dot, 1=Pan, 2=Wrap, 3=Spread
       └──────────────── 0=aus, 1=Center-LED an
```

Im C#-Code: `EncoderControl.CalculateCcValue()`.

### LCD SysEx Aufbau

```
F0 00 20 32 dd 4C nn cc c1 c2 c3 c4 c5 c6 c7 c8 c9 c10 c11 c12 c13 c14 F7
│           │  │  │  │  └── c1..c7: obere Zeile, c8..c14: untere Zeile (ASCII)
│           │  │  │  └───── Farbbyte: Bits 0-2=Farbe, Bit 4=Invert oben, Bit 5=Invert unten
│           │  │  └──────── LCD-Nummer (0..7)
│           │  └─────────── 4C = LCD Command
│           └────────────── Device ID: 14=X-Touch, 15=X-Touch-Ext
└────────────────────────── Behringer Manufacturer SysEx Header
```

### Mackie Control SysEx (intern verwendet)

Parallel zum Behringer-Protokoll nutzt das Projekt auch den Mackie-Kompatibilitätsmodus:

```
F0 00 00 66 15 12 offset text... F7   → Display-Text (Mackie)
F0 00 00 66 15 72 c0 c1..c7 F7       → Display-Farben (Mackie)
F0 00 00 66 15 13 ...                 → Handshake Challenge
F0 00 00 66 15 14 ...                 → Handshake Response
```

Beide Formate werden vom `MidiMessageDecoder` erkannt und dekodiert.

## Nicht portierte Features

Folgende Features aus dem Python-Projekt sind noch nicht implementiert:

| Feature | Python-Datei | Status |
|---|---|---|
| Matrix-Modus | `XTouchVM.py` (MatrixMode) | Skelett im Python, nicht implementiert |
| Desktop/VR Shortcut-Funktionen | `XTouchVM.py` (desktop_mode, vr_mode) | Struktur vorhanden, Logik fehlt |
| Windows Toast-Benachrichtigungen | `audiomanager.pyw` (Notificator) | NuGet-Paket vorbereitet |
| Denoiser-Steuerung per Encoder | `XTouchVM.py` (Encoder 1 in Home-View) | Kann als `EncoderFunction` hinzugefugt werden (siehe [VOICEMEETER-API.md](VOICEMEETER-API.md)) |
| Dynamische Kanalzuweisung per Encoder 6/7 | `XTouchVM.py` | Nicht portiert |
| Handshake/Challenge-Response | `XTouchLib.py` | Code vorhanden in `MackieProtocol`, nicht aktiv |
| XTouchLib2 (alternatives Wrapper-Pattern) | `XTouchLib2.py` | In XTouchDevice integriert |

## Neu in C# (nicht im Python-Original)

| Feature | Datei | Beschreibung |
|---|---|---|
| **Encoder-Funktionsliste** | `EncoderControl.cs`, `EncoderFunction.cs` | Encoder schalten per Drücken zyklisch durch konfigurierbare Funktionen (HIGH/MID/LOW/PAN/GAIN). Jede Funktion hat eigenen Wertebereich, Schrittweite und merkt sich den Zustand. |
| **X-Touch Panel** | `XTouchPanelWindow.xaml(.cs)` | Vollständige interaktive Visualisierung der X-Touch Oberfläche (8 Strips + Main + Master Section mit Jog Wheel, Transport, Function Keys etc.) |
| **MIDI Debug Monitor** | `MidiDebugWindow.xaml(.cs)`, `MidiMessageDecoder.cs` | Echtzeit-Dekodierung und Anzeige aller MIDI-Nachrichten basierend auf der Behringer-Dokumentation |
