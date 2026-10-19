using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Upsy.Core.Link;
using Upsy.Core.Presentation;
using Upsy.Core.Station;
using Upsy.Flyout.Glyphs;

namespace Upsy.Flyout;

/// <summary>What the flyout shows for one station.</summary>
/// <param name="IsTray">The station the tray icon shows (pinned, listed first).</param>
/// <param name="Several">More than one station is listed: show the station More button.</param>
public sealed record StationSection(string Id, StationProfile Profile, StatusView View, MonitorSnapshot Snapshot, bool IsTray, bool Several);

/// <summary>One station in the flyout: glyph, level and state, In/Out strip, notice, Details.</summary>
public partial class StationView : UserControl
{
    private StationGlyph? _glyph;
    private NoticeAction _noticeAction;

    public StationView(string id)
    {
        Id = id;
        InitializeComponent();
    }

    public event EventHandler<NoticeAction>? NoticeRequested;

    public event EventHandler? MoreRequested;

    public event EventHandler? DetailsToggled;

    public string Id { get; }

    public UIElement MoreAnchor => MoreButton;

    /// <summary>Draws the separator above this section (every section but the first).</summary>
    public bool ShowSeparator
    {
        set => Separator.BorderThickness = new Thickness(0, value ? 1 : 0, 0, 0);
    }

    internal void Render(StationSection section, Palette t)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(t);
        var v = section.View;
        var s = section.Snapshot;
        var toneBrush = v.Tone switch
        {
            Tone.Ok => t.Ok,
            Tone.Caution => t.Caution,
            Tone.Critical => t.Critical,
            _ => t.Ink,
        };

        if (_glyph is null || _glyph.GetType() != StationGlyph.For(section.Profile).GetType())
        {
            _glyph = StationGlyph.For(section.Profile);
            GlyphHost.Content = _glyph;
        }

        SourceText.Text = v.Source;
        TrayPin.Visibility = section.IsTray && section.Several ? Visibility.Visible : Visibility.Collapsed;
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
            FillDetails(section, t);
        }

        Notice.Visibility = v.NoticeTitle is null ? Visibility.Collapsed : Visibility.Visible;
        NoticeTitle.Text = v.NoticeTitle ?? string.Empty;
        NoticeBody.Text = v.NoticeBody ?? string.Empty;
        _noticeAction = v.NoticeAction;
        var (label, accent) = v.NoticeAction switch
        {
            NoticeAction.Resume => ("Resume", true),
            NoticeAction.SignIn => ("Sign in", true),
            NoticeAction.TryAgain => ("Try again", false),
            NoticeAction.SetupGuide => ("Open setup guide", false),
            NoticeAction.BluetoothSettings => ("Bluetooth settings", false),
            _ => (string.Empty, false),
        };
        NoticeButton.Visibility = v.NoticeAction == NoticeAction.None ? Visibility.Collapsed : Visibility.Visible;
        NoticeButton.Content = label;
        NoticeButton.Background = accent ? t.Accent : t.Card;
        NoticeButton.Foreground = accent ? t.OnAccent : t.Ink;

        MoreButton.Visibility = section.Several ? Visibility.Visible : Visibility.Collapsed;
        var moreName = $"{v.Name} options";
        MoreButton.ToolTip = moreName;
        AutomationProperties.SetName(MoreButton, moreName);

        var tel = s.HasData ? s.Telemetry : null;
        _glyph.Render(
            new GlyphState(
                tel?.BatteryPercent ?? (v.LevelOnly ? v.Percent : null),
                v.Tone,
                v.LevelOnly,
                tel?.AcOutputOn == true,
                tel?.DcOutputOn == true,
                tel is { AcInputW: > 0, AcInputVolts: > 100 },
                tel is { DcInputW: > 0 },
                s.Settings?.LightOn == true),
            t);
        _glyph.Describe(tel is null
            ? $"{section.Profile.FullName}: {v.Headline}"
            : string.Create(CultureInfo.InvariantCulture,
                $"{section.Profile.FullName}: {tel.BatteryPercent}%, AC output {(tel.AcOutputOn ? "on" : "off")}, DC output {(tel.DcOutputOn ? "on" : "off")}"));
    }

    internal bool DetailsOpen
    {
        get => DetailsToggle.IsChecked == true;
        set => DetailsToggle.IsChecked = value;
    }

    private static string StatusGlyph(StatusView v, MonitorSnapshot s) => s.Status switch
    {
        LinkStatus.BluetoothOff => "",
        LinkStatus.Paused => "",
        LinkStatus.SignInNeeded => "",
        LinkStatus.KeyRejected => "",
        LinkStatus.KeyTableMissing => "",
        LinkStatus.Searching or LinkStatus.Connecting when v.LevelOnly => "",
        LinkStatus.Searching or LinkStatus.Connecting => "",
        _ => v.Tone switch
        {
            Tone.Ok => "",
            Tone.Caution or Tone.Critical => "",
            _ => "",
        },
    };

    private void FillDetails(StationSection section, Palette t)
    {
        var s = section.Snapshot;
        var tel = s.Telemetry!;
        var p = section.View.Power!;
        var profile = section.Profile;
        var inv = CultureInfo.InvariantCulture;
        var rows = new List<(string Label, string? Value)>
        {
            ("Power", null),
            ("AC input", string.Create(inv, $"{tel.AcInputW} W · {tel.AcInputVolts:0.0} V")),
            (profile.DcInputLabel, profile.Key == StationCatalog.Eb3a.Key ? string.Create(inv, $"{tel.DcInputW} W · {tel.DcInputVolts:0.00} V") : $"{tel.DcInputW} W"),
            ("AC output", $"{(tel.AcOutputOn ? "On" : "Off")} · {tel.AcOutputW} W"),
            (profile.DcOutputLabel, $"{(tel.DcOutputOn ? "On" : "Off")} · {tel.DcOutputW} W"),
            ("Battery net", p.NetBatteryW > 0 ? $"+{p.NetBatteryW} W" : $"{p.NetBatteryW} W"),
            ("Load present", p.LoadPresent ? "Yes" : "No"),
        };

        if (tel.ToFull is { } full && p.Charging)
        {
            rows.Add(("Full in", $"{Capitalize(StatusText.Runtime(full))} (station)"));
        }
        else if (tel.ToEmpty is { } empty && !p.OnMains && p.LoadPresent)
        {
            rows.Add(("Empty in", $"{Capitalize(StatusText.Runtime(empty))} (station)"));
        }

        if (s.Settings is { Items.Count: > 0 } settings)
        {
            rows.Add(("Station", null));
            rows.AddRange(settings.Items.Select(i => (i.Label, (string?)i.Value)));
        }

        if (s.Identity is { } id)
        {
            rows.Add(("Device", null));
            rows.Add(("Model", profile.FullName));
            rows.Add(("Serial", id.Serial));
            rows.Add(("Firmware", id.FirmwareText));
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

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private void OnDetailsToggled(object sender, RoutedEventArgs e)
    {
        DetailsBody.Visibility = DetailsToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        DetailsToggled?.Invoke(this, EventArgs.Empty);
    }

    private void OnMoreButton(object sender, RoutedEventArgs e) => MoreRequested?.Invoke(this, EventArgs.Empty);

    private void OnNoticeButton(object sender, RoutedEventArgs e) => NoticeRequested?.Invoke(this, _noticeAction);
}
