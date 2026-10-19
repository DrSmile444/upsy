using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Upsy.Core.Station;

namespace Upsy.Dialogs;

/// <summary>A supported station heard nearby; <paramref name="Added"/> when it is already in the list.</summary>
public sealed record NearbyStation(DiscoveredStation Station, bool Added);

/// <summary>Lists supported stations advertising nearby (DESIGN.md "Add station window"), refreshing once a second while open.</summary>
public partial class AddStationWindow : DialogWindow
{
    private readonly Func<IReadOnlyList<NearbyStation>> _nearby;
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromSeconds(1) };
    private string _shown = string.Empty;

    public AddStationWindow(Func<IReadOnlyList<NearbyStation>> nearby)
    {
        _nearby = nearby;
        InitializeComponent();
        _refresh.Tick += (_, _) => Refresh();
        Loaded += (_, _) =>
        {
            Refresh();
            _refresh.Start();
        };
        Closed += (_, _) => _refresh.Stop();
    }

    public event EventHandler<DiscoveredStation>? AddRequested;

    private static string Signal(short rssi) => rssi >= -65 ? "signal good" : rssi >= -80 ? "signal fair" : "signal weak";

    private void Refresh()
    {
        var list = _nearby();
        var key = string.Join("|", list.Select(n => $"{n.Station.Profile.Key}:{n.Station.Serial}:{n.Added}:{Signal(n.Station.Rssi)}"));
        if (key == _shown)
        {
            return;
        }

        _shown = key;
        Rows.Children.Clear();
        for (var i = 0; i < list.Count; i++)
        {
            Rows.Children.Add(Row(list[i], first: i == 0));
        }

        Empty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Border Row(NearbyStation nearby, bool first)
    {
        var station = nearby.Station;
        var grid = new Grid { Margin = new Thickness(12, 10, 12, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new TextBlock { Text = "", FontFamily = (FontFamily)FindResource("Icons"), FontSize = 20, VerticalAlignment = VerticalAlignment.Center };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "Ink2");
        grid.Children.Add(icon);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
        var name = new TextBlock { Text = station.Profile.FullName, FontWeight = FontWeights.SemiBold };
        name.SetResourceReference(TextBlock.ForegroundProperty, "Ink");
        var sub = new TextBlock { Text = $"{station.Name ?? station.Serial} · {Signal(station.Rssi)}", FontSize = 12 };
        sub.SetResourceReference(TextBlock.ForegroundProperty, "Ink2");
        text.Children.Add(name);
        text.Children.Add(sub);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        FrameworkElement action;
        if (nearby.Added)
        {
            var added = new TextBlock { Text = "Added", FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            added.SetResourceReference(TextBlock.ForegroundProperty, "Ink2");
            action = added;
        }
        else
        {
            var add = new Button { Content = "Add", Style = (Style)FindResource("AccentButton"), Margin = new Thickness(0) };
            System.Windows.Automation.AutomationProperties.SetName(add, $"Add {station.Profile.FullName}");
            add.Click += (_, _) =>
            {
                AddRequested?.Invoke(this, station);
                Close();
            };
            action = add;
        }

        Grid.SetColumn(action, 2);
        grid.Children.Add(action);

        var row = new Border { BorderThickness = new Thickness(0, first ? 0 : 1, 0, 0), Child = grid };
        row.SetResourceReference(Border.BorderBrushProperty, "Divider");
        return row;
    }
}
