using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Hardware;
using XTouchVMBridge.Core.Logic;
using XTouchVMBridge.Core.Models;

namespace XTouchVMBridge.Voicemeeter.Services;

/// <summary>
/// Sync loop helpers for levels, state-driven LED updates, fader positions, and display content.
/// </summary>
public partial class VoicemeeterBridge
{
    private void UpdateLevels()
    {
        // Push meter values to hardware only when changed (or when a forced refresh is active).
        for (int xtCh = 0; xtCh < MidiDevice_ChannelCount(); xtCh++)
        {
            int vmCh = CurrentChannelMapping[xtCh];
            double level = _vm.GetLevel(vmCh);

            if (_forceLevelRefresh || Math.Abs(level - _levelCache[vmCh]) > 0.1)
            {
                _levelCache[vmCh] = level;
                int meterLevel = LevelMeterControl.DbToLevel(level);
                _xtouch.SetLevelMeter(xtCh, meterLevel);
            }
        }

        _forceLevelRefresh = false;
    }

    private void UpdateParameters()
    {
        // Pull the current Voicemeeter snapshot once per sync cycle.
        _vmState = _vm.GetCurrentState();
        bool anySoloActive = false;
        for (int strip = 0; strip < VoicemeeterState.StripCount; strip++)
        {
            if (_vmState.Solos[strip])
            {
                anySoloActive = true;
                break;
            }
        }

        var now = DateTime.UtcNow;

        bool anyFaderActive = false;
        for (int i = 0; i < MidiDevice_ChannelCount(); i++)
        {
            if (_xtouch.Channels[i].Fader.IsTouched ||
                (i < _faderProtectUntil.Length && _faderProtectUntil[i] > now))
            {
                anyFaderActive = true;
                break;
            }
        }
        if (_xtouch.IsMainFaderTouched || _faderProtectUntil[8] > now)
            anyFaderActive = true;

        for (int xtCh = 0; xtCh < MidiDevice_ChannelCount(); xtCh++)
        {
            int vmCh = CurrentChannelMapping[xtCh];
            var mapping = GetMapping(vmCh);

            double dbToSet;
            if (mapping?.Fader != null)
            {
                dbToSet = _vm.GetParameter(mapping.Fader.Parameter);
            }
            else
            {
                string prefix = vmCh < 8 ? $"Strip[{vmCh}]" : $"Bus[{vmCh - 8}]";
                dbToSet = _vm.GetParameter($"{prefix}.Gain");
            }

            // Do not write motor positions while any fader is touched/protected.
            if (!anyFaderActive)
            {
                _xtouch.SetFaderDb(xtCh, dbToSet);
            }

            if (_gainCacheInitialized && Math.Abs(dbToSet - _lastGainValues[xtCh]) > 0.05)
            {
                // Show changed gain briefly on scribble strip when the user is not touching the fader.
                bool isTouched = _xtouch.Channels[xtCh].Fader.IsTouched;
                if (!isTouched)
                {
                    string dbText = dbToSet <= -60 ? " -inf " : $"{dbToSet:F1}dB";
                    _xtouch.SetDisplayText(xtCh, 1, dbText);
                    _displayDbUntil[xtCh] = DateTime.UtcNow + TimeSpan.FromSeconds(3);
                }
            }
            _lastGainValues[xtCh] = dbToSet;

            foreach (XTouchButtonType btnType in Enum.GetValues<XTouchButtonType>())
            {
                var btnMap = GetButtonMapping(vmCh, btnType);
                if (btnMap != null)
                {
                    if (btnMap.ActionType == ButtonActionType.MqttPublish)
                        continue;
                    if (btnMap.MqttLedReceive?.Enabled == true)
                        continue;
                    if (string.IsNullOrWhiteSpace(btnMap.Parameter))
                        continue;

                    if (btnType == XTouchButtonType.Mute)
                    {
                        // Mute LED uses dedicated solo/mute visibility policy.
                        _xtouch.SetButtonLed(xtCh, btnType, MuteLedPolicy.Resolve(vmCh, _vmState, anySoloActive));
                        continue;
                    }

                    float val = string.Equals(
                        btnMap.Parameter,
                        ButtonMappingConfig.ChannelRecordActionParameter,
                        StringComparison.Ordinal)
                        ? (_isRecorderActive ? 1f : 0f)
                        : _vm.GetParameter(btnMap.Parameter);
                    _xtouch.SetButtonLed(xtCh, btnType,
                        val > 0.5f ? LedState.On : LedState.Off);
                }
            }

            if (!(xtCh < _displayEncoderUntil.Length && _displayEncoderUntil[xtCh] > DateTime.UtcNow))
                SyncEncoderRing(xtCh, vmCh);
        }

        SyncMainFader(anyFaderActive);

        if (!_gainCacheInitialized)
            _gainCacheInitialized = true;

        UpdateMasterButtonVmLedStates();
        UpdateDisplays();
    }
    private void UpdateMasterButtonVmLedStates()
    {
        foreach (var (note, action) in _config.MasterButtonActions)
        {
            if (action.ActionType != MasterButtonActionType.VmParameter)
                continue;
            if (action.VmLedSource != MasterVmLedSource.VoicemeeterState)
                continue;
            if (string.IsNullOrWhiteSpace(action.VmParameter))
                continue;

            float val = _vm.GetParameter(action.VmParameter);
            _xtouch.SetMasterButtonLed(note, val > 0.5f ? LedState.On : LedState.Off);
        }
    }

    private void SyncMainFader(bool anyFaderActive)
    {
        var currentView = ChannelViews[_currentViewIndex];
        if (!currentView.MainFaderChannel.HasValue) return;

        int vmCh = currentView.MainFaderChannel.Value;
        var mapping = GetMapping(vmCh);

        double db;
        if (mapping?.Fader != null)
        {
            db = _vm.GetParameter(mapping.Fader.Parameter);
        }
        else
        {
            string prefix = vmCh < 8 ? $"Strip[{vmCh}]" : $"Bus[{vmCh - 8}]";
            db = _vm.GetParameter($"{prefix}.Gain");
        }

        if (!anyFaderActive)
        {
            _xtouch.SetFaderDb(8, db);
        }

        _lastGainValues[8] = db;
    }

    private void UpdateDisplays()
    {
        // Update top/bottom text and per-channel display colors.
        var colors = new XTouchColor[8];
        var now = DateTime.UtcNow;

        for (int xtCh = 0; xtCh < MidiDevice_ChannelCount(); xtCh++)
        {
            int vmCh = CurrentChannelMapping[xtCh];

            bool encoderActive = xtCh < _displayEncoderUntil.Length && _displayEncoderUntil[xtCh] > now;

            var viewColor = ChannelViews[_currentViewIndex].GetChannelColor(xtCh);
            if (viewColor.HasValue)
                colors[xtCh] = viewColor.Value;
            else if (_config.Channels.TryGetValue(vmCh, out var chConfig))
                colors[xtCh] = chConfig.Color;
            else
                colors[xtCh] = XTouchColor.White;

            if (!encoderActive)
            {
                string vmLabel = GetVmLabel(vmCh);
                _xtouch.SetDisplayText(xtCh, 0, vmLabel);
            }

            bool showingDb = xtCh < _displayDbUntil.Length && _displayDbUntil[xtCh] > now;
            bool isTouched = _xtouch.Channels[xtCh].Fader.IsTouched;
            if (!showingDb && !isTouched && !encoderActive)
            {
                _xtouch.SetDisplayText(xtCh, 1, ChannelViews[_currentViewIndex].Name);
            }
        }

        _xtouch.SetAllDisplayColors(colors);
    }

    private void SyncEncoderRing(int xtCh, int vmCh)
    {
        var encoder = _xtouch.Channels[xtCh].Encoder;
        var fn = encoder.ActiveFunction;

        if (fn == null)
        {
            _xtouch.SetEncoderRing(xtCh, 0, XTouchEncoderRingMode.Dot, false);
            return;
        }

        // Keep ring value in sync with the currently selected encoder function parameter.
        float currentValue = _vm.GetParameter(fn.VmParameter);
        fn.CurrentValue = currentValue;

        encoder.SyncRingToActiveFunction();
        _xtouch.SetEncoderRing(xtCh, encoder.CalculateCcValue(), encoder.RingMode, encoder.RingLed);
    }
}

