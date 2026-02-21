using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XTouchVMBridge.App.Services;

public static class AppIconFactory
{
    private static readonly Lazy<Icon> TrayIconCache = new(CreateIconCore);
    private static readonly Lazy<ImageSource> WindowIconCache = new(CreateWindowIconCore);

    public static Icon CreateTrayIcon() => (Icon)TrayIconCache.Value.Clone();

    public static ImageSource CreateWindowIcon() => WindowIconCache.Value;

    private static Icon CreateIconCore()
    {
        using var bmp = new Bitmap(64, 64);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(System.Drawing.Color.FromArgb(30, 30, 30));

        using var font = new Font("Segoe UI", 22, System.Drawing.FontStyle.Bold);
        using var brush = new SolidBrush(System.Drawing.Color.FromArgb(0, 180, 255));
        var size = g.MeasureString("XV", font);
        float x = (64 - size.Width) / 2;
        float y = (64 - size.Height) / 2;
        g.DrawString("XV", font, brush, x, y);

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(hIcon);
            return (Icon)icon.Clone();
        }
        finally
        {
            _ = DestroyIcon(hIcon);
        }
    }

    private static ImageSource CreateWindowIconCore()
    {
        using var icon = CreateIconCore();
        var source = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        source.Freeze();
        return source;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
