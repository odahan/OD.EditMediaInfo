using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace OD.EditMediaInfo.Gui;

/// <summary>Applies the app palette to the native Windows caption and border.</summary>
internal static class DarkWindowChrome
{
    private const int UseImmersiveDarkMode = 20;
    private const int BorderColor = 34;
    private const int CaptionColor = 35;
    private const int TextColor = 36;

    /// <summary>Applies the colors once the window has a native handle.</summary>
    public static void Attach(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            SetAttribute(handle, UseImmersiveDarkMode, 1);
            SetAttribute(handle, BorderColor, GetColor(window, "BorderBrush"));
            SetAttribute(handle, CaptionColor, GetColor(window, "WindowBrush"));
            SetAttribute(handle, TextColor, GetColor(window, "TextBrush"));
        };
    }

    private static int GetColor(Window window, string resource)
    {
        var color = ((SolidColorBrush)window.FindResource(resource)).Color;
        return color.R | (color.G << 8) | (color.B << 16);
    }

    private static void SetAttribute(IntPtr handle, int attribute, int value) =>
        _ = DwmSetWindowAttribute(handle, attribute, in value, sizeof(int));

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, in int value, int size);
}
