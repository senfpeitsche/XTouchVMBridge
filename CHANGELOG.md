[English](CHANGELOG.md) | [Deutsch](CHANGELOG-DE.md)

# Changelog

## [Unreleased] - 2026-02-19

### Channel REC special action
- **Fix**: REC special action (`Record Start/Stop (filename: channel + time)`) now reliably stops on second press
- **Change**: Stop is executed via `Recorder.Stop` instead of relying only on status reads
- **Change**: REC LED follows recorder state for the special action

### Mapping editor (channel buttons)
- **Change**: MQTT LED options are only visible when `ActionType = MqttPublish`
- **New**: Added hint text for REC special action that LED follows recorder state automatically

## [Unreleased] - 2026-02-17

### Installer (WiX/MSI)
- **New**: Added WiX v4 setup project `XTouchVMBridge.Setup`
- **New**: MSI build via `dotnet build XTouchVMBridge.Setup/XTouchVMBridge.Setup.wixproj -c Release`
- **New**: Automatic app publish + harvest of publish files into the MSI
- **New**: Start menu shortcut included in installer
- **Change**: Desktop shortcut is currently not included to keep MSI ICE validation clean for per-machine installation

## [Unreleased] - 2026-02-15

### MQTT: Mapping editor and runtime expanded
- **New**: Button mapping can now switch between `VM-Parameter` and `MQTT Publish`
- **New**: MQTT LED reception for channel buttons (`On/Off/Blink/Toggle` per topic+payload)
- **New**: Test functions in button mapping:
  - `Test Publish` for the currently configured MQTT publish
  - `Test LED` for the currently configured MQTT LED topic/payload

### Master buttons: MQTT expanded
- **New**: Master action type `MqttPublish` (Press/Release Payload, QoS, Retain)
- **New**: `LED per MQTT steuern` also in the master section for `MqttPublish`
- **New**: Master action type `SelectMqttDevice`:
  - selects an active MQTT target device (`DeviceId` + `CommandTopic`)
  - Selector LEDs show active selection (exactly one active)
- **New**: Master action type `MqttTransport`:
  - sends `play_pause/play/pause/stop/next/prev` to the active target device
  - optional payload override, QoS, retain
- **New**: Transport presets in the editor depending on the transport button:
  - Rewind -> `prev`, Forward -> `next`, Stop -> `stop`, Play -> `play_pause`, Record -> `pause`

### MQTT subscription
- **New**: MQTT client automatically subscribes to LED topics from:
  - Channel button `MqttLedReceive`
  - Master button `MqttLed*`

## [Unreleased] - 2025-02-09

### Master Button Actions: New action types
- **New**: Restart VM Audio Engine (`RestartAudioEngine`)
- **New**: Show VM window (`ShowVoicemeeter`) — brings Voicemeeter to the foreground via `Command.Show`
- **New**: Lock/unlock VM GUI (`LockGui`) — toggle `Command.Lock`
- **New**: Trigger Voicemeeter macro button (`TriggerMacroButton`) — triggers macro buttons 0–79 via `MacroButton_SetStatus`
- **New**: Macro button index input field (0-79) in the mapping editor

### LED feedback for master buttons
- **New**: Configurable LED feedback mode per master button action
  - **Blink** (default): LED flashes for 150ms as confirmation
  - **Toggle**: LED switches between on and off with each press (1st press = on, 2nd press = off)
  - **Blinking** (Continuous): LED flashes continuously via hardware flashing (Mackie Protocol Velocity 2), pressing again stops flashing
- **New**: LED feedback combo box in the mapping editor (visible when the action is active)
- **New**: LED feedback is stored in `config.json` (`"ledFeedback": "Blink"` / `"Toggle"` / `"Blinking"`)

### 7-segment display: Cycle button changed
- **Change**: Changed default cycle button from SMPTE (Note 113) to NAME/VALUE (Note 52).
- **Improvement**: LED feedback for the cycle button (LED on when not in time mode)
- **Fix**: Thread safety with `volatile` for display mode fields

### Voicemeeter API Extensions
- **New**: `IVoicemeeterService.ShowVoicemeeter()` — `Command.Show = 1`
- **New**: `IVoicemeeterService.LockGui(bool)` — set/unset `Command.Lock`
- **New**: `IVoicemeeterService.TriggerMacroButton(int)` — `MacroButton_SetStatus(index, 1, mode=2)`

### Code quality: File splitting into partial classes
- **Refactoring**: Large files divided into partial classes:
  - `XTouchPanelWindow` → `.MasterSection.cs`, `.ChannelStrips.cs`, `.MainFader.cs`, `.Templates.cs`, `.DetailPanels.cs`, `.EncoderInteraction.cs`, `.MappingEditor.cs`
  - `VoicemeeterBridge` → `.Sync.cs`, `.Callbacks.cs`
  - `XTouchDevice` → `.Input.cs`, `.Output.cs`
  - `MidiMessageDecoder` → `.SysEx.cs`

## [Previous] - 2025-02-06

### X-Touch Panel: Encoder control via mouse
- **New**: Ctrl+click on encoder cycled through assigned functions (identical to hardware presses)
- **New**: Mouse wheel on encoder changes the value of the active function (±1 step per notch)
- **New**: Ctrl+mouse wheel on encoder for rough control (±5 steps per notch)
- **New**: ToolTip on encoder knobs shows operating instructions

### X-Touch Panel: Button LED toggle for all buttons
- **New**: Ctrl+click on unassigned channel buttons (REC/SELECT) toggles the LED directly (On/Off)
- **New**: Ctrl+click on master buttons toggle the LED (On/Off) if no action is configured
- **Fix**: LED toggle now works reliably (own state memory in PanelView)
- **Fix**: VoicemeeterBridge no longer overwrites unassigned button LEDs to Off

### X-Touch initialization
- **Fix**: Master Section buttons (Notes 40-103) are reset on startup
  (previously only channel buttons Notes 0–31)

## [Previous] - 2025-02-04

### LCD displays
- **Fix**: LCD displays now work correctly
  - SysEx prefix changed from 0x15 (MCU Extended) to 0x14 (MCU Main).
  - Added handshake message upon initialization (`F0 00 00 66 14 13 00 F7`)

### 7 segment display
- **Fix**: 7-segment display now works correctly
  - Switched from Behringer SysEx to Mackie Control CC (CC 64-75).
  - Order corrected (right to left)
  - ASCII character conversion adjusted

### Encoder LED rings
- **Fix**: LED rings now show correct position
  - CC value mapping empirically determined and implemented
  - Position 0-10 instead of 0-15 (corresponds to the 11 usable LEDs)
  - Mode-specific value ranges: Dot=1-11, Pan=17-27, Wrap=33-43, Spread=49-54

### Encoder functions
- **Improvement**: Encoder functions are re-registered when changing views
- **Improvement**: Encoder rings are synchronized with every parameter update
- **Improvement**: Display shows parameter name (top) and value (bottom) when operating the encoder
- **Improvement**: Automatically switch back to channel name after 5 seconds

### Channel View Cycling
- **Change**: Flip button (Note 50) is now reserved for Channel View Cycling
- **Change**: Fader Bank Left/Right (Notes 46-47) are now freely assignable buttons

### Encoder 0 special logic
- **Removed**: Encoder 0 now behaves like all other encoders (no special treatment)

### Bug fixes
- **Fix**: Fixed MIDI Debug Window NullReferenceException when opening

### Test scripts
- **New**: `test_lcd.py` - LCD display protocol tests
- **New**: `test_segment.py` - 7-segment display protocol tests
- **New**: `test_encoder_ring.py` - Encoder LED ring protocol tests (CC 48-55 mapping)

### Documentation
- **Updated**: ARCHITECTURE.md with X-Touch MIDI protocol details
  - Encoder LED ring mapping (determined empirically)
  - 7 segment display CC protocol
  - LCD display SysEx format
  - Handshake message
