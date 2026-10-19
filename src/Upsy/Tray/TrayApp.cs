using System.Diagnostics;
using System.Net.Http;
using System.Windows.Forms;
using Microsoft.Win32;
using Upsy.Ble;
using Upsy.Core.Alerts;
using Upsy.Core.Link;
using Upsy.Core.Power;
using Upsy.Core.Presentation;
using Upsy.Core.Protocol.EcoFlow;
using Upsy.Core.Settings;
using Upsy.Core.Station;
using Upsy.Dialogs;
using Upsy.Flyout;
using Upsy.Infrastructure;

namespace Upsy.Tray;

/// <summary>
/// Composition root: the station list, one monitor per station, the shared BLE scanner, the tray icon for the chosen
/// station, the flyout with every station, menus, windows and notifications.
/// </summary>
public sealed class TrayApp : IDisposable
{
    public const string SetupGuideUrl = "https://github.com/DrSmile444/upsy#ecoflow-stations";
    private static readonly int[] ThresholdSteps = [10, 15, 20, 25, 30];

    /// <summary>An advertised level older than this is not shown.</summary>
    private static readonly TimeSpan AdvertisementFresh = TimeSpan.FromSeconds(60);

    private readonly System.Windows.Threading.Dispatcher _ui;
    private readonly NotifyIcon _notify;
    private readonly TrayMenu _menu = new();
    private readonly FlyoutWindow _flyout = new();
    private readonly AdvertisementHub _hub = new();
    private readonly WinRtStationTransport _transport = new();
    private readonly DpapiProtector _protector = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly Dictionary<string, StationHost> _hosts = [];
    private readonly Dictionary<string, (DiscoveredStation Station, DateTimeOffset At)> _nearby = [];
    private readonly StationRegistry _registry;
    private UpsySettings _settings;
    private System.Drawing.Icon? _icon;
    private bool _signInOpen;

    public TrayApp(System.Windows.Threading.Dispatcher ui)
    {
        _ui = ui;
        _settings = SettingsStore.Load();
        _registry = new StationRegistry(_settings.Stations, _settings.TrayStationId);
        _registry.Changed += (_, _) => SaveStations();

        _notify = new NotifyIcon { Text = "upsy", Visible = true };
        _notify.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ToggleFlyout();
            }
            else if (e.Button == MouseButtons.Right)
            {
                _flyout.Hide();
                _menu.ShowAtCursor(MenuItems(), WindowsTheme.AppsAreLight);
            }
        };

        _flyout.PauseToggleRequested += (_, id) => TogglePause(id);
        _flyout.NoticeRequested += (_, e) => OnNotice(e.Id, e.Action);
        _flyout.StationMoreRequested += (_, e) => _menu.ShowAbove(e.Anchor, StationMenuItems(e.Id), WindowsTheme.AppsAreLight);
        _flyout.MoreRequested += (_, _) => _menu.ShowAbove(_flyout.MoreAnchor, MenuItems(), WindowsTheme.AppsAreLight);
        _flyout.AddStationRequested += (_, _) => ShowAddStation();
        _hub.Received += OnAdvertisement;

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Refresh();
    }

    public void Start()
    {
        _hub.Start();
        foreach (var entry in _registry.Stations)
        {
            StartHost(entry);
        }

        Log.Info($"watching {_registry.Stations.Count} station(s): {string.Join(", ", _registry.Stations.Select(_registry.DisplayName))}");
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _hub.Dispose();
        foreach (var host in _hosts.Values)
        {
            host.Dispose();
        }

        _notify.Visible = false;
        _notify.Dispose();
        TrayIconRenderer.Destroy(_icon);
        _http.Dispose();
        _flyout.Close();
    }

    private void StartHost(StationEntry entry)
    {
        if (entry.Profile is not { } profile || _hosts.ContainsKey(entry.Id))
        {
            return;
        }

        IStationDriver driver = profile.Key == StationCatalog.Delta2.Key
            ? new EcoFlowDriver(entry.Serial ?? string.Empty, () => _settings.EcoFlowUserId(_protector), KeyTableStore.Load, TimeProvider.System)
            : new BluettiDriver(TimeProvider.System);
        var host = new StationHost(entry, new StationMonitor(_transport, driver, TimeProvider.System, entry.Address), _settings.LowPercent);
        host.Monitor.Changed += (_, s) => _ui.BeginInvoke(() => OnSnapshot(host, s));
        _hosts[entry.Id] = host;
        host.Start();
    }

    private void OnSnapshot(StationHost host, MonitorSnapshot s)
    {
        if (!_hosts.ContainsKey(host.Entry.Id))
        {
            return; // forgotten while the update was on its way
        }

        var name = _registry.DisplayName(host.Entry);
        var wasConnected = host.Snapshot.HasData;
        var previous = host.Snapshot.Status;
        host.Snapshot = s;
        if (s.HasData)
        {
            var t = s.Telemetry!;
            if (!wasConnected)
            {
                host.Estimator.Reset();
                Log.Info($"connected to {name} {s.Identity?.Serial}: {t.BatteryPercent}%, in {t.AcInputW + t.DcInputW} W, out {t.AcOutputW + t.DcOutputW} W, AC {t.AcInputVolts} V");
                if (host.Entry.Serial is null && s.Identity?.Serial is { Length: > 0 } serial)
                {
                    _registry.Update(host.Entry.Id, host.Monitor.Address, serial);
                    host.Entry = _registry.Stations.First(e => e.Id == host.Entry.Id);
                }
            }

            host.Estimator.Add(t);
            foreach (var alert in host.Alerts.Observe(t))
            {
                var (title, body) = StatusText.Alert(alert, t, host.Runtime(), name);
                Log.Info($"alert {alert} for {name}: {body}");
                _notify.ShowBalloonTip(10_000, title, body, alert is AlertKind.BatteryCritical or AlertKind.MainsLost ? ToolTipIcon.Warning : ToolTipIcon.Info);
            }
        }
        else
        {
            host.Alerts.ObserveNoData();
            if (wasConnected || s.Status != previous)
            {
                Log.Info($"{name}: link {s.Status}");
            }
        }

        Refresh();
    }

    /// <summary>Runs on the scanner thread: only supported stations go on to the UI thread.</summary>
    private void OnAdvertisement(object? sender, Advertisement ad)
    {
        if (StationCatalog.Recognize(ad) is { } station)
        {
            _ui.BeginInvoke(() => OnStationHeard(station));
        }
    }

    private void OnStationHeard(DiscoveredStation station)
    {
        var now = DateTimeOffset.UtcNow;
        _nearby[$"{station.Profile.Key}:{station.Serial}"] = (station, now);

        if (_registry.Stations.Count == 0)
        {
            Log.Info($"first station found: {station.Profile.FullName}");
            AddStation(station);
            return;
        }

        if (_registry.Find(station) is not { } entry || !_hosts.TryGetValue(entry.Id, out var host))
        {
            return;
        }

        if (entry.Address != station.Address)
        {
            Log.Info($"{_registry.DisplayName(entry)} now advertises at a new address");
            _registry.Update(entry.Id, station.Address, station.Serial);
            host.Entry = _registry.Stations.First(e => e.Id == entry.Id);
            host.Monitor.UpdateAddress(station.Address);
        }

        if (station.AdvertisedPercent is { } level)
        {
            var changed = host.AdvertisedPercent != level;
            host.AdvertisedPercent = level;
            host.AdvertisedAt = now;
            if (changed && !host.Snapshot.HasData)
            {
                Refresh();
            }
        }
    }

    private void AddStation(DiscoveredStation station)
    {
        var entry = _registry.Add(station);
        Log.Info($"station added: {_registry.DisplayName(entry)}");
        StartHost(entry);
        Refresh();
        if (station.Profile.Key == StationCatalog.Delta2.Key && _settings.ProtectedEcoFlowUserId is null)
        {
            ShowSignIn(entry.Id);
        }
    }

    private void Forget(string id)
    {
        if (!_hosts.TryGetValue(id, out var host))
        {
            return;
        }

        var name = _registry.DisplayName(host.Entry);
        var confirm = new ConfirmWindow(
            $"Forget {name}?",
            "upsy stops watching this station: no data, no alerts, and it is not reconnected after a restart. You can add it again from Add station"
                + (host.Entry.Profile?.Key == StationCatalog.Delta2.Key ? "; your EcoFlow sign-in is kept." : "."),
            "Forget");
        if (confirm.ShowDialog() != true)
        {
            return;
        }

        _hosts.Remove(id);
        host.Dispose();
        _registry.Forget(id);
        Log.Info($"station forgotten: {name}");
        Refresh();
    }

    private void SaveStations()
    {
        _settings = _settings with { Stations = [.. _registry.Stations], TrayStationId = _registry.TrayStationId };
        SettingsStore.Save(_settings);
    }

    private StationContext Context(StationHost host)
    {
        var fresh = host.AdvertisedPercent is not null && DateTimeOffset.UtcNow - host.AdvertisedAt < AdvertisementFresh;
        return new StationContext(_registry.DisplayName(host.Entry), host.Entry.Profile!, fresh ? host.AdvertisedPercent : null);
    }

    private StatusView View(StationHost host) => StatusText.Build(host.Snapshot, host.Runtime(), _settings.LowPercent, Context(host));

    private void Refresh()
    {
        var ordered = _registry.Ordered.Where(e => _hosts.ContainsKey(e.Id)).Select(e => _hosts[e.Id]).ToList();
        StatusView tray;
        if (ordered.Count == 0)
        {
            tray = StatusText.FirstStart();
            _notify.Text = tray.Tooltip;
        }
        else
        {
            var views = ordered.Select(View).ToList();
            tray = views[0];
            _notify.Text = StatusText.Tooltip(tray, views.Skip(1));
        }

        var trayHost = ordered.FirstOrDefault();
        var glyph = trayHost?.Snapshot.Status == LinkStatus.Paused ? TrayGlyph.Paused
            : tray.Percent is null ? TrayGlyph.NoData
            : tray.Percent >= 100 && !tray.LevelOnly ? TrayGlyph.Full
            : TrayGlyph.Number;
        var state = new TrayIconState(glyph, tray.Percent ?? 0, tray.Tone, tray.Power?.Charging == true, tray.LevelOnly);
        var size = SystemInformation.SmallIconSize.Width;
        using var bmp = TrayIconRenderer.Render(state, size, WindowsTheme.TaskbarIsLight);
        var old = _icon;
        _icon = TrayIconRenderer.ToIcon(bmp);
        _notify.Icon = _icon;
        TrayIconRenderer.Destroy(old);

        if (_flyout.IsVisible)
        {
            _flyout.Render(Sections(ordered));
        }
    }

    private List<StationSection> Sections(List<StationHost> ordered)
    {
        if (ordered.Count == 0)
        {
            return [new StationSection("first-start", StationCatalog.Eb3a, StatusText.FirstStart(), MonitorSnapshot.Initial, true, false)];
        }

        var trayId = _registry.TrayStationId;
        return [.. ordered.Select(h => new StationSection(h.Entry.Id, h.Entry.Profile!, View(h), h.Snapshot, h.Entry.Id == trayId, ordered.Count > 1))];
    }

    private void ToggleFlyout()
    {
        if (_flyout.IsVisible)
        {
            _flyout.Hide();
            return;
        }

        _flyout.ApplyTheme(WindowsTheme.AppsAreLight);
        _flyout.Render(Sections([.. _registry.Ordered.Where(e => _hosts.ContainsKey(e.Id)).Select(e => _hosts[e.Id])]));
        _flyout.ShowNear(Cursor.Position);
    }

    private void TogglePause(string id)
    {
        if (!_hosts.TryGetValue(id, out var host))
        {
            return;
        }

        if (host.Snapshot.Status == LinkStatus.Paused)
        {
            host.Monitor.Resume();
            Log.Info($"{_registry.DisplayName(host.Entry)}: resumed by user");
        }
        else
        {
            host.Monitor.Pause();
            Log.Info($"{_registry.DisplayName(host.Entry)}: paused by user");
        }
    }

    private void OnNotice(string id, NoticeAction action)
    {
        _hosts.TryGetValue(id, out var host);
        switch (action)
        {
            case NoticeAction.Resume:
                host?.Monitor.Resume();
                break;
            case NoticeAction.BluetoothSettings:
                Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
                break;
            case NoticeAction.SignIn:
                ShowSignIn(id);
                break;
            case NoticeAction.TryAgain:
                host?.Monitor.Retry();
                break;
            case NoticeAction.SetupGuide:
                Process.Start(new ProcessStartInfo(SetupGuideUrl) { UseShellExecute = true });
                break;
        }
    }

    private void ShowSignIn(string stationId)
    {
        if (_signInOpen)
        {
            return;
        }

        _flyout.Hide();
        var model = _hosts.TryGetValue(stationId, out var host) ? host.Entry.Profile!.Model : StationCatalog.Delta2.Model;
        var account = new EcoFlowAccount(_http);
        var window = new SignInWindow(model, account.SignInAsync);
        _signInOpen = true;
        try
        {
            if (window.ShowDialog() != true || window.UserId is not { } userId)
            {
                return;
            }

            _settings = _settings.WithEcoFlowUserId(userId, _protector);
            SettingsStore.Save(_settings);
            Log.Info("EcoFlow sign-in succeeded; the user ID is stored for this Windows account");
            RetryEcoFlow();
        }
        finally
        {
            _signInOpen = false;
        }
    }

    private void SignOut()
    {
        _settings = _settings.WithEcoFlowUserId(null, _protector);
        SettingsStore.Save(_settings);
        Log.Info("signed out of EcoFlow");
        foreach (var host in EcoFlowHosts())
        {
            host.Monitor.Reconnect();
        }
    }

    private void RetryEcoFlow()
    {
        foreach (var host in EcoFlowHosts())
        {
            host.Monitor.Retry();
        }
    }

    private IEnumerable<StationHost> EcoFlowHosts() => _hosts.Values.Where(h => h.Entry.Profile?.Key == StationCatalog.Delta2.Key);

    private void ShowAddStation()
    {
        _flyout.Hide();
        var window = new AddStationWindow(() =>
        {
            var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(15);
            return [.. _nearby.Values
                .Where(n => n.At > cutoff)
                .Select(n => new NearbyStation(n.Station, _registry.Contains(n.Station)))
                .OrderBy(n => n.Added)
                .ThenByDescending(n => n.Station.Rssi)];
        });
        window.AddRequested += (_, station) => AddStation(station);
        window.Show();
        window.Activate();
    }

    /// <summary>App menu per DESIGN.md "Context menu" and "Context menu, several stations".</summary>
    private List<System.Windows.Controls.Control> MenuItems()
    {
        var ordered = _registry.Ordered.Where(e => _hosts.ContainsKey(e.Id)).Select(e => _hosts[e.Id]).ToList();
        var tray = ordered.FirstOrDefault();
        var title = tray is null ? StatusText.FirstStart().Tooltip : View(tray).Tooltip;
        var several = ordered.Count > 1;

        var threshold = TrayMenu.Item("Low battery alert", "", () => { }, meta: $"{_settings.LowPercent}%");
        foreach (var step in ThresholdSteps)
        {
            var value = step;
            threshold.Items.Add(TrayMenu.Item($"{value}%", string.Empty, () => SetThreshold(value), isChecked: value == _settings.LowPercent));
        }

        var items = new List<System.Windows.Controls.Control>
        {
            TrayMenu.Title(title),
            TrayMenu.Item("Open upsy", string.Empty, ToggleFlyout, bold: true),
        };

        if (tray is not null)
        {
            var name = several ? $" {_registry.DisplayName(tray.Entry)}" : string.Empty;
            items.Add(tray.Snapshot.Status == LinkStatus.Paused
                ? TrayMenu.Item($"Resume{name}", "", () => TogglePause(tray.Entry.Id))
                : TrayMenu.Item($"Pause{name} for 30 minutes", "", () => TogglePause(tray.Entry.Id)));
        }

        if (several)
        {
            var show = TrayMenu.Item("Show in tray", "", () => { }, meta: _registry.DisplayName(tray!.Entry));
            foreach (var host in ordered)
            {
                var id = host.Entry.Id;
                show.Items.Add(TrayMenu.Item(_registry.DisplayName(host.Entry), string.Empty, () => SetTray(id), isChecked: host == tray));
            }

            items.Add(show);
        }

        items.Add(TrayMenu.Item("Add station…", "", ShowAddStation));
        items.Add(new System.Windows.Controls.Separator());
        items.Add(TrayMenu.Item("Start with Windows", string.Empty, () => Autostart.Set(!Autostart.IsEnabled), isChecked: Autostart.IsEnabled));
        items.Add(threshold);
        if (_settings.ProtectedEcoFlowUserId is not null)
        {
            items.Add(TrayMenu.Item("Sign out of EcoFlow", "", SignOut));
        }

        items.Add(new System.Windows.Controls.Separator());
        items.Add(TrayMenu.Item("Exit", "", () => System.Windows.Application.Current.Shutdown()));
        return items;
    }

    /// <summary>The station More menu in the flyout (several stations).</summary>
    private List<System.Windows.Controls.Control> StationMenuItems(string id)
    {
        if (!_hosts.TryGetValue(id, out var host))
        {
            return [];
        }

        var isTray = _registry.TrayStationId == id;
        var showInTray = TrayMenu.Item("Show in tray", "", () => SetTray(id), isChecked: isTray);
        showInTray.IsEnabled = !isTray;
        return
        [
            TrayMenu.Title(View(host).Tooltip.Split('\n')[0]),
            host.Snapshot.Status == LinkStatus.Paused
                ? TrayMenu.Item("Resume", "", () => TogglePause(id))
                : TrayMenu.Item("Pause for 30 minutes", "", () => TogglePause(id)),
            showInTray,
            new System.Windows.Controls.Separator(),
            TrayMenu.Item("Forget this station…", "", () => Forget(id)),
        ];
    }

    private void SetTray(string id)
    {
        _registry.SetTrayStation(id);
        Log.Info($"tray shows {_registry.DisplayName(_registry.TrayStation!)}");
        Refresh();
    }

    private void SetThreshold(int value)
    {
        _settings = _settings with { LowPercent = value };
        foreach (var host in _hosts.Values)
        {
            host.Alerts.LowPercent = value;
        }

        SettingsStore.Save(_settings);
        Refresh();
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e) => _ui.BeginInvoke(() =>
    {
        _flyout.ApplyTheme(WindowsTheme.AppsAreLight);
        Refresh();
    });

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Log.Info("resumed from sleep: reconnecting every station");
            _ui.BeginInvoke(() =>
            {
                _hub.Start();
                foreach (var host in _hosts.Values)
                {
                    host.Monitor.Reconnect();
                }
            });
        }
    }

    /// <summary>One station's link and derived state.</summary>
    private sealed class StationHost(StationEntry entry, StationMonitor monitor, int lowPercent) : IDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        private Task? _run;

        public StationEntry Entry { get; set; } = entry;

        public StationMonitor Monitor { get; } = monitor;

        public RuntimeEstimator Estimator { get; } = new(TimeProvider.System, entry.Profile!.NominalCapacityWh);

        public AlertMonitor Alerts { get; } = new(TimeProvider.System) { LowPercent = lowPercent };

        public MonitorSnapshot Snapshot { get; set; } = MonitorSnapshot.Initial;

        public int? AdvertisedPercent { get; set; }

        public DateTimeOffset AdvertisedAt { get; set; }

        public TimeSpan? Runtime() =>
            Snapshot.HasData ? Estimator.Estimate(Snapshot.Telemetry!, PowerReading.From(Snapshot.Telemetry!)) : null;

        public void Start() => _run = Task.Run(() => Monitor.RunAsync(_stop.Token));

        public void Dispose()
        {
            _stop.Cancel();
            try
            {
                _run?.Wait(TimeSpan.FromSeconds(3));
            }
            catch (AggregateException)
            {
            }

            Monitor.Dispose();
            _stop.Dispose();
        }
    }
}
