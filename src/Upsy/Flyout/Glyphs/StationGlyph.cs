using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Upsy.Core.Presentation;
using Upsy.Core.Station;

namespace Upsy.Flyout.Glyphs;

/// <summary>What the glyph shows: the level on the station's screen and which outputs and inputs are live.</summary>
/// <param name="Percent">Null without data: the screen's gauge is drawn dashed.</param>
/// <param name="LevelOnly">The level comes from the advertisement: a muted gauge, no live parts.</param>
public sealed record GlyphState(int? Percent, Tone Tone, bool LevelOnly, bool AcOut, bool DcOut, bool AcIn, bool DcIn, bool Light);

/// <summary>
/// Base of the generated per-model glyphs (Flyout/Glyphs/*Glyph.xaml, from tools/gen_station_glyph.py). Every model
/// uses the same part names; parts a model's front view lacks are absent and skipped.
/// </summary>
public class StationGlyph : UserControl
{
    public static StationGlyph For(StationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return profile.Key == StationCatalog.Delta2.Key ? new Delta2Glyph() : new Eb3aGlyph();
    }

    internal void Render(GlyphState state, Palette t)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(t);
        if (Part<Panel>("Glyph") is { } root)
        {
            foreach (var shape in Shapes(root))
            {
                shape.Stroke = t.Glyph;
                shape.Fill = Equals(shape.Tag, "dot") ? t.Glyph : null;
                shape.StrokeLineJoin = PenLineJoin.Round;
                shape.StrokeStartLineCap = shape.StrokeEndLineCap = PenLineCap.Round;
            }
        }

        foreach (var pad in new[] { Part<Shape>("GAcPad"), Part<Shape>("GDcPad") })
        {
            if (pad is not null)
            {
                pad.Stroke = null;
                pad.Fill = null;
            }
        }

        var track = Part<Path>("GGaugeTrack");
        var value = Part<Path>("GGaugeValue");
        if (track is null || value is null)
        {
            return;
        }

        track.Stroke = t.Glyph;
        track.Opacity = 0.35;
        if (state.Percent is not { } percent)
        {
            value.Visibility = Visibility.Collapsed;
            track.StrokeDashArray = [0.6, 1.2];
            return;
        }

        track.StrokeDashArray = null;
        value.Visibility = percent > 0 ? Visibility.Visible : Visibility.Collapsed;
        value.Data = GaugeArc(track.Tag as string, percent);
        value.Stroke = state.LevelOnly ? t.Ink3 : state.Tone switch
        {
            Tone.Ok => t.Ok,
            Tone.Caution => t.Caution,
            Tone.Critical => t.Critical,
            _ => t.Ink,
        };

        if (state.LevelOnly)
        {
            return;
        }

        Paint("GAcOut", state.AcOut ? t.GlyphOn : t.Glyph);
        Fill("GAcPad", state.AcOut ? t.AccentTint : null);
        Paint("GDcOut", state.DcOut ? t.GlyphOn : t.Glyph);
        Fill("GDcPad", state.DcOut ? t.AccentTint : null);
        Paint("GAcIn", state.AcIn ? t.Ok : t.Glyph);
        Paint("GDcIn", state.DcIn ? t.Ok : t.Glyph);
        Paint("GLight", state.Light ? t.GlyphOn : t.Glyph);
    }

    internal void Describe(string text) => AutomationProperties.SetName(this, text);

    /// <summary>The screen's half ring, filled from the left to <paramref name="percent"/>; <paramref name="geometry"/> is "cx cy r".</summary>
    private static PathGeometry GaugeArc(string? geometry, int percent)
    {
        var parts = (geometry ?? "0 0 1").Split(' ').Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();
        var (cx, cy, r) = (parts[0], parts[1], parts[2]);
        var angle = Math.PI * (1 - (Math.Clamp(percent, 0, 100) / 100.0));
        var figure = new PathFigure { StartPoint = new Point(cx - r, cy), IsClosed = false };
        figure.Segments.Add(new ArcSegment(new Point(cx + (r * Math.Cos(angle)), cy - (r * Math.Sin(angle))), new Size(r, r), 0, isLargeArc: false, SweepDirection.Clockwise, isStroked: true));
        var arc = new PathGeometry([figure]);
        arc.Freeze();
        return arc;
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

    private T? Part<T>(string name)
        where T : class => FindName(name) as T;

    private void Paint(string group, Brush brush)
    {
        if (Part<Panel>(group) is not { } panel)
        {
            return;
        }

        foreach (var shape in Shapes(panel))
        {
            shape.Stroke = brush;
            if (Equals(shape.Tag, "dot"))
            {
                shape.Fill = brush;
            }
        }
    }

    private void Fill(string pad, Brush? brush)
    {
        if (Part<Shape>(pad) is { } shape)
        {
            shape.Fill = brush;
        }
    }
}
