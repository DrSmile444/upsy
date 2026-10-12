using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;
using Upsy.Ble;
using Upsy.Core.Alerts;
using Upsy.Core.Link;
using Upsy.Core.Power;
using Upsy.Core.Presentation;
using Upsy.Flyout;
using Upsy.Infrastructure;

namespace Upsy.Tray;

/// <summary>Composition root: monitor + derived state + tray icon + flyout + menu + notifications.</summary>
public sealed class TrayApp : IDisposable
{
    private static readonly int[] ThresholdSteps = [10, 15, 20, 25, 30];
    private readonly System.Windows.Threading.Dispatcher _ui;
    private readonly NotifyIcon _notify;
    private readonly ContextMenuStrip _menu = new() { RenderMode = ToolStripRenderMode.System, ShowImageMargin = false, ShowCheckMargin = true };
    private readonly StationMonitor _monitor;
    private readonly RuntimeEstimator _estimator = new(TimeProvider.System);
    private readonly AlertMonitor _alerts = new(TimeProvider.System);
    private readonly CancellationTokenSource _stop = new();
    private readonly FlyoutWindow _flyout = new();
    private AppSettings _settings;
    private MonitorSnapshot _snapshot = MonitorSnapshot.Initial;
    private System.Drawing.Icon? _icon;
    private Task? _run;

    public TrayApp(System.Windows.Threading.Dispatcher ui)
    {
        _ui = ui;
        _settings = SettingsStore.Load();
        _alerts.LowPercent = _settings.LowPercent;
        _monitor = new StationMonitor(new WinRtStationTransport(), TimeProvider.System, _settings.StationAddress);
        _monitor.Changed += (_, s) => _ui.BeginInvoke(() => OnSnapshot(s));
        _monitor.AddressLearned += (_, a) => _ui.BeginInvoke(() =>
        {
            _settings = _settings with { StationAddress = a };
            SettingsStore.Save(_settings);
            Log.Info($"station address {a:X12} remembered");
        });

        _notify = new NotifyIcon { Text = "upsy", Visible = true, ContextMenuStrip = _menu };
        _notify.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ToggleFlyout();
            }
        };
        _menu.Opening += (_, _) => BuildMenu();

        _flyout.PauseRequested += (_, _) => Pause();
        _flyout.ResumeRequested += (_, _) => _monitor.Resume();
        _flyout.MoreRequested += (_, _) => _menu.Show(Cursor.Position);
        _flyout.BluetoothSettingsRequested += (_, _) => Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Refresh();
    }

    public void Start() => _run = Task.Run(() => _monitor.RunAsync(_stop.Token));

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _stop.Cancel();
        try
        {
            _run?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
        }

        _notify.Visible = false;
        _notify.Dispose();
        _menu.Dispose();
        TrayIconRenderer.Destroy(_icon);
        _monitor.Dispose();
        _stop.Dispose();
        _flyout.Close();
    }

    private TimeSpan? Runtime() =>
        _snapshot.HasData ? _estimator.Estimate(_snapshot.Telemetry!, PowerReading.From(_snapshot.Telemetry!)) : null;

    private void OnSnapshot(MonitorSnapshot s)
    {
        var wasConnected = _snapshot.HasData;
        _snapshot = s;
        if (s.HasData)
        {
            if (!wasConnected)
            {
                _estimator.Reset();
                var t = s.Telemetry!;
                Log.Info($"connected to {s.Identity?.Model} {s.Identity?.Serial}: {t.BatteryPercent}%, in {t.AcInputW + t.DcInputW} W, out {t.AcOutputW + t.DcOutputW} W, AC {t.AcInputVolts} V");
            }

            _estimator.Add(s.Telemetry!);
            foreach (var alert in _alerts.Observe(s.Telemetry!))
            {
                var (title, body) = StatusText.Alert(alert, s.Telemetry!, Runtime(), s.Identity?.Model ?? "EB3A");
                Log.Info($"alert {alert}: {body}");
                _notify.ShowBalloonTip(10_000, title, body, alert is AlertKind.BatteryCritical or AlertKind.MainsLost ? ToolTipIcon.Warning : ToolTipIcon.Info);
            }
        }
        else
        {
            _alerts.ObserveNoData();
            if (wasConnected || s.Status != LinkStatus.Connected)
            {
                Log.Info($"link {s.Status}");
            }
        }

        Refresh();
    }

    private void Refresh()
    {
        var view = StatusText.Build(_snapshot, Runtime(), _settings.LowPercent);
        _notify.Text = view.Tooltip;

        var glyph = _snapshot.Status == LinkStatus.Paused ? TrayGlyph.Paused
            : view.Percent is null ? TrayGlyph.NoData
            : view.Percent >= 100 ? TrayGlyph.Full
            : TrayGlyph.Number;
        var state = new TrayIconState(glyph, view.Percent ?? 0, view.Tone, view.Power?.Charging == true);
        var size = SystemInformation.SmallIconSize.Width;
        using var bmp = TrayIconRenderer.Render(state, size, WindowsTheme.TaskbarIsLight);
        var old = _icon;
        _icon = TrayIconRenderer.ToIcon(bmp);
        _notify.Icon = _icon;
        TrayIconRenderer.Destroy(old);

        if (_flyout.IsVisible)
        {
            _flyout.Render(view, _snapshot);
        }
    }

    private void ToggleFlyout()
    {
        if (_flyout.IsVisible)
        {
            _flyout.Hide();
            return;
        }

        _flyout.ApplyTheme(WindowsTheme.AppsAreLight);
        _flyout.Render(StatusText.Build(_snapshot, Runtime(), _settings.LowPercent), _snapshot);
        _flyout.ShowNear(Cursor.Position);
    }

    private void Pause()
    {
        _monitor.Pause();
        Log.Info("paused by user");
    }

    private void BuildMenu()
    {
        _menu.Items.Clear();
        var view = StatusText.Build(_snapshot, Runtime(), _settings.LowPercent);
        _menu.Items.Add(new ToolStripLabel(view.Tooltip) { ForeColor = System.Drawing.SystemColors.GrayText });
        var open = new ToolStripMenuItem("Open upsy", null, (_, _) => ToggleFlyout());
        open.Font = new System.Drawing.Font(open.Font, System.Drawing.FontStyle.Bold);
        _menu.Items.Add(open);
        _menu.Items.Add(_snapshot.Status == LinkStatus.Paused
            ? new ToolStripMenuItem("Resume connection", null, (_, _) => _monitor.Resume())
            : new ToolStripMenuItem("Pause for 30 minutes", null, (_, _) => Pause()));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Start with Windows", null, (_, _) => Autostart.Set(!Autostart.IsEnabled)) { Checked = Autostart.IsEnabled });
        var threshold = new ToolStripMenuItem($"Low battery alert ({_settings.LowPercent}%)");
        foreach (var step in ThresholdSteps)
        {
            var value = step;
            threshold.DropDownItems.Add(new ToolStripMenuItem($"{value}%", null, (_, _) => SetThreshold(value)) { Checked = value == _settings.LowPercent });
        }

        _menu.Items.Add(threshold);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown()));
    }

    private void SetThreshold(int value)
    {
        _settings = _settings with { LowPercent = value };
        _alerts.LowPercent = value;
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
            Log.Info("resumed from sleep: reconnecting");
            _monitor.Reconnect();
        }
    }
}
