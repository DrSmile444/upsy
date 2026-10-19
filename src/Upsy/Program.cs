using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Upsy.Core.Link;
using Upsy.Core.Presentation;
using Upsy.Core.Station;
using Upsy.Dialogs;
using Upsy.Flyout;
using Upsy.Infrastructure;
using Upsy.Tray;

namespace Upsy;

public static class Program
{
    private const string MutexName = @"Local\upsy-single-instance";
    private static readonly int[] ThresholdPreview = [10, 15, 20, 25, 30];

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--render")
        {
            return RenderPreview(args[1]);
        }

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirst);
        if (!isFirst)
        {
            return 0;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Log.Error("unhandled UI exception", e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("unhandled exception", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("unobserved task exception", e.Exception);
            e.SetObserved();
        };

        Log.Info("upsy started");
        using var tray = new TrayApp(app.Dispatcher);
        tray.Start();
        app.Run();
        Log.Info("upsy stopped");
        return 0;
    }

    /// <summary>Dev aid: renders every tray icon state, flyout state, menu and window to PNG files for review against the design.</summary>
    private static int RenderPreview(string dir)
    {
        Directory.CreateDirectory(dir);
        TrayIconState[] icons =
        [
            new(TrayGlyph.Number, 58, Tone.Neutral, false),
            new(TrayGlyph.Number, 54, Tone.Neutral, true),
            new(TrayGlyph.Full, 100, Tone.Neutral, false),
            new(TrayGlyph.Number, 18, Tone.Caution, false),
            new(TrayGlyph.Number, 7, Tone.Critical, false),
            new(TrayGlyph.NoData, 0, Tone.Neutral, false),
            new(TrayGlyph.Paused, 0, Tone.Neutral, false),
            new(TrayGlyph.Number, 84, Tone.Neutral, false, LevelOnly: true),
        ];
        foreach (var light in new[] { true, false })
        {
            foreach (var size in new[] { 16, 20, 24, 32 })
            {
                for (var i = 0; i < icons.Length; i++)
                {
                    using var bmp = TrayIconRenderer.Render(icons[i], size, light);
                    bmp.Save(Path.Combine(dir, $"tray-{(light ? "light" : "dark")}-{size}-{i}.png"));
                }
            }
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var light in new[] { true, false })
        {
            foreach (var (name, sections, expand) in PreviewStates())
            {
                var window = new FlyoutWindow { Left = -10000, Top = -10000 };
                window.ApplyTheme(light);
                window.Render(sections);
                if (expand is not null)
                {
                    window.ExpandDetails(expand);
                }

                window.Show();
                window.UpdateLayout();
                SaveWindow(window, Path.Combine(dir, $"flyout-{(light ? "light" : "dark")}-{name}.png"));
                window.Close();
            }

            RenderMenus(dir, light);
        }

        RenderDialogs(dir);
        app.Shutdown();
        return 0;
    }

    /// <summary>Flyout states for review. Placeholder identities: real serials never go into renders (README screenshots come from here).</summary>
    private static (string Name, StationSection[] Sections, string? Expand)[] PreviewStates()
    {
        var eb3aId = new StationIdentity("EB3A", "2240000000001", [new("ARM", "2063.02"), new("DSP", "2052.07")]);
        var settingsRegs = new ushort[Eb3aRegisters.SettingsCount];
        settingsRegs[0] = 4;              // 3034: LED off
        settingsRegs[3063 - 3034] = 1;    // ECO on
        settingsRegs[3064 - 3034] = 1;    // after 1 h
        var eb3aSettings = Eb3aRegisters.DecodeSettings(settingsRegs);
        var d2Id = new StationIdentity("DELTA 2", "R331EXAMPLE00001", [new(string.Empty, "1.2.1.63")]);
        var d2Settings = new StationSettings([new("Charge limit", "100%"), new("Discharge limit", "0%"), new("X-Boost", "On"), new("AC charging power", "200 W")]);
        var now = DateTimeOffset.Now;
        var eb3a = StationContext.Eb3a;
        var d2 = new StationContext("DELTA 2", StationCatalog.Delta2);
        var d2Level = d2 with { AdvertisedPercent = 84 };

        StationSection Section(string id, StationContext ctx, MonitorSnapshot snapshot, TimeSpan? runtime, bool tray, bool several) =>
            new(id, ctx.Profile, StatusText.Build(snapshot, runtime, 20, ctx), snapshot, tray, several);
        MonitorSnapshot Eb3a(Telemetry t) => new(LinkStatus.Connected, t, eb3aId, eb3aSettings, now, now, null);
        MonitorSnapshot D2(Telemetry t) => new(LinkStatus.Connected, t, d2Id, d2Settings, now, now, null);
        MonitorSnapshot Status(LinkStatus status, StationIdentity? id = null) =>
            new(status, null, id, null, null, now.AddMinutes(-27), status == LinkStatus.Paused ? now.AddMinutes(30) : null);

        var d2Battery = new Telemetry(82, 0, 0, 103, 0, true, false, 0, 0, ToEmpty: TimeSpan.FromMinutes(431));
        var d2Charging = new Telemetry(80, 0, 328, 101, 0, true, false, 233.3, 0, ToFull: TimeSpan.FromMinutes(73));
        var eb3aBattery = new Telemetry(58, 0, 0, 128, 0, true, false, 2.6, 0);
        return
        [
            ("battery", [Section("eb3a", eb3a, Eb3a(eb3aBattery), TimeSpan.FromMinutes(62), true, false)], null),
            ("low", [Section("eb3a", eb3a, Eb3a(new(18, 0, 0, 131, 0, true, false, 2.6, 0)), TimeSpan.FromMinutes(21), true, false)], null),
            ("charging", [Section("eb3a", eb3a, Eb3a(new(54, 0, 390, 126, 0, true, false, 228.4, 0)), null, true, false)], "eb3a"),
            ("full", [Section("eb3a", eb3a, Eb3a(new(100, 0, 126, 126, 0, true, false, 228.3, 0)), null, true, false)], null),
            ("bluetooth-off", [Section("eb3a", eb3a, Status(LinkStatus.BluetoothOff, eb3aId), null, true, false)], null),
            ("paused", [Section("eb3a", eb3a, Status(LinkStatus.Paused, eb3aId), null, true, false)], null),
            ("first-start", [new("first", StationCatalog.Eb3a, StatusText.FirstStart(), MonitorSnapshot.Initial, true, false)], null),
            ("two", [Section("eb3a", eb3a, Eb3a(new(100, 0, 46, 46, 0, true, false, 228, 0)), null, true, true),
                Section("d2", d2, D2(d2Battery), TimeSpan.FromMinutes(431), false, true)], null),
            ("two-delta2", [Section("d2", d2, D2(d2Charging), null, true, true),
                Section("eb3a", eb3a, Eb3a(eb3aBattery), TimeSpan.FromMinutes(62), false, true)], "d2"),
            ("level-signin", [Section("d2", d2Level, Status(LinkStatus.SignInNeeded), null, true, false)], null),
            ("level-rejected", [Section("d2", d2Level, Status(LinkStatus.KeyRejected), null, true, false)], null),
            ("level-keytable", [Section("d2", d2Level, Status(LinkStatus.KeyTableMissing), null, true, false)], null),
            ("level-busy", [Section("d2", d2Level, Status(LinkStatus.Searching) with { Failures = StatusText.BusyAfterFailures }, null, true, false)], null),
        ];
    }

    private static void RenderMenus(string dir, bool light)
    {
        var theme = light ? "light" : "dark";
        var threshold = TrayMenu.Item("Low battery alert", "", () => { }, meta: "20%");
        threshold.Items.Add(new System.Windows.Controls.MenuItem());
        System.Windows.Controls.Control[] main =
        [
            TrayMenu.Title("EB3A · 58% · 128 W out · about 1 h left"),
            TrayMenu.Item("Open upsy", string.Empty, () => { }, bold: true),
            TrayMenu.Item("Pause for 30 minutes", "", () => { }),
            TrayMenu.Item("Add station…", "", () => { }),
            new System.Windows.Controls.Separator(),
            TrayMenu.Item("Start with Windows", string.Empty, () => { }, isChecked: true),
            threshold,
            new System.Windows.Controls.Separator(),
            TrayMenu.Item("Exit", "", () => { }),
        ];
        SaveVisual(TrayMenu.Build(main, light), Path.Combine(dir, $"menu-{theme}.png"));
        System.Windows.Controls.Control[] steps = [.. ThresholdPreview.Select(v => TrayMenu.Item($"{v}%", string.Empty, () => { }, isChecked: v == 20))];
        var sub = TrayMenu.Build(steps, light);
        sub.MinWidth = 136;
        SaveVisual(sub, Path.Combine(dir, $"menu-{theme}-threshold.png"));

        var show = TrayMenu.Item("Show in tray", "", () => { }, meta: "EB3A");
        show.Items.Add(new System.Windows.Controls.MenuItem());
        var lowAlert = TrayMenu.Item("Low battery alert", "", () => { }, meta: "20%");
        lowAlert.Items.Add(new System.Windows.Controls.MenuItem());
        System.Windows.Controls.Control[] multi =
        [
            TrayMenu.Title("EB3A · 100% · on mains"),
            TrayMenu.Item("Open upsy", string.Empty, () => { }, bold: true),
            TrayMenu.Item("Pause EB3A for 30 minutes", "", () => { }),
            show,
            TrayMenu.Item("Add station…", "", () => { }),
            new System.Windows.Controls.Separator(),
            TrayMenu.Item("Start with Windows", string.Empty, () => { }, isChecked: true),
            lowAlert,
            TrayMenu.Item("Sign out of EcoFlow", "", () => { }),
            new System.Windows.Controls.Separator(),
            TrayMenu.Item("Exit", "", () => { }),
        ];
        SaveVisual(TrayMenu.Build(multi, light), Path.Combine(dir, $"menu-{theme}-multi.png"));
        System.Windows.Controls.Control[] stationTray = [TrayMenu.Item("EB3A", string.Empty, () => { }, isChecked: true), TrayMenu.Item("DELTA 2", string.Empty, () => { })];
        var trayChoice = TrayMenu.Build(stationTray, light);
        trayChoice.MinWidth = 136;
        SaveVisual(trayChoice, Path.Combine(dir, $"menu-{theme}-show-in-tray.png"));
        System.Windows.Controls.Control[] station =
        [
            TrayMenu.Title("DELTA 2 · 82% · 103 W out"),
            TrayMenu.Item("Pause for 30 minutes", "", () => { }),
            TrayMenu.Item("Show in tray", "", () => { }),
            new System.Windows.Controls.Separator(),
            TrayMenu.Item("Forget this station…", "", () => { }),
        ];
        SaveVisual(TrayMenu.Build(station, light), Path.Combine(dir, $"menu-{theme}-station.png"));
    }

    /// <summary>The sign-in, Add station and Forget windows, in the current Windows apps theme.</summary>
    private static void RenderDialogs(string dir)
    {
        var theme = WindowsTheme.AppsAreLight ? "light" : "dark";
        void Save(DialogWindow window, string name)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -10000;
            window.Top = -10000;
            window.Show();
            window.UpdateLayout();
            SaveWindow(window, Path.Combine(dir, $"{name}-{theme}.png"));
            window.Close();
        }

        Save(new SignInWindow("DELTA 2", (_, _, _) => Task.FromResult(new Core.Protocol.EcoFlow.EcoFlowSignIn(null, null))), "signin");
        NearbyStation[] nearby =
        [
            new(new DiscoveredStation(StationCatalog.Delta2, 0x01, "R331EXAMPLE00001", "R33-0001", 84, -60), false),
            new(new DiscoveredStation(StationCatalog.Eb3a, 0x02, "2240000000001", "EB3A2240000000001", null, -70), true),
        ];
        Save(new AddStationWindow(() => nearby), "add-station");
        Save(new AddStationWindow(() => []), "add-station-empty");
        Save(new ConfirmWindow(
            "Forget DELTA 2?",
            "upsy stops watching this station: no data, no alerts, and it is not reconnected after a restart. You can add it again from Add station; your EcoFlow sign-in is kept.",
            "Forget"), "forget");
    }

    private static void SaveWindow(Window window, string path)
    {
        var target = new RenderTargetBitmap((int)window.ActualWidth * 2, (int)window.ActualHeight * 2, 192, 192, PixelFormats.Pbgra32);
        target.Render((Visual)window.Content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private static void SaveVisual(FrameworkElement element, string path)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        element.Arrange(new Rect(element.DesiredSize));
        element.UpdateLayout();
        var target = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * 2), (int)Math.Ceiling(element.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
        target.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
