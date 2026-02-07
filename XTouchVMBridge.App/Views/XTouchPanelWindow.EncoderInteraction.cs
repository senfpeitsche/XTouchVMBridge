using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Hardware;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace XTouchVMBridge.App.Views;

/// <summary>
/// Encoder-Interaktion: Klick (Strg+Klick → Funktion cyclen), Mausrad (Wert ändern),
/// Ring-Visualisierung (Pan/Wrap-Modi).
/// </summary>
public partial class XTouchPanelWindow
{
    /// <summary>
    /// Klick auf Encoder-Knob: Bei Strg → nächste Funktion durchschalten,
    /// sonst Detail-Panel anzeigen.
    /// </summary>
    private void OnEncoderClick(int ch)
    {
        if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
        {
            if (_device == null || ch >= _device.Channels.Count) return;
            var encoder = _device.Channels[ch].Encoder;
            if (!encoder.HasFunctions) return;

            // Zur nächsten Funktion cyclen
            var fn = encoder.CycleFunction();
            if (fn == null) return;

            // Aktuellen Wert aus Voicemeeter lesen
            if (_vm != null)
                fn.CurrentValue = _vm.GetParameter(fn.VmParameter);

            // Bridge-Sync unterdrücken damit der Wert nicht sofort überschrieben wird
            _bridge?.SuppressEncoderSync(ch, TimeSpan.FromSeconds(3));

            // Ring-Position synchronisieren
            encoder.SyncRingToActiveFunction();
            _device.SetEncoderRing(ch, encoder.CalculateCcValue(), encoder.RingMode, encoder.RingLed);

            // Display kurz aktualisieren (Name + Wert)
            _device.SetDisplayText(ch, 0, fn.Name);
            _device.SetDisplayText(ch, 1, fn.FormatValue());
            return;
        }

        ShowEncoderDetail(ch);
    }

    /// <summary>
    /// Mausrad auf Encoder-Knob: Wert der aktiven Funktion ändern.
    /// </summary>
    private void OnEncoderMouseWheel(int ch, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (_device == null || ch >= _device.Channels.Count) return;
        var encoder = _device.Channels[ch].Encoder;
        if (!encoder.HasFunctions || encoder.ActiveFunction == null) return;

        // Mausrad: feste dB-Schritte (unabhängig von Encoder-StepSize)
        // Normal: ±0.1 dB, Strg: ±0.5 dB
        double step = System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)
            ? 0.5 : 0.1;
        double delta = e.Delta > 0 ? step : -step;

        var fn = encoder.ActiveFunction;
        fn.CurrentValue = Math.Clamp(fn.CurrentValue + delta, fn.MinValue, fn.MaxValue);
        encoder.SyncRingToActiveFunction();

        // Wert an Voicemeeter senden
        _vm?.SetParameter(fn.VmParameter, (float)fn.CurrentValue);

        // Bridge-Sync unterdrücken damit der Wert nicht sofort überschrieben wird
        _bridge?.SuppressEncoderSync(ch, TimeSpan.FromSeconds(1));

        // Ring-Position am Gerät aktualisieren
        _device.SetEncoderRing(ch, encoder.CalculateCcValue(), encoder.RingMode, encoder.RingLed);

        // Display kurz aktualisieren (Name + Wert)
        _device.SetDisplayText(ch, 0, fn.Name);
        _device.SetDisplayText(ch, 1, fn.FormatValue());

        e.Handled = true;
    }

    /// <summary>
    /// Aktualisiert die visuelle Encoder-Ring-Anzeige basierend auf dem RingMode:
    /// - Pan/Dot/Spread: Strich in der Mitte bei Position 5, Balken von Mitte nach links/rechts
    /// - Wrap: Balken von links nach rechts (z.B. Gain)
    /// </summary>
    private void UpdateEncoderRingVisual(int ch, EncoderControl encoder)
    {
        var indicator = _encoderRingIndicators[ch];
        var container = _encoderRingContainers[ch];
        if (indicator == null || container == null) return;

        double totalWidth = container.Width; // 48
        int pos = encoder.RingPosition;      // 0..10
        double center = totalWidth / 2.0;    // 24

        if (encoder.RingMode == XTouchEncoderRingMode.Wrap)
        {
            // Wrap-Modus (Gain): Balken füllt von links nach rechts
            double fillWidth = (pos / 10.0) * totalWidth;
            indicator.Margin = new Thickness(0, 0, 0, 0);
            indicator.HorizontalAlignment = HorizontalAlignment.Left;
            indicator.Width = Math.Max(2, fillWidth);
        }
        else
        {
            // Pan/Dot/Spread: von der Mitte aus
            // Position 5 = Mitte → schmaler Strich
            // Position < 5 → Balken geht von Position nach Mitte (links)
            // Position > 5 → Balken geht von Mitte nach Position (rechts)
            double posPixel = (pos / 10.0) * totalWidth;

            if (pos == 5)
            {
                // Exakt Mitte: nur ein schmaler Strich
                indicator.Margin = new Thickness(center - 1, 0, 0, 0);
                indicator.HorizontalAlignment = HorizontalAlignment.Left;
                indicator.Width = 2;
            }
            else if (pos < 5)
            {
                // Links von der Mitte: Balken von posPixel bis center
                double left = posPixel;
                double width = center - posPixel;
                indicator.Margin = new Thickness(left, 0, 0, 0);
                indicator.HorizontalAlignment = HorizontalAlignment.Left;
                indicator.Width = Math.Max(2, width);
            }
            else
            {
                // Rechts von der Mitte: Balken von center bis posPixel
                double width = posPixel - center;
                indicator.Margin = new Thickness(center, 0, 0, 0);
                indicator.HorizontalAlignment = HorizontalAlignment.Left;
                indicator.Width = Math.Max(2, width);
            }
        }
    }
}
