"""Generate the small EB3A station glyph from the owner's outline (assets/station/eb3a-outline.svg).

The outline is drawn at 800x740 with text labels and hairlines that disappear at flyout size. This keeps its
geometry, drops labels and fine details, thickens strokes for ~104 px, and tags the parts that follow live
data. It writes two files from one shape list so they never drift:

  assets/station/eb3a.svg                      the glyph as SVG (ids = live parts)
  src/Upsy/Flyout/FlyoutWindow.xaml            the same glyph as WPF shapes, between the station-glyph markers

Run: python tools/gen_station_glyph.py
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

# Stroke weights tuned for ~104 px wide (scale ~0.13): 16 -> ~2 px.
HEAVY, MID, LIGHT = 18, 13, 10

# (group, kind, attrs). group None = static outline.
SHAPES = [
    # Silhouette, handle, front bezel (static)
    (None, "path", dict(d="M111 139H61Q47 139 40 155L16 205Q8 220 8 247V686Q8 718 40 718H759Q790 718 790 685V247Q790 220 781 205L758 154Q750 139 735 139H686", w=HEAVY)),
    (None, "path", dict(d="M112 164V62Q112 10 166 10H632Q687 10 687 62V164H643V76Q643 54 618 54H180Q156 54 156 77V164Z", w=HEAVY)),
    (None, "rect", dict(x=47, y=245, width=706, height=439, rx=35, w=MID)),
    # Screen frame (the live gauge is drawn separately)
    (None, "rect", dict(x=265, y=272, width=270, height=148, rx=19, w=MID)),
    # DC output: car socket block + USB row
    ("dc", "rect", dict(x=91, y=274, width=155, height=207, rx=15, w=LIGHT)),
    ("dc", "circle", dict(cx=169, cy=362, r=52, w=MID)),
    ("dc", "circle", dict(cx=169, cy=362, r=26, w=LIGHT)),
    ("dc", "circle", dict(cx=133, cy=450, r=11, w=LIGHT)),
    ("dc", "circle", dict(cx=208, cy=450, r=11, w=LIGHT)),
    ("dc", "rect", dict(x=290, y=436, width=39, height=15, rx=3, w=LIGHT)),
    ("dc", "rect", dict(x=381, y=436, width=39, height=15, rx=3, w=LIGHT)),
    ("dc", "rect", dict(x=474, y=438, width=28, height=11, rx=5, w=LIGHT)),
    # Light
    ("light", "rect", dict(x=562, y=274, width=145, height=155, rx=14, w=LIGHT)),
    ("light", "circle", dict(cx=634, cy=340, r=40, w=MID)),
    ("light", "circle", dict(cx=634, cy=340, r=16, w=LIGHT)),
    # Input section frame (static) + AC in (IEC socket) + DC in (round)
    (None, "rect", dict(x=98, y=500, width=299, height=163, rx=16, w=LIGHT)),
    (None, "circle", dict(cx=138, cy=581, r=18, w=LIGHT)),
    ("acin", "rect", dict(x=186, y=539, width=99, height=88, rx=12, w=MID)),
    ("dcin", "circle", dict(cx=337, cy=586, r=38, w=MID)),
    ("dcin", "circle", dict(cx=337, cy=586, r=17, w=LIGHT)),
    # AC output
    ("ac", "rect", dict(x=434, y=500, width=270, height=163, rx=16, w=LIGHT)),
    ("ac", "circle", dict(cx=584, cy=582, r=60, w=MID)),
    ("ac", "dot", dict(cx=556, cy=580, r=10)),
    ("ac", "dot", dict(cx=612, cy=580, r=10)),
    ("ac", "rect", dict(x=453, y=559, width=23, height=49, rx=11, w=LIGHT)),
]

# Gauge on the station screen: a half ring like the real display, filled to the battery level.
GAUGE = dict(cx=400, cy=392, r=72, w=16)
PADS = {"ac": (434, 500, 270, 163, 16), "dc": (91, 274, 155, 207, 15)}
GROUP_NAMES = {"dc": "GDcOut", "light": "GLight", "acin": "GAcIn", "dcin": "GDcIn", "ac": "GAcOut"}


def svg_shape(kind, a, extra=""):
    if kind == "path":
        return f'<path d="{a["d"]}" stroke-width="{a["w"]}"{extra}/>'
    if kind == "rect":
        return f'<rect x="{a["x"]}" y="{a["y"]}" width="{a["width"]}" height="{a["height"]}" rx="{a["rx"]}" stroke-width="{a["w"]}"{extra}/>'
    if kind == "circle":
        return f'<circle cx="{a["cx"]}" cy="{a["cy"]}" r="{a["r"]}" stroke-width="{a["w"]}"{extra}/>'
    if kind == "dot":
        return f'<circle cx="{a["cx"]}" cy="{a["cy"]}" r="{a["r"]}" fill="currentColor" stroke="none"{extra}/>'
    raise ValueError(kind)


def xaml_shape(kind, a):
    if kind == "path":
        return f'<Path Data="{a["d"]}" StrokeThickness="{a["w"]}" />'
    if kind == "rect":
        return (f'<Rectangle Canvas.Left="{a["x"]}" Canvas.Top="{a["y"]}" Width="{a["width"]}" Height="{a["height"]}" '
                f'RadiusX="{a["rx"]}" RadiusY="{a["rx"]}" StrokeThickness="{a["w"]}" />')
    if kind == "circle":
        d = a["r"] * 2
        return (f'<Ellipse Canvas.Left="{a["cx"] - a["r"]}" Canvas.Top="{a["cy"] - a["r"]}" Width="{d}" Height="{d}" '
                f'StrokeThickness="{a["w"]}" />')
    if kind == "dot":
        d = a["r"] * 2
        return f'<Ellipse Tag="dot" Canvas.Left="{a["cx"] - a["r"]}" Canvas.Top="{a["cy"] - a["r"]}" Width="{d}" Height="{d}" />'
    raise ValueError(kind)


def gauge_track():
    g = GAUGE
    return f'M{g["cx"] - g["r"]} {g["cy"]} A{g["r"]} {g["r"]} 0 0 1 {g["cx"] + g["r"]} {g["cy"]}'


def write_svg():
    lines = [
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 800 740" width="104" height="96" fill="none" stroke="currentColor" '
        'stroke-linecap="round" stroke-linejoin="round" role="img" aria-label="Bluetti EB3A">',
        "  <!-- Generated by tools/gen_station_glyph.py from eb3a-outline.svg (owner's drawing). Do not edit by hand. -->",
    ]
    for name, (x, y, w, h, rx) in PADS.items():
        lines.append(f'  <rect id="{name}-pad" x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="none" stroke="none"/>')
    groups = {}
    for group, kind, a in SHAPES:
        groups.setdefault(group, []).append(svg_shape(kind, a))
    for group, items in groups.items():
        if group is None:
            lines += [f"  {i}" for i in items]
        else:
            lines.append(f'  <g id="{group}">')
            lines += [f"    {i}" for i in items]
            lines.append("  </g>")
    g = GAUGE
    lines.append(f'  <path id="gauge-track" d="{gauge_track()}" stroke-width="{g["w"]}" opacity=".35"/>')
    lines.append(f'  <path id="gauge-value" d="{gauge_track()}" stroke-width="{g["w"]}"/>')
    lines.append("</svg>")
    (ROOT / "assets/station/eb3a.svg").write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_xaml():
    out = [
        '<Viewbox x:Name="GlyphBox" Width="104" Height="96" Stretch="Uniform" VerticalAlignment="Center" AutomationProperties.Name="Station">',
        '  <Canvas x:Name="Glyph" Width="800" Height="740">',
    ]
    for name, (x, y, w, h, rx) in PADS.items():
        pad = "GAcPad" if name == "ac" else "GDcPad"
        out.append(f'    <Rectangle x:Name="{pad}" Canvas.Left="{x}" Canvas.Top="{y}" Width="{w}" Height="{h}" RadiusX="{rx}" RadiusY="{rx}" />')
    groups = {}
    for group, kind, a in SHAPES:
        groups.setdefault(group, []).append(xaml_shape(kind, a))
    for group, items in groups.items():
        if group is None:
            out += [f"    {i}" for i in items]
        else:
            out.append(f'    <Canvas x:Name="{GROUP_NAMES[group]}">')
            out += [f"      {i}" for i in items]
            out.append("    </Canvas>")
    g = GAUGE
    out.append(f'    <Path x:Name="GGaugeTrack" Data="{gauge_track()}" StrokeThickness="{g["w"]}" StrokeStartLineCap="Round" StrokeEndLineCap="Round" />')
    out.append(f'    <Path x:Name="GGaugeValue" StrokeThickness="{g["w"]}" StrokeStartLineCap="Round" StrokeEndLineCap="Round" />')
    out.append("  </Canvas>")
    out.append("</Viewbox>")
    target = ROOT / "src/Upsy/Flyout/FlyoutWindow.xaml"
    xaml = target.read_text(encoding="utf-8")
    begin = "<!-- station-glyph:begin (generated by tools/gen_station_glyph.py) -->"
    end = "<!-- station-glyph:end -->"
    head, rest = xaml.split(begin, 1)
    _, tail = rest.split(end, 1)
    indent = "        "
    body = "\n".join(indent + line for line in out)
    target.write_text(f"{head}{begin}\n{body}\n{indent}{end}{tail}", encoding="utf-8")


if __name__ == "__main__":
    write_svg()
    write_xaml()
    print("wrote assets/station/eb3a.svg and the glyph in src/Upsy/Flyout/FlyoutWindow.xaml")
