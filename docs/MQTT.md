[English](MQTT.md) | [Deutsch](MQTT-DE.md)

# MQTT integration

This document describes the MQTT features in `XTouchVMBridge` and shows a recommended setup
for direct device control without Home Assistant.

## Overview

MQTT integration consists of three levels:

1. Global MQTT client (broker connection, publish/subscribe)
2. Channel buttons (per channel button: VM parameters or MQTT Publish, optional LED via MQTT)
3. Master buttons (MQTT Publish, Device Select, Transport, optional LED via MQTT for MQTT Publish)

## Global MQTT client

Configurable in the MQTT dialog:

- `enabled`, `host`, `port`, `useTls`
- Auth: `username`, `password`
- Publish Defaults: `publishTopic`, `publishQos`, `publishRetain`
- Subscriptions: `subscribeTopics`, `subscribeQos`

Addition: Topics from LED mappings are automatically subscribed to.

## Channel buttons

In the panel mapping editor per channel button:

- `ActionType = VmParameter` or `ActionType = MqttPublish`
- `MqttPublish`: `topic`, `payloadPressed`, `payloadReleased`, `qos`, `retain`
- `MqttLedReceive`: `topic`, payloads for `on`, `off`, `blink`, `toggle`

Notes:

- MQTT LED fields (`MqttLedReceive`) are only visible in the editor when `ActionType = MqttPublish`.
- For the REC special action `Record Start/Stop (filename: channel + time)`, LED follows recorder state.
- First press starts recording, second press stops recording.

Test functions in the editor:

- `Test Publish`: sends the configured press payload
- `Test LED`: sends the selected LED test payload (`On/Off/Blink/Toggle`)

## Master buttons

MQTT-related action types are available in the master mapping editor:

- `MqttPublish`
- `SelectMqttDevice`
- `MqttTransport`

Additionally (non-MQTT action):
- `VmParameter` supports `vmLedSource`:
  - `ManualFeedback` (default): LED follows `ledFeedback`
  - `VoicemeeterState`: LED follows the real VM parameter state (On/Off)

### 1) MqttPublish

Fields:

- `mqttTopic`
- `mqttPayloadPressed`, `mqttPayloadReleased`
- `mqttQos`, `mqttRetain`

Optional:

- `LED per MQTT steuern` (only visible with `MqttPublish`)
- `mqttLedTopic`, payloads for `on/off/blink/toggle`

### 2) SelectMqttDevice

Fields:

- `mqttDeviceId`
- `mqttDeviceCommandTopic`

Behavior:

- Press selects the target device as active
- pressing the same selector again deactivates the target
- only one selector is active, selector LED shows the active status

### 3) MqttTransport

Fields:

- `mqttTransportCommand` (`play_pause`, `play`, `pause`, `stop`, `next`, `prev`)
- optional `mqttPayloadPressed` as payload override
- `mqttQos`, `mqttRetain`

Behavior:

- sends to the currently active target device from `SelectMqttDevice`
- no transport command is sent without an active selection

Editor presets for typical transport buttons:

- Note `91` (Rewind) -> `prev`
- Grade `92` (Forward) -> `next`
- Note `93` (Stop) -> `stop`
- Note `94` (Play) -> `play_pause`
- Note `95` (Record) -> `pause`

## Recommended topic schema (without Home Assistant)

For two target devices:

- `media/deviceA/cmd`
- `media/deviceB/cmd`

Payloads:

- `play_pause`, `play`, `pause`, `stop`, `next`, `prev`

Optional for LED return channel:

- `media/deviceA/led`
- `media/deviceB/led`

with payloads:

- `on`, `off`, `blink`, `toggle`

## Practical example DAT/CD (like in your company)

Broker/User:

- Host: `10.5.0.240`
- User: `mqtt`

Topics:

- DAT: `Remote/DAT`
- CD: `Remote/CD`

Transport payloads per device:

- DAT: `DATPlay`, `DATStop`, `DATFF`, `DATRew`
- CD: `CDPlay`, `CDStop`, `CDFF`, `CDRew`

Example CLI (corresponds to your current control):
```bash
mosquitto_pub -h 10.5.0.240 -u mqtt -t Remote/DAT -m DATFF
```
Mapping idea in the editor:

- `SelectMqttDevice` for DAT with `mqttDeviceId = DAT`, `mqttDeviceCommandTopic = Remote/DAT`
- `SelectMqttDevice` for CD with `mqttDeviceId = CD`, `mqttDeviceCommandTopic = Remote/CD`
- Set `MqttTransport` to Play/Stop/FF/Rew
- set `mqttPayloadPressed` per transport button:
  - Play: `Play`
  - Stop: `Stop`
  - FF: `FF`
  - Rew: `Rew`

Note:

- With `SelectMqttDevice + MqttTransport` the payload remains identical and only the topic changes (e.g. `Remote/DAT` vs. `Remote/CD`).
- If your target device absolutely requires prefix payloads such as `DATFF` / `CDFF`, this cannot currently be dynamically mapped with a common transport button set.
- Workaround: configure your own transport buttons as `MqttPublish` for each device (fixed payloads per button).

## Example config (excerpt)
```json
{
  "masterButtonActions": {
    "84": {
      "actionType": "SelectMqttDevice",
      "mqttDeviceId": "deviceA",
      "mqttDeviceCommandTopic": "media/deviceA/cmd"
    },
    "85": {
      "actionType": "SelectMqttDevice",
      "mqttDeviceId": "deviceB",
      "mqttDeviceCommandTopic": "media/deviceB/cmd"
    },
    "91": {
      "actionType": "MqttTransport",
      "mqttTransportCommand": "prev",
      "mqttQos": 0,
      "mqttRetain": false
    },
    "92": {
      "actionType": "MqttTransport",
      "mqttTransportCommand": "next",
      "mqttQos": 0,
      "mqttRetain": false
    },
    "93": {
      "actionType": "MqttTransport",
      "mqttTransportCommand": "stop",
      "mqttQos": 0,
      "mqttRetain": false
    },
    "94": {
      "actionType": "MqttTransport",
      "mqttTransportCommand": "play_pause",
      "mqttQos": 0,
      "mqttRetain": false
    }
  }
}
```
## Troubleshooting

- No response to MQTT:
  - Activate MQTT in dialog (`enabled = true`)
  - Check broker connection with `Testen`
  - Compare topic exactly (including upper/lower case if necessary)
- LED does not respond:
  - correct LED topic/payloads set
  - LED mapping saved
  - for master: LED-per-MQTT only available with promotion `MqttPublish`
- Transport doesn't send anything:
  - first press a `SelectMqttDevice` button (active target must be set)
