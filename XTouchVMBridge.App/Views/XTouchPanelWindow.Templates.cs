using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace XTouchVMBridge.App.Views;

/// <summary>
/// WPF Control-Templates für Buttons und Encoder-Knobs.
/// </summary>
public partial class XTouchPanelWindow
{
    private static ControlTemplate CreateRoundedButtonTemplate(double cornerRadius)
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
        { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush")
        { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness")
        { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
        border.SetValue(Border.PaddingProperty, new Thickness(2));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(cp);
        template.VisualTree = border;
        return template;
    }

    private static ControlTemplate CreateEncoderTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var grid = new FrameworkElementFactory(typeof(Grid));
        var outerEllipse = new FrameworkElementFactory(typeof(Ellipse));
        outerEllipse.SetValue(Ellipse.FillProperty, new SolidColorBrush(Color.FromRgb(34, 34, 34)));
        outerEllipse.SetValue(Ellipse.StrokeProperty, new SolidColorBrush(Color.FromRgb(85, 85, 85)));
        outerEllipse.SetValue(Ellipse.StrokeThicknessProperty, 2.0);
        grid.AppendChild(outerEllipse);
        var innerEllipse = new FrameworkElementFactory(typeof(Ellipse));
        innerEllipse.SetValue(Ellipse.FillProperty, new SolidColorBrush(Color.FromRgb(51, 51, 51)));
        innerEllipse.SetValue(Ellipse.WidthProperty, 20.0);
        innerEllipse.SetValue(Ellipse.HeightProperty, 20.0);
        grid.AppendChild(innerEllipse);
        template.VisualTree = grid;
        return template;
    }
}
