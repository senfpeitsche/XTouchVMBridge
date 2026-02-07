using System.Windows;
using System.Windows.Controls;
using XTouchVMBridge.Core.Hardware;

namespace XTouchVMBridge.App.Views;

/// <summary>
/// Fader-Maus-Interaktion: Strg+Klick zum Steuern, Doppelklick für 0 dB Reset.
/// </summary>
public partial class XTouchPanelWindow
{
    private void OnFaderMouseDown(int ch, System.Windows.Input.MouseButtonEventArgs e)
    {
        var now = DateTime.Now;

        // Doppelklick-Erkennung: Fader auf 0 dB setzen
        if (_lastFaderClickChannel == ch &&
            (now - _lastFaderClickTime).TotalMilliseconds < DoubleTapThresholdMs)
        {
            SetFaderTo0dB(ch);
            _lastFaderClickChannel = -1;
            e.Handled = true;
            return;
        }

        _lastFaderClickTime = now;
        _lastFaderClickChannel = ch;

        if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
        {
            _draggingFaderChannel = ch;
            var slider = _faderSliders[ch];

            SetFaderFromMousePosition(slider, ch, e.GetPosition(slider));

            if (e.Source is UIElement overlay)
                overlay.CaptureMouse();

            e.Handled = true;
            return;
        }

        ShowFaderDetail(ch);
    }

    private void SetFaderTo0dB(int ch)
    {
        if (_config == null || _vm == null) return;

        int vmCh = ResolveVmChannel(ch);
        if (!_config.Mappings.TryGetValue(vmCh, out var mapping) || mapping.Fader == null) return;

        double db = Math.Clamp(0.0, mapping.Fader.Min, mapping.Fader.Max);
        _vm.SetParameter(mapping.Fader.Parameter, (float)db);

        int position = FaderControl.DbToPosition(db);
        _faderSliders[ch].Value = position;
        _faderDbLabels[ch].Text = "0.0 dB";
    }

    private void OnFaderMouseMove(int ch, System.Windows.Input.MouseEventArgs e)
    {
        if (_draggingFaderChannel != ch) return;
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            OnFaderMouseUp(ch);
            return;
        }

        var slider = _faderSliders[ch];
        SetFaderFromMousePosition(slider, ch, e.GetPosition(slider));
    }

    private void OnFaderMouseUp(int ch)
    {
        if (_draggingFaderChannel != ch) return;
        _draggingFaderChannel = -1;

        var slider = _faderSliders[ch];
        if (slider.Parent is Grid faderHost && faderHost.Children.Count > 1 && faderHost.Children[1] is UIElement overlay)
            overlay.ReleaseMouseCapture();
    }

    private void SetFaderFromMousePosition(Slider slider, int ch, System.Windows.Point mousePos)
    {
        double ratio = 1.0 - Math.Clamp(mousePos.Y / slider.ActualHeight, 0.0, 1.0);
        double value = slider.Minimum + ratio * (slider.Maximum - slider.Minimum);
        int position = (int)Math.Clamp(value, slider.Minimum, slider.Maximum);

        slider.Value = position;

        if (_config == null || _vm == null) return;

        int vmCh = ResolveVmChannel(ch);
        if (!_config.Mappings.TryGetValue(vmCh, out var mapping) || mapping.Fader == null) return;

        double db = FaderControl.PositionToDb(position);
        db = Math.Clamp(db, mapping.Fader.Min, mapping.Fader.Max);
        _vm.SetParameter(mapping.Fader.Parameter, (float)db);

        _faderDbLabels[ch].Text = db <= -65 ? "-∞ dB" : $"{db:F1} dB";
    }

    private void OnFaderValueChanged(int ch, double newValue)
    {
        if (_draggingFaderChannel != ch) return;
        if (_config == null || _vm == null) return;

        int vmCh = ResolveVmChannel(ch);
        if (!_config.Mappings.TryGetValue(vmCh, out var mapping) || mapping.Fader == null) return;

        double db = FaderControl.PositionToDb((int)newValue);
        db = Math.Clamp(db, mapping.Fader.Min, mapping.Fader.Max);
        _vm.SetParameter(mapping.Fader.Parameter, (float)db);

        _faderDbLabels[ch].Text = db <= -65 ? "-∞ dB" : $"{db:F1} dB";
    }
}
