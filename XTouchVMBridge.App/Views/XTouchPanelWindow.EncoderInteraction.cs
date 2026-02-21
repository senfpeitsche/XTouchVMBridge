using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Hardware;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace XTouchVMBridge.App.Views;

public partial class XTouchPanelWindow
{
    private void OnEncoderClick(int ch)
    {
        if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
        {
            if (_device == null || ch >= _device.Channels.Count) return;
            var encoder = _device.Channels[ch].Encoder;
            if (!encoder.HasFunctions) return;

            var fn = encoder.CycleFunction();
            if (fn == null) return;

            if (_vm != null)
                fn.CurrentValue = _vm.GetParameter(fn.VmParameter);

            _bridge?.SuppressEncoderSync(ch, TimeSpan.FromSeconds(3));

            encoder.SyncRingToActiveFunction();
            _device.SetEncoderRing(ch, encoder.CalculateCcValue(), encoder.RingMode, encoder.RingLed);

            _device.SetDisplayText(ch, 0, fn.Name);
            _device.SetDisplayText(ch, 1, fn.FormatValue());
            return;
        }

        ShowEncoderDetail(ch);
    }

    private void OnEncoderMouseWheel(int ch, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (_device == null || ch >= _device.Channels.Count) return;
        var encoder = _device.Channels[ch].Encoder;
        if (!encoder.HasFunctions || encoder.ActiveFunction == null) return;

        double step = System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)
            ? 0.5 : 0.1;
        double delta = e.Delta > 0 ? step : -step;

        var fn = encoder.ActiveFunction;
        fn.CurrentValue = Math.Clamp(fn.CurrentValue + delta, fn.MinValue, fn.MaxValue);
        encoder.SyncRingToActiveFunction();

        _vm?.SetParameter(fn.VmParameter, (float)fn.CurrentValue);

        _bridge?.SuppressEncoderSync(ch, TimeSpan.FromSeconds(1));

        _device.SetEncoderRing(ch, encoder.CalculateCcValue(), encoder.RingMode, encoder.RingLed);

        _device.SetDisplayText(ch, 0, fn.Name);
        _device.SetDisplayText(ch, 1, fn.FormatValue());

        e.Handled = true;
    }

    private void UpdateEncoderRingVisual(int ch, EncoderControl encoder)
    {
        var indicator = _encoderRingIndicators[ch];
        var container = _encoderRingContainers[ch];
        if (indicator == null || container == null) return;

        double totalWidth = container.Width; // 48 px
        int pos = encoder.RingPosition;      // 0..10 ring steps
        double center = totalWidth / 2.0;    // midpoint at 24 px

        if (encoder.RingMode == XTouchEncoderRingMode.Wrap)
        {
            double fillWidth = (pos / 10.0) * totalWidth;
            indicator.Margin = new Thickness(0, 0, 0, 0);
            indicator.HorizontalAlignment = HorizontalAlignment.Left;
            indicator.Width = Math.Max(2, fillWidth);
        }
        else
        {
            double posPixel = (pos / 10.0) * totalWidth;

            if (pos == 5)
            {
                indicator.Margin = new Thickness(center - 1, 0, 0, 0);
                indicator.HorizontalAlignment = HorizontalAlignment.Left;
                indicator.Width = 2;
            }
            else if (pos < 5)
            {
                double left = posPixel;
                double width = center - posPixel;
                indicator.Margin = new Thickness(left, 0, 0, 0);
                indicator.HorizontalAlignment = HorizontalAlignment.Left;
                indicator.Width = Math.Max(2, width);
            }
            else
            {
                double width = posPixel - center;
                indicator.Margin = new Thickness(center, 0, 0, 0);
                indicator.HorizontalAlignment = HorizontalAlignment.Left;
                indicator.Width = Math.Max(2, width);
            }
        }
    }
}
