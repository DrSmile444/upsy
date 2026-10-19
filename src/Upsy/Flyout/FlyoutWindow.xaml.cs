using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Upsy.Core.Link;
using Upsy.Core.Presentation;
using Upsy.Core.Station;

namespace Upsy.Flyout;

/// <summary>The left-click flyout. Layout and tokens follow DESIGN.md / design/upsy.design.html.</summary>
public partial class FlyoutWindow : Window
{
    private bool? _themeIsLight;
    private System.Drawing.Point? _anchor;

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

    public event EventHandler? PauseRequested;

    /// <summary>Where the "More" menu opens from.</summary>
    public UIElement MoreAnchor => MoreButton;

    public event EventHandler? ResumeRequested;

    public event EventHandler? MoreRequested;

    public event EventHandler? BluetoothSettingsRequested;

    private NoticeAction _noticeAction;
    private bool _paused;

    public void ApplyTheme(bool light)
    {
        if (_themeIsLight == light)
        {
            return;
        }

        _themeIsLight = light;
        (light ? Palette.Light : Palette.Dark).Apply(Resources);
    }

    public void Render(StatusView v, MonitorSnapshot s)
    {
        ArgumentNullException.ThrowIfNull(v);
        ArgumentNullException.ThrowIfNull(s);
        var t = _themeIsLight == false ? Palette.Dark : Palette.Light;
        var toneBrush = v.Tone switch
        {
            Tone.Ok => t.Ok,
            Tone.Caution => t.Caution,
            Tone.Critical => t.Critical,
            _ => t.Ink,
        };

        SourceText.Text = v.Source;
        PercentText.Visibility = v.Percent is null ? Visibility.Collapsed : Visibility.Visible;
        PercentValue.Text = v.Percent?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        StatusLine.Text = v.Headline;
        StatusLine.Foreground = toneBrush;
        StatusLine.FontSize = v.Percent is null ? 20 : 14;
        StatusIcon.Foreground = toneBrush;
        StatusIcon.Text = StatusGlyph(v, s);
        StatusIcon.Visibility = StatusIcon.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        DetailText.Text = v.Detail ?? string.Empty;
        DetailText.Visibility = v.Detail is null ? Visibility.Collapsed : Visibility.Visible;

        var hasData = s.HasData && v.Power is not null;
        Flows.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
        Details.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
        if (hasData)
        {
            InText.Text = $"{v.Power!.InputW} W";
            OutText.Text = $"{v.Power.OutputW} W";
            FillDetails(s, v, t);
        }

        Notice.Visibility = v.NoticeTitle is null ? Visibility.Collapsed : Visibility.Visible;
        NoticeTitle.Text = v.NoticeTitle ?? string.Empty;
        NoticeBody.Text = v.NoticeBody ?? string.Empty;
        _noticeAction = v.NoticeAction;
        NoticeButton.Visibility = v.NoticeAction == NoticeAction.None ? Visibility.Collapsed : Visibility.Visible;
        NoticeButton.Content = v.NoticeAction == NoticeAction.Resume ? "Resume" : "Bluetooth settings";
        NoticeButton.Background = v.NoticeAction == NoticeAction.Resume ? t.Accent : t.Card;
        NoticeButton.Foreground = v.NoticeAction == NoticeAction.Resume ? t.OnAccent : t.Ink;

        _paused = s.Status == LinkStatus.Paused;
        PauseButton.Content = _paused ? "" : "";
        var pauseName = _paused ? "Resume connection" : "Pause for 30 minutes";
        PauseButton.ToolTip = pauseName;
        AutomationProperties.SetName(PauseButton, pauseName);

        RenderGlyph(s, v, t);
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

    private static string StatusGlyph(StatusView v, MonitorSnapshot s) => s.Status switch
    {
        LinkStatus.BluetoothOff => "",
        LinkStatus.Paused => "",
        LinkStatus.Searching or LinkStatus.Connecting => "",
        _ => v.Tone switch
        {
            Tone.Ok => "",
            Tone.Caution or Tone.Critical => "",
            _ => "",
        },
    };

    private void RenderGlyph(MonitorSnapshot s, StatusView v, Palette t)
    {
        foreach (var shape in Shapes(Glyph))
        {
            shape.Stroke = t.Glyph;
            shape.Fill = Equals(shape.Tag, "dot") ? t.Glyph : null;
            shape.StrokeLineJoin = PenLineJoin.Round;
            shape.StrokeStartLineCap = shape.StrokeEndLineCap = PenLineCap.Round;
        }

        GAcPad.Stroke = GDcPad.Stroke = null;
        GGaugeTrack.Stroke = t.Glyph;
        GGaugeTrack.Opacity = 0.35;
        var tel = s.Telemetry;
        if (!s.HasData || tel is null)
        {
            GGaugeValue.Visibility = Visibility.Collapsed;
            GGaugeTrack.StrokeDashArray = [0.6, 1.2];
            return;
        }

        GGaugeTrack.StrokeDashArray = null;
        GGaugeValue.Visibility = tel.BatteryPercent > 0 ? Visibility.Visible : Visibility.Collapsed;
        GGaugeValue.Data = GaugeArc(tel.BatteryPercent);
        GGaugeValue.Stroke = v.Tone switch
        {
            Tone.Ok => t.Ok,
            Tone.Caution => t.Caution,
            Tone.Critical => t.Critical,
            _ => t.Ink,
        };

        Paint(GAcOut, tel.AcOutputOn ? t.GlyphOn : t.Glyph);
        GAcPad.Fill = tel.AcOutputOn ? t.AccentTint : null;
        Paint(GDcOut, tel.DcOutputOn ? t.GlyphOn : t.Glyph);
        GDcPad.Fill = tel.DcOutputOn ? t.AccentTint : null;
        Paint(GAcIn, tel.AcInputW > 0 && tel.AcInputVolts > 100 ? t.Ok : t.Glyph);
        Paint(GDcIn, tel.DcInputW > 0 ? t.Ok : t.Glyph);
        Paint(GLight, s.Settings?.Led is LedMode.Low or LedMode.High or LedMode.Sos ? t.GlyphOn : t.Glyph);

        AutomationProperties.SetName(GlyphBox, string.Create(CultureInfo.InvariantCulture,
            $"{s.Identity?.Model ?? "Station"}: {tel.BatteryPercent}%, AC output {(tel.AcOutputOn ? "on" : "off")}, DC output {(tel.DcOutputOn ? "on" : "off")}"));
    }

    /// <summary>The station screen's half ring, filled from the left to <paramref name="percent"/>.</summary>
    private static PathGeometry GaugeArc(int percent)
    {
        const double cx = 400, cy = 392, r = 72;
        var angle = Math.PI * (1 - (Math.Clamp(percent, 0, 100) / 100.0));
        var start = new Point(cx - r, cy);
        var end = new Point(cx + (r * Math.Cos(angle)), cy - (r * Math.Sin(angle)));
        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment(end, new Size(r, r), 0, isLargeArc: false, SweepDirection.Clockwise, isStroked: true));
        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }

    private static IEnumerable<Shape> Shapes(Panel panel)
    {
        foreach (var child in panel.Children)
        {
            if (child is Shape shape)
            {
                yield return shape;
            }
            else if (child is Panel nested)
            {
                foreach (var inner in Shapes(nested))
                {
                    yield return inner;
                }
            }
        }
    }

    private static void Paint(Panel group, Brush brush)
    {
        foreach (var shape in Shapes(group))
        {
            shape.Stroke = brush;
            if (Equals(shape.Tag, "dot"))
            {
                shape.Fill = brush;
            }
        }
    }

    private void FillDetails(MonitorSnapshot s, StatusView v, Palette t)
    {
        var tel = s.Telemetry!;
        var p = v.Power!;
        var inv = CultureInfo.InvariantCulture;
        var rows = new List<(string Label, string? Value)>
        {
            ("Power", null),
            ("AC input", string.Create(inv, $"{tel.AcInputW} W · {tel.AcInputVolts:0.0} V")),
            ("DC input", string.Create(inv, $"{tel.DcInputW} W · {tel.DcInputVolts:0.00} V")),
            ("AC output", $"{(tel.AcOutputOn ? "On" : "Off")} · {tel.AcOutputW} W"),
            ("DC output", $"{(tel.DcOutputOn ? "On" : "Off")} · {tel.DcOutputW} W"),
            ("Battery net", p.NetBatteryW > 0 ? $"+{p.NetBatteryW} W" : $"{p.NetBatteryW} W"),
            ("Load present", p.LoadPresent ? "Yes" : "No"),
        };

        if (s.Settings is { } st)
        {
            rows.Add(("Station", null));
            rows.Add(("Charging mode", st.Charging.ToString()));
            rows.Add(("ECO mode", st.EcoOn ? $"On · off after {st.EcoShutdownHours} h at ≤10 W" : "Off"));
            rows.Add(("Power lifting", st.PowerLiftingOn ? "On" : "Off"));
            rows.Add(("Light", st.Led switch { LedMode.Off => "Off", LedMode.Low => "Low", LedMode.High => "High", LedMode.Sos => "SOS", _ => "Unknown" }));
        }

        if (s.Identity is { } id)
        {
            rows.Add(("Device", null));
            rows.Add(("Model", id.Model));
            rows.Add(("Serial", id.Serial));
            rows.Add(("Firmware", $"ARM {id.ArmFirmware} · DSP {id.DspFirmware}"));
        }

        rows.Add(("Updated", s.UpdatedAt is { } at ? at.ToLocalTime().ToString("HH:mm:ss", inv) : "-"));

        DetailsGrid.Children.Clear();
        DetailsGrid.RowDefinitions.Clear();
        for (var i = 0; i < rows.Count; i++)
        {
            DetailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var (label, value) = rows[i];
            var l = new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = value is null ? t.Ink : t.Ink2,
                FontWeight = value is null ? FontWeights.SemiBold : FontWeights.Normal,
                Margin = new Thickness(0, value is null && i > 0 ? 8 : 2, 0, 1),
            };
            Grid.SetRow(l, i);
            DetailsGrid.Children.Add(l);
            if (value is not null)
            {
                var r = new TextBlock { Text = value, FontSize = 12, Foreground = t.Ink, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 2, 0, 1) };
                Grid.SetRow(r, i);
                Grid.SetColumn(r, 1);
                DetailsGrid.Children.Add(r);
            }
        }
    }

    private void OnDetailsToggled(object sender, RoutedEventArgs e) =>
        DetailsBody.Visibility = DetailsToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private void OnPauseButton(object sender, RoutedEventArgs e) => (_paused ? ResumeRequested : PauseRequested)?.Invoke(this, EventArgs.Empty);

    private void OnMoreButton(object sender, RoutedEventArgs e) => MoreRequested?.Invoke(this, EventArgs.Empty);

    private void OnNoticeButton(object sender, RoutedEventArgs e)
    {
        if (_noticeAction == NoticeAction.Resume)
        {
            ResumeRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (_noticeAction == NoticeAction.BluetoothSettings)
        {
            BluetoothSettingsRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>Windows 11 tokens from DESIGN.md.</summary>
internal sealed record Palette(
    Brush Flyout, Brush FlyoutStroke, Brush Card, Brush CardStroke, Brush Ink, Brush Ink2, Brush Divider,
    Brush SubtleHover, Brush SubtlePressed, Brush FooterTint, Brush Accent, Brush OnAccent, Brush AccentTint,
    Brush Ok, Brush Caution, Brush Critical, Brush Glyph, Brush GlyphOn)
{
    public static readonly Palette Light = new(
        B("#F9F9F9"), B("#1A000000"), B("#FFFFFF"), B("#0F000000"), B("#1B1B1B"), B("#5C5C5C"), B("#14000000"),
        B("#0A000000"), B("#06000000"), B("#0A7F7F7F"), B("#005FB8"), B("#FFFFFF"), B("#29005FB8"),
        B("#0F7B0F"), B("#9D5D00"), B("#C42B1C"), B("#5C5C5C"), B("#1B1B1B"));

    public static readonly Palette Dark = new(
        B("#2C2C2C"), B("#14FFFFFF"), B("#0DFFFFFF"), B("#0FFFFFFF"), B("#FFFFFF"), B("#C8C8C8"), B("#14FFFFFF"),
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
    }

    private static SolidColorBrush B(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
