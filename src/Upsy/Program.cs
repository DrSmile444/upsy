using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Upsy.Core.Link;
using Upsy.Core.Presentation;
using Upsy.Core.Station;
using Upsy.Flyout;
using Upsy.Infrastructure;
using Upsy.Tray;

namespace Upsy;

public static class Program
{
    private const string MutexName = @"Local\upsy-single-instance";

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

    /// <summary>Dev aid: renders every tray icon state and flyout state to PNG files for review against the design.</summary>
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

        var id = new StationIdentity("EB3A", "2240000000001", "2063.02", "2052.07");
        var settings = new StationSettings(LedMode.Off, true, 1, ChargingMode.Standard, false);
        var now = DateTimeOffset.Now;
        (string Name, MonitorSnapshot Snapshot, TimeSpan? Runtime)[] states =
        [
            ("battery", new(LinkStatus.Connected, new(58, 0, 0, 128, 0, true, false, 2.6, 0), id, settings, now, now, null), TimeSpan.FromMinutes(62)),
            ("low", new(LinkStatus.Connected, new(18, 0, 0, 131, 0, true, false, 2.6, 0), id, settings, now, now, null), TimeSpan.FromMinutes(21)),
            ("charging", new(LinkStatus.Connected, new(54, 0, 390, 126, 0, true, false, 228.4, 0), id, settings, now, now, null), null),
            ("full", new(LinkStatus.Connected, new(100, 0, 126, 126, 0, true, false, 228.3, 0), id, settings, now, now, null), null),
            ("bluetooth-off", new(LinkStatus.BluetoothOff, null, id, null, null, now.AddMinutes(-27), null), null),
            ("paused", new(LinkStatus.Paused, null, id, null, null, now, now.AddMinutes(30)), null),
            ("first-start", MonitorSnapshot.Initial, null),
        ];

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var light in new[] { true, false })
        {
            foreach (var (name, snapshot, runtime) in states)
            {
                var window = new FlyoutWindow { Left = -10000, Top = -10000 };
                window.ApplyTheme(light);
                window.Render(StatusText.Build(snapshot, runtime, 20), snapshot);
                if (name == "charging")
                {
                    window.DetailsToggle.IsChecked = true;
                }

                window.Show();
                window.UpdateLayout();
                var target = new RenderTargetBitmap((int)window.ActualWidth * 2, (int)window.ActualHeight * 2, 192, 192, PixelFormats.Pbgra32);
                var visual = (Visual)window.Content;
                target.Render(visual);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(target));
                using (var file = File.Create(Path.Combine(dir, $"flyout-{(light ? "light" : "dark")}-{name}.png")))
                {
                    encoder.Save(file);
                }

                window.Close();
            }
        }

        app.Shutdown();
        return 0;
    }
}
