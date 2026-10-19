using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Upsy.Core.Presentation;

namespace Upsy.Flyout;

/// <summary>The left-click flyout: one <see cref="StationView"/> per station, the tray station first. Layout and tokens follow DESIGN.md.</summary>
public partial class FlyoutWindow : Window
{
    private readonly Dictionary<string, StationView> _views = [];
    private bool? _themeIsLight;
    private System.Drawing.Point? _anchor;
    private string? _singleId;
    private bool _singlePaused;

    public FlyoutWindow()
    {
        InitializeComponent();
        Deactivated += (_, _) => Hide();
        SizeChanged += (_, _) =>
        {
            if (IsVisible)
            {
                Reposition();
            }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Hide();
            }
        };
    }

    /// <summary>Pause or resume asked from the footer (one station) for this station id.</summary>
    public event EventHandler<string>? PauseToggleRequested;

    public event EventHandler<(string Id, NoticeAction Action)>? NoticeRequested;

    public event EventHandler<(string Id, UIElement Anchor)>? StationMoreRequested;

    public event EventHandler? MoreRequested;

    public event EventHandler? AddStationRequested;

    /// <summary>Where the app menu opens from.</summary>
    public UIElement MoreAnchor => MoreButton;

    public void ApplyTheme(bool light)
    {
        if (_themeIsLight == light)
        {
            return;
        }

        _themeIsLight = light;
        (light ? Palette.Light : Palette.Dark).Apply(Resources);
    }

    /// <summary>Shows these stations in this order, keeping each station's Details open or closed across updates.</summary>
    public void Render(IReadOnlyList<StationSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        var t = _themeIsLight == false ? Palette.Dark : Palette.Light;
        foreach (var gone in _views.Keys.Except(sections.Select(s => s.Id)).ToList())
        {
            _views.Remove(gone);
        }

        Stations.Children.Clear();
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            if (!_views.TryGetValue(section.Id, out var view))
            {
                view = new StationView(section.Id);
                view.NoticeRequested += (sender, action) => NoticeRequested?.Invoke(this, (((StationView)sender!).Id, action));
                view.MoreRequested += (sender, _) => StationMoreRequested?.Invoke(this, (((StationView)sender!).Id, ((StationView)sender!).MoreAnchor));
                _views[section.Id] = view;
            }

            view.ShowSeparator = i > 0;
            view.Render(section, t);
            Stations.Children.Add(view);
        }

        // One station: the footer pauses it, as before. Several: each section has its own More; the footer adds stations.
        var single = sections.Count == 1 ? sections[0] : null;
        _singleId = single?.Id;
        _singlePaused = single?.Snapshot.Status == Core.Link.LinkStatus.Paused;
        PauseButton.Visibility = single is null ? Visibility.Collapsed : Visibility.Visible;
        PauseButton.Content = _singlePaused ? "\uE768" : "\uE769";
        var pauseName = _singlePaused ? "Resume connection" : "Pause for 30 minutes";
        PauseButton.ToolTip = pauseName;
        System.Windows.Automation.AutomationProperties.SetName(PauseButton, pauseName);
        AddButton.Visibility = sections.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Opens the Details of every station (render previews).</summary>
    internal void ExpandDetails(string id)
    {
        if (_views.TryGetValue(id, out var view))
        {
            view.DetailsOpen = true;
        }
    }

    /// <summary>Shows the window above the taskbar, horizontally near <paramref name="anchor"/> (screen pixels).</summary>
    public void ShowNear(System.Drawing.Point anchor)
    {
        _anchor = anchor;
        Show();
        UpdateLayout();
        Reposition();
        Activate();
        // Focus the window, not a control: the focus ring appears only once the user presses Tab, as in native flyouts.
        Keyboard.Focus(this);
    }

    /// <summary>
    /// Keeps the flyout glued to the taskbar edge. Runs on every size change, so expanding Details grows the
    /// window away from the taskbar instead of under it, and collapsing brings it back down.
    /// </summary>
    private void Reposition()
    {
        if (_anchor is not { } anchor)
        {
            return;
        }

        var source = PresentationSource.FromVisual(this);
        var toDip = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var screen = System.Windows.Forms.Screen.FromPoint(anchor);
        var work = screen.WorkingArea;
        var bounds = screen.Bounds;
        var a = toDip.Transform(new Point(anchor.X, anchor.Y));
        var wa = new Rect(toDip.Transform(new Point(work.Left, work.Top)), toDip.Transform(new Point(work.Right, work.Bottom)));
        MaxHeight = wa.Height;
        var w = ActualWidth;
        var h = ActualHeight;
        const double gap = -4; // the window carries a 16 px shadow margin

        double left;
        double top;
        if (work.Bottom < bounds.Bottom)
        {
            left = a.X - (w / 2);
            top = wa.Bottom - h - gap;
        }
        else if (work.Top > bounds.Top)
        {
            left = a.X - (w / 2);
            top = wa.Top + gap;
        }
        else if (work.Left > bounds.Left)
        {
            left = wa.Left + gap;
            top = a.Y - (h / 2);
        }
        else
        {
            left = wa.Right - w - gap;
            top = a.Y - (h / 2);
        }

        Left = Math.Clamp(left, wa.Left, Math.Max(wa.Left, wa.Right - w));
        Top = Math.Clamp(top, wa.Top, Math.Max(wa.Top, wa.Bottom - h));
    }

    private void OnPauseButton(object sender, RoutedEventArgs e)
    {
        if (_singleId is { } id)
        {
            PauseToggleRequested?.Invoke(this, id);
        }
    }

    private void OnAddButton(object sender, RoutedEventArgs e) => AddStationRequested?.Invoke(this, EventArgs.Empty);

    private void OnMoreButton(object sender, RoutedEventArgs e) => MoreRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>Windows 11 tokens from DESIGN.md.</summary>
internal sealed record Palette(
    Brush Flyout, Brush FlyoutStroke, Brush Card, Brush CardStroke, Brush Ink, Brush Ink2, Brush Ink3, Brush Divider,
    Brush SubtleHover, Brush SubtlePressed, Brush FooterTint, Brush Accent, Brush OnAccent, Brush AccentTint,
    Brush Ok, Brush Caution, Brush Critical, Brush Glyph, Brush GlyphOn)
{
    public static readonly Palette Light = new(
        B("#F9F9F9"), B("#1A000000"), B("#FFFFFF"), B("#0F000000"), B("#1B1B1B"), B("#5C5C5C"), B("#8A8A8A"), B("#14000000"),
        B("#0A000000"), B("#06000000"), B("#0A7F7F7F"), B("#005FB8"), B("#FFFFFF"), B("#29005FB8"),
        B("#0F7B0F"), B("#9D5D00"), B("#C42B1C"), B("#5C5C5C"), B("#1B1B1B"));

    public static readonly Palette Dark = new(
        B("#2C2C2C"), B("#14FFFFFF"), B("#0DFFFFFF"), B("#0FFFFFFF"), B("#FFFFFF"), B("#C8C8C8"), B("#9D9D9D"), B("#14FFFFFF"),
        B("#0FFFFFFF"), B("#0AFFFFFF"), B("#0A7F7F7F"), B("#60CDFF"), B("#000000"), B("#2960CDFF"),
        B("#6CCB5F"), B("#FCE100"), B("#FF99A4"), B("#9D9D9D"), B("#FFFFFF"));

    /// <summary>Publishes the tokens as dynamic resources (flyout and tray menu share them).</summary>
    public void Apply(ResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        resources["Flyout"] = Flyout;
        resources["FlyoutStroke"] = FlyoutStroke;
        resources["Card"] = Card;
        resources["CardStroke"] = CardStroke;
        resources["Ink"] = Ink;
        resources["Ink2"] = Ink2;
        resources["Divider"] = Divider;
        resources["SubtleHover"] = SubtleHover;
        resources["SubtlePressed"] = SubtlePressed;
        resources["Focus"] = Ink;
        resources["FooterTint"] = FooterTint;
        resources["Ink3"] = Ink3;
        resources["Accent"] = Accent;
        resources["OnAccent"] = OnAccent;
        resources["Critical"] = Critical;
    }

    private static SolidColorBrush B(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
