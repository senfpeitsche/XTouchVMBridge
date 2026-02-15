[English](MIGRATION.md) | [Deutsch](MIGRATION-DE.md)

# Migration: Python → C#

This document describes the mapping of Python files, classes and concepts
to the corresponding C# implementations.

## File mapping

| Python file | C# equivalent | Project |
|---|---|---|
| `XTouchLibTypes.py` | `Core/Enums/` (XTouchColor, XTouchButtonType, LedState, XTouchEncoderRingMode) | Core |
| `XTouchLibTypes.py` (XTouchState) | `Core/Models/XTouchChannel.cs` + `Core/Hardware/*` | Core |
| `XTouchLib.py` (XTouch) | `Midi/XTouch/XTouchDevice.cs` | Midi |
| `XTouchLib.py` (protocol constants) | `Midi/XTouch/MackieProtocol.cs` | Midi |
| `XTouchLib2.py` | Integrated into `XTouchDevice` (not a separate module) | Midi |
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
| `audiomanager.pyw` (LogWindow) | `App/Views/LogWindow.xaml` + `.xaml.cs` | App |
| `audiomanager.pyw` (Notifier) ​​| Not ported yet (can be done via toast notification package) | — |
| `islocked.py` | `App/Services/ScreenLockDetector.cs` | App |
| — (new) | `Core/Hardware/EncoderFunction.cs` | Core |
| — (new) | `Core/Models/MasterButtonActionConfig.cs` | Core |
| — (new) | `Core/Events/MasterButtonEventArgs.cs` | Core |
| — (new) | `Midi/XTouch/MidiMessageDecoder.cs` | Midi |
| — (new) | `App/Services/MasterButtonActionService.cs` | App |
| — (new) | `App/Services/SegmentDisplayService.cs` | App |
| — (new) | `App/Views/MidiDebugWindow.xaml` + `.xaml.cs` | App |
| — (new) | `App/Views/XTouchPanelWindow.xaml` + `.xaml.cs` | App |

## Concept Mapping: Python → C#

### Threading

| Python | C# |
|---|---|
| `threading.Thread(target=fn, daemon=True)` | `BackgroundService` / `Task.Run` with `CancellationToken` |
| `self.running = False` (stop flag) | `CancellationToken` / `stoppingToken.IsCancellationRequested` |
| `time.sleep(0.1)` | `await Task.Delay(100, stoppingToken)` |
| Thread + while loop | `BackgroundService.ExecuteAsync()` |

### Callbacks → Events

| Python | C# |
|---|---|
| `fader_callback=fn` (constructor parameter) | `event EventHandler<FaderEventArgs> FaderChanged` |
| `self.callback(channel, db, pos)` | `FaderChanged?.Invoke(this, new FaderEventArgs(ch, pos, db))` |
| `change_callback(fader=fn)` | Subscribe to event: `device.FaderChanged += OnFader;` |

### State management

| Python | C# |
|---|---|
| `XTouchState` with properties + validation | `XTouchChannel` with `HardwareControlBase` controls |
| `XTouchStateUnchecked` (without validation) | Not ported — `XTouchChannel` always validates |
| `@property` + `@setter` | C# Properties with `get/set` |
| `__eq__` / `copy()` | `record` types where necessary |

### Configuration

| Python | C# |
|---|---|
| `json.load/dump` | `System.Text.Json.JsonSerializer` |
| `config['channels']['0']` (dict access) | `config.Channels[0]` (typed: `ChannelConfig`) |
| Manual validation in `load_config()` | `ConfigurationService.ValidateConfig()` |

### Logging

| Python | C# |
|---|---|
| `logging.getLogger(__name__)` | `ILogger<T>` via Dependency Injection |
| `ExceptionLoggingMeta` (Metaclass) | Not ported — `try/catch` to `BackgroundService` is enough |
| `coloredlogs` (Terminal Output) | `Serilog.Sinks.Console` |
| `logfile.log` (File) | `Serilog.Sinks.File` (Rolling, 7 days) |

### MIDI library

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

### Voicemeeter API

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
| `vm.ldirty` | Not directly available -- levels are read via polling |
| `vm.command.restart()` | `VoicemeeterRemote.SetParameterFloat("Command.Restart", 1.0f)` |
| `vm.apply({...})` | Not ported -- set parameters individually |

For a complete reference of all available parameters see [VOICEMEETER-API.md](VOICEMEETER-API.md).

### UI frameworks

| Python | C# |
|---|---|
| `pystray.Icon` | `Hardcodet.Wpf.TaskbarNotification.TaskbarIcon` |
| `customtkinter.CTk` | WPF (`Window`) |
| `CTkTextbox` | `TextBox` (WPF) |
| `win11toast` | Not yet ported — `Microsoft.Toolkit.Uwp.Notifications` planned |
| `PIL.Image` (icon creation) | `System.Drawing.Bitmap` |

## MIDI protocol reference

The X-Touch Extender communicates via the Mackie Control Extended protocol.
All Magic Numbers are centralized in `Midi/XTouch/MackieProtocol.cs`.

The official Behringer documentation is available as a PDF in the project:
`Document_BE_X-TOUCH-X-TOUCH-EXTENDER-MIDI-Mode-Implementation.pdf`

### Message types (reference from the manufacturer documentation)

| Type | MIDI message | Parameters | Direction |
|---|---|---|---|
| **Buttons** | Note On #0..103 | push: vel 127, release: vel 0 | IN |
| **Button LEDs** | Note On #0..103 | vel 0..63: off, vel 64: flash, vel 65..127: on | OUT |
| **Fader** | CC 70..77 | value 0..127 (MIDI mode) | IN/OUT |
| **Fader** | Pitch wheel Ch 0..7 | 14-bit (-8192..+8191) (MC mode) | IN/OUT |
| **Fader Touch** | Note On #110..117 | touch: vel 127, release: vel 0 | IN |
| **Encoder** | CC 80..87 | absolute: 0..127, relative: inc=65, dec=1 | IN |
| **Encoder Rings** | CC 80..87 | value 0..127 | OUT |
| **Jog Wheel** | CC 88 | CW: 65, CCW: 1 | IN |
| **Meter LEDs** | CC 90..97 | value 0..127 | OUT |
| **Foot Controller** | CC4 | value 0..127 | IN |
| **Foot Switch** | CC 64 (FS1), CC 67 ​​(FS2) | push: 127, release: 0 | IN |
| **LCDs** | SysEx F0 00 20 32 dd 4C nn cc c1..c14 F7 | dd=DeviceID, nn=LCD#, cc=color, c1..c14=ASCII | OUT |
| **Segment Display** | SysEx F0 00 20 32 dd 37 s1..s12 d1 d2 F7 | 7 segment data + dots (dd=DeviceID) | OUT |

### Button note numbers (MC mode)

| Button type | Channel 1 | Channel 2 | ... | Channel 8 | Formula |
|---|---|---|---|---|---|
| REC | 0 | 1 | ... | 7 | `0 * 8 + ch` |
| SOLO | 8 | 9 | ... | 15 | `1 * 8 + ch` |
| MUTE | 16 | 17 | ... | 23 | `2 * 8 + ch` |
| SELECT | 24 | 25 | ... | 31 | `3 * 8 + ch` |
| Encoder Press | 32 | 33 | ... | 39 | `32 + ch` |
| Fader Touch | 110 | 111 | ... | 117 | `110 + ch` |

In C# code: `ButtonControl.NoteNumber = (int)buttonType * 8 + channel`.

### Encoder Ring CC value structure
```
Bits:  6    5-4     3-0
      LED   Mode    Position
       │     │        │
       │     │        └── 0–15 (Ring-Position)
       │     └────────── 0=Dot, 1=Pan, 2=Wrap, 3=Spread
       └──────────────── 0=aus, 1=Center-LED an
```
In C# code: `EncoderControl.CalculateCcValue()`.

### LCD SysEx structure
```
F0 00 20 32 dd 4C nn cc c1 c2 c3 c4 c5 c6 c7 c8 c9 c10 c11 c12 c13 c14 F7
│           │  │  │  │  └── c1..c7: obere Zeile, c8..c14: untere Zeile (ASCII)
│           │  │  │  └───── Farbbyte: Bits 0-2=Farbe, Bit 4=Invert oben, Bit 5=Invert unten
│           │  │  └──────── LCD-Nummer (0..7)
│           │  └─────────── 4C = LCD Command
│           └────────────── Device ID: 14=X-Touch, 15=X-Touch-Ext
└────────────────────────── Behringer Manufacturer SysEx Header
```
### 7-segment display SysEx structure
```
F0 00 20 32 dd 37 s1 s2 s3 s4 s5 s6 s7 s8 s9 s10 s11 s12 d1 d2 F7
│           │  │  └── s1..s12: 7-Segment-Bitmuster (je 7 Bits: a-g)
│           │  └───── 37 = Segment Display Command
│           └──────── Device ID: 14=X-Touch, 15=X-Touch-Ext
└──────────────────── Behringer Manufacturer SysEx Header
```
Segment bits: `bit 0=a(oben), bit 1=b(rechts oben), bit 2=c(rechts unten), bit 3=d(unten), bit 4=e(links unten), bit 5=f(links oben), bit 6=g(mitte)`.

Dot bytes: `d1` = Dots for Display 1-7 (bit 0=Display 1, ..., bit 6=Display 7), `d2` = Dots for Display 8-12.

In C# code: `MackieProtocol.BuildSegmentDisplayMessage()`, `MackieProtocol.TextToSegments()`, `XTouchDevice.SetSegmentDisplay()`.

### Mackie Control SysEx (used internally)

In parallel with the Behringer protocol, the project also uses the Mackie compatibility mode:
```
F0 00 00 66 15 12 offset text... F7   → Display-Text (Mackie)
F0 00 00 66 15 72 c0 c1..c7 F7       → Display-Farben (Mackie)
F0 00 00 66 15 13 ...                 → Handshake Challenge
F0 00 00 66 15 14 ...                 → Handshake Response
```
Both formats are recognized and decoded by the `MidiMessageDecoder`.

## Unported features

The following features from the Python project are not yet implemented:

| Feature | Python file | Status |
|---|---|---|
| Matrix mode | `XTouchVM.py` (MatrixMode) | Skeleton in Python, not implemented |
| Desktop/VR Shortcut Features | `XTouchVM.py` (desktop_mode, vr_mode) | Structure present, logic missing |
| Windows Toast Notifications | `audiomanager.pyw` (Notifier) ​​| NuGet package prepared |
| Denoiser control via encoder | `XTouchVM.py` (Encoder 1 in Home View) | Can be added as `EncoderFunction` (see [VOICEMEETER-API.md](VOICEMEETER-API.md)) |
| Dynamic channel assignment via encoder 6/7 | `XTouchVM.py` | Not ported |
| Handshake/challenge response | `XTouchLib.py` | Code present in `MackieProtocol`, not active |
| XTouchLib2 (alternative wrapper pattern) | `XTouchLib2.py` | Integrated into XTouchDevice |

## New in C# (not in Python original)

| Feature | File | Description |
|---|---|---|
| **Encoder Function List** | `EncoderControl.cs`, `EncoderFunction.cs` | Encoders switch cyclically through configurable functions (HIGH/MID/LOW/PAN/GAIN) when pressed. Each function has its own value range, step size and remembers the state. |
| **X-Touch Panel** | `XTouchPanelWindow.xaml(.cs)` | Complete interactive visualization of the X-Touch interface (8 strips + main + master section with jog wheel, transport, function keys etc.) |
| **MIDI Debug Monitor** | `MidiDebugWindow.xaml(.cs)`, `MidiMessageDecoder.cs` | Real-time decoding and display of all MIDI messages based on Behringer documentation |
| **X-Touch device selection** | `TrayIconService.cs`, `XTouchDevice.cs` | Dynamic device list in the tray menu, support for X-Touch and X-Touch Extender |
| **Auto Reconnect** | `AudioDeviceMonitorService.cs` | Automatic reconnection every 5 seconds when X-Touch is disconnected |
| **Connection Status** | `TrayIconService.cs`, `IMidiDevice.cs` | Tooltip + menu show connection status, `ConnectionStateChanged` event |
| **Master Button Actions** | `MasterButtonActionService.cs`, `MasterButtonActionConfig.cs` | F1-F8 etc. configurable for program start, key combinations, text, VM parameter toggle. Editor integrated in the X-Touch panel. |
| **7-segment display** | `SegmentDisplayService.cs`, `MackieProtocol.cs` | Timecode display shows time/date/memory. SMPTE button cycled modes. Full 7-segment font, Behringer SysEx protocol. |
