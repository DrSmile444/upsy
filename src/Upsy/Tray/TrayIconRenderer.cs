using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Upsy.Core.Presentation;

namespace Upsy.Tray;

public enum TrayGlyph
{
    Number,
    Full,
    NoData,
    Paused,
}

public sealed record TrayIconState(TrayGlyph Glyph, int Percent, Tone Tone, bool Charging);

/// <summary>Draws the notification-area icon (DESIGN.md "Tray icon") for the current taskbar theme and size.</summary>
public static partial class TrayIconRenderer
{
    private const string SymbolFont = "Segoe Fluent Icons";
    private const string SymbolFallback = "Segoe MDL2 Assets";
    private const string BatteryFull = "";
    private const string BatteryChargingFull = "";
    private const string BatteryEmpty = "";
    private const string BatteryUnknown = "";

    public static Bitmap Render(TrayIconState state, int size, bool lightTaskbar)
    {
        ArgumentNullException.ThrowIfNull(state);
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.Clear(Color.Transparent);

        var k = size / 16f;
        var ink = lightTaskbar ? Color.FromArgb(0x1B, 0x1B, 0x1B) : Color.White;
        var dim = Color.FromArgb(140, ink);

        switch (state.Glyph)
        {
            case TrayGlyph.Full:
                // Same Fluent glyphs the Windows battery flyout uses, so the icon reads as native.
                DrawSymbol(g, state.Charging ? BatteryChargingFull : BatteryFull, size, ink);
                break;
            case TrayGlyph.NoData:
                DrawSymbol(g, BatteryUnknown, size, Color.FromArgb(200, ink));
                break;
            case TrayGlyph.Paused:
                DrawSymbol(g, BatteryEmpty, size, dim);
                using (var bars = new SolidBrush(ink))
                {
                    var bw = Math.Max(1f, MathF.Round(1.5f * k));
                    var bh = MathF.Round(4 * k);
                    var top = MathF.Round((size - bh) / 2);
                    var cx = MathF.Round(7.5f * k);
                    g.FillRectangle(bars, cx - bw - MathF.Round(0.75f * k), top, bw, bh);
                    g.FillRectangle(bars, cx + MathF.Round(0.75f * k), top, bw, bh);
                }

                break;
            default:
                var color = state.Tone switch
                {
                    Tone.Caution => lightTaskbar ? Color.FromArgb(0x9D, 0x5D, 0x00) : Color.FromArgb(0xFC, 0xE1, 0x00),
                    Tone.Critical => lightTaskbar ? Color.FromArgb(0xC4, 0x2B, 0x1C) : Color.FromArgb(0xFF, 0x99, 0xA4),
                    _ => ink,
                };
                DrawDigits(g, state.Percent.ToString(System.Globalization.CultureInfo.InvariantCulture), size, k, color);
                var bar = state.Tone is Tone.Caution or Tone.Critical ? color : state.Charging ? Ok(lightTaskbar) : Color.Empty;
                if (!bar.IsEmpty)
                {
                    using var brush = new SolidBrush(bar);
                    using var path = RoundedRect(new RectangleF(1 * k, 14 * k, 14 * k, 2 * k), 1 * k);
                    g.FillPath(brush, path);
                }

                break;
        }

        return bmp;
    }

    /// <summary>Creates an <see cref="Icon"/> that owns its handle; dispose it with <see cref="Destroy"/>.</summary>
    public static Icon ToIcon(Bitmap bmp)
    {
        ArgumentNullException.ThrowIfNull(bmp);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public static void Destroy(Icon? icon)
    {
        if (icon is null)
        {
            return;
        }

        var handle = icon.Handle;
        icon.Dispose();
        _ = DestroyIcon(handle);
    }

    private static Color Ok(bool light) => light ? Color.FromArgb(0x0F, 0x7B, 0x0F) : Color.FromArgb(0x6C, 0xCB, 0x5F);

    private static void DrawDigits(Graphics g, string text, int size, float k, Color color)
    {
        var px = 12f * k;
        using var family = FontFamily.Families.FirstOrDefault(f => f.Name == "Segoe UI Variable Display") is { } variable
            ? new FontFamily(variable.Name)
            : new FontFamily("Segoe UI");
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.Alignment = StringAlignment.Center;
        format.LineAlignment = StringAlignment.Center;
        Font font;
        while (true)
        {
            font = new Font(family, px, FontStyle.Bold, GraphicsUnit.Pixel);
            if (g.MeasureString(text, font, int.MaxValue, format).Width <= size + (0.5f * k) || px < 7 * k)
            {
                break;
            }

            font.Dispose();
            px -= 0.5f * k;
        }

        using (font)
        using (var brush = new SolidBrush(color))
        {
            g.DrawString(text, font, brush, new RectangleF(0, -0.5f * k, size, 14 * k), format);
        }
    }

    /// <summary>
    /// Draws a Segoe Fluent Icons glyph centred in the icon. Rendered with WPF (DirectWrite): GDI+ drops this
    /// font at some pixel sizes (20 and 24 px, i.e. 125% and 150% scaling).
    /// </summary>
    private static void DrawSymbol(Graphics g, string glyph, int size, Color color)
    {
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
        var typeface = new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily($"{SymbolFont}, {SymbolFallback}"),
            System.Windows.FontStyles.Normal, System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal);
        var text = new System.Windows.Media.FormattedText(glyph, System.Globalization.CultureInfo.InvariantCulture,
            System.Windows.FlowDirection.LeftToRight, typeface, size, brush, 1.0);
        var bounds = text.BuildGeometry(new System.Windows.Point(0, 0)).Bounds;
        var origin = new System.Windows.Point(
            Math.Round(((size - bounds.Width) / 2) - bounds.X),
            Math.Round(((size - bounds.Height) / 2) - bounds.Y));

        var visual = new System.Windows.Media.DrawingVisual();
        System.Windows.Media.TextOptions.SetTextRenderingMode(visual, System.Windows.Media.TextRenderingMode.Grayscale);
        using (var dc = visual.RenderOpen())
        {
            dc.DrawText(text, origin);
        }

        var target = new System.Windows.Media.Imaging.RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        target.Render(visual);
        var pixels = new byte[size * size * 4];
        target.CopyPixels(pixels, size * 4, 0);

        using var layer = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        var data = layer.LockBits(new Rectangle(0, 0, size, size), System.Drawing.Imaging.ImageLockMode.WriteOnly, layer.PixelFormat);
        try
        {
            for (var row = 0; row < size; row++)
            {
                Marshal.Copy(pixels, row * size * 4, data.Scan0 + (row * data.Stride), size * 4);
            }
        }
        finally
        {
            layer.UnlockBits(data);
        }

        g.DrawImageUnscaled(layer, 0, 0);
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr handle);
}
