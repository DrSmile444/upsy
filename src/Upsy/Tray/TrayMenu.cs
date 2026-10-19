using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using Upsy.Flyout;

namespace Upsy.Tray;

/// <summary>A Windows 11 style context menu (TrayMenu.xaml) that can open from the tray icon or from a flyout button.</summary>
public sealed partial class TrayMenu
{
    private static readonly ResourceDictionary Styles = new() { Source = new Uri("/upsy;component/Tray/TrayMenu.xaml", UriKind.Relative) };

    // A tray icon has no WPF window: the menu needs an active window of ours, or it would not close on an outside click.
    private readonly Window _host = new()
    {
        WindowStyle = WindowStyle.None,
        AllowsTransparency = true,
        Background = System.Windows.Media.Brushes.Transparent,
        ShowInTaskbar = false,
        ShowActivated = true,
        Topmost = true,
        Width = 1,
        Height = 1,
    };

    private ContextMenu? _open;

    public bool IsOpen => _open?.IsOpen == true;

    /// <summary>Opens at the mouse pointer (right-click on the tray icon).</summary>
    public void ShowAtCursor(IEnumerable<Control> items, bool light)
    {
        var p = System.Windows.Forms.Cursor.Position;
        _host.Left = p.X;
        _host.Top = p.Y;
        _host.Show();
        var source = PresentationSource.FromVisual(_host);
        if (source?.CompositionTarget is { } target)
        {
            var dip = target.TransformFromDevice.Transform(new Point(p.X, p.Y));
            _host.Left = dip.X;
            _host.Top = dip.Y;
        }

        _host.Activate();
        _ = SetForegroundWindow(new WindowInteropHelper(_host).Handle);
        Open(items, light, _host, PlacementMode.MousePoint, () => _host.Hide());
    }

    /// <summary>Opens above <paramref name="anchor"/> (More button in the flyout).</summary>
    public void ShowAbove(UIElement anchor, IEnumerable<Control> items, bool light) =>
        Open(items, light, anchor, PlacementMode.Top, null);

    public static MenuItem Item(string header, string glyph, Action onClick, bool isChecked = false, string? meta = null, bool bold = false)
    {
        var item = new MenuItem
        {
            Header = header,
            Tag = glyph,
            IsCheckable = false,
            IsChecked = isChecked,
            InputGestureText = meta ?? string.Empty,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        };
        item.Click += (_, _) => onClick();
        return item;
    }

    public static MenuItem Title(string text) => new() { Header = text, IsEnabled = false, Tag = string.Empty };

    /// <summary>Creates the styled menu without opening it (also used by <c>upsy --render</c>).</summary>
    public static ContextMenu Build(IEnumerable<Control> items, bool light)
    {
        ArgumentNullException.ThrowIfNull(items);
        var menu = new ContextMenu { Style = (Style)Styles["UpsyContextMenu"] };
        menu.Resources.MergedDictionaries.Add(Styles);
        (light ? Palette.Light : Palette.Dark).Apply(menu.Resources);
        foreach (var item in items)
        {
            menu.Items.Add(item);
        }

        return menu;
    }

    private void Open(IEnumerable<Control> items, bool light, UIElement target, PlacementMode placement, Action? onClosed)
    {
        if (_open is { IsOpen: true })
        {
            _open.IsOpen = false;
        }

        var menu = Build(items, light);
        menu.PlacementTarget = target;
        menu.Placement = placement;
        menu.Closed += (_, _) => onClosed?.Invoke();
        _open = menu;
        menu.IsOpen = true;
    }

    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hwnd);
}
