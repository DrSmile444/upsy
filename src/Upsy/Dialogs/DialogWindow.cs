using System.Windows;
using System.Windows.Input;
using Upsy.Flyout;
using Upsy.Infrastructure;

namespace Upsy.Dialogs;

/// <summary>
/// Base of upsy's small windows (sign-in, Add station, confirmations): Windows 11 frame drawn in XAML with the flyout
/// tokens, centred, themed with the apps theme, draggable by its title bar, Escape closes.
/// </summary>
public class DialogWindow : Window
{
    public DialogWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        Topmost = true;
        UseLayoutRounding = true;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 14;
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/upsy;component/Assets/upsy.ico"));
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    /// <summary>Applies the theme tokens after the XAML has set <see cref="FrameworkElement.Resources"/>, which replaces the dictionary.</summary>
    protected override void OnInitialized(EventArgs e)
    {
        (WindowsTheme.AppsAreLight ? Palette.Light : Palette.Dark).Apply(Resources);
        base.OnInitialized(e);
    }

    /// <summary>Hook for the title bar: drag the window.</summary>
    protected void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    protected void OnCloseButton(object sender, RoutedEventArgs e) => Close();
}
