using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WatermarkApp;

public partial class AppMessageBox : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private AppMessageBox(string message, string title)
    {
        InitializeComponent();
        Title = title;
        TxtMessage.Text = message;

        try
        {
            this.SourceInitialized += (s, e) =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                int useImmersiveDarkMode = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int));
            };
        }
        catch { }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    public static void Show(string message)
    {
        Show(message, "Message", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public static void Show(string message, string title, MessageBoxButton button, MessageBoxImage icon)
    {
        var box = new AppMessageBox(message, title);
        if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            box.Owner = Application.Current.MainWindow;
        }
        box.ShowDialog();
    }
}