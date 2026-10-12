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
                DrawBattery(g, k, ink);
                using (var fill = new SolidBrush(state.Charging ? Ok(lightTaskbar) : ink))
                {
                    g.FillRectangle(fill, 3 * k, 6 * k, 8 * k, 4 * k);
                }

                break;
            case TrayGlyph.NoData:
                DrawBattery(g, k, dim);
                using (var pen = new Pen(Color.FromArgb(190, ink), 1.2f * k))
                {
                    g.DrawLine(pen, 2 * k, 14 * k, 13 * k, 2 * k);
                }

                break;
            case TrayGlyph.Paused:
                DrawBattery(g, k, dim);
                using (var bars = new SolidBrush(ink))
                {
                    g.FillRectangle(bars, 5 * k, 6.5f * k, 1.6f * k, 3 * k);
                    g.FillRectangle(bars, 7.6f * k, 6.5f * k, 1.6f * k, 3 * k);
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

    private static void DrawBattery(Graphics g, float k, Color color)
    {
        using var pen = new Pen(color, 1f * k);
        using var body = RoundedRect(new RectangleF(1.5f * k, 4.5f * k, 11 * k, 7 * k), 1.5f * k);
        g.DrawPath(pen, body);
        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, 13 * k, 6.5f * k, 1.5f * k, 3 * k);
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
