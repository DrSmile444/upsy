"""Generate the small station glyphs from the owner's outlines (assets/station/<model>-outline.svg).

The outlines are drawn large, with labels and hairlines that disappear at flyout size. This keeps their
geometry, drops labels and fine details, thickens strokes for the 104x96 glyph box, and tags the parts that
follow live data. Each model writes its files from one shape list so they never drift:

  assets/station/<model>.svg                   the glyph as SVG (ids = live parts)
  src/Upsy/Flyout/FlyoutWindow.xaml            EB3A only: the same glyph as WPF shapes, between the station-glyph markers

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

# DELTA 2 (outline 1086x1448, front view). AC outlets, 12 V and inputs are at the back, so the live parts are
# the screen gauge and the USB-A/USB-C panel (DC out). Scale is ~0.066 at 96 px tall: 24 -> ~1.6 px.
D2_HEAVY, D2_MID, D2_LIGHT = 30, 24, 20
DELTA2_SHAPES = [
    # Shell with the ribbed side grips, handle, shoulders (static)
    (None, "path", dict(d="M112 125Q112 108 140 103Q243 79 350 79H736Q844 79 947 103Q975 108 975 126V144C1000 146 1000 170 975 174C1000 177 1000 201 975 207C1000 210 1000 234 975 240C1000 243 1000 267 975 273C1000 276 1000 300 975 306C1000 309 1000 333 975 339C1000 342 1000 366 975 372C1000 375 1000 399 975 404V1257Q975 1324 919 1343Q871 1357 757 1357H329Q217 1357 169 1343Q112 1325 112 1257V404C87 402 87 378 111 372C87 369 87 345 111 339C87 336 87 312 111 306C87 303 87 279 111 273C87 270 87 246 111 240C87 237 87 213 111 207C87 204 87 180 111 174C87 171 87 147 111 144V125", w=D2_HEAVY)),
    (None, "path", dict(d="M150 102V411M310 83L335 215M936 102V411M776 83L751 215", w=D2_LIGHT)),
    (None, "path", dict(d="M232 1235V292Q233 271 258 258L334 217Q352 207 377 207H709Q733 207 752 217L828 258Q853 271 854 292V1235", w=D2_MID)),
    (None, "path", dict(d="M390 303Q353 300 325 280Q302 263 325 247L342 237Q355 227 378 227H708Q731 227 745 237L762 247Q785 263 762 280Q734 300 696 303Z", w=D2_LIGHT)),
    (None, "path", dict(d="M113 415Q172 410 265 411M821 411Q913 410 974 415", w=D2_LIGHT)),
    (None, "path", dict(d="M113 1225Q167 1233 231 1235L241 1247V1265Q241 1310 275 1338Q295 1357 329 1357M231 1237H854M974 1225Q920 1233 855 1235L846 1247V1265Q846 1310 812 1338Q792 1357 757 1357", w=D2_LIGHT)),
    # Front panel and screen frame (the live gauge is drawn separately)
    (None, "rect", dict(x=266, y=324, width=555, height=889, rx=65, w=D2_MID)),
    (None, "rect", dict(x=330, y=398, width=425, height=206, rx=18, w=D2_MID)),
    # Power button
    (None, "circle", dict(cx=543.5, cy=1111, r=44, w=D2_LIGHT)),
    # DC out: the USB panel with four USB-A and two USB-C ports
    ("dc", "path", dict(d="M482 961H337Q306 961 306 930V740Q306 708 337 708H750Q781 708 781 740V930Q781 961 750 961H605", w=D2_LIGHT)),
    ("dc", "rect", dict(x=335, y=744, width=66, height=34, rx=4, w=D2_LIGHT)),
    ("dc", "rect", dict(x=448, y=744, width=66, height=34, rx=4, w=D2_LIGHT)),
    ("dc", "rect", dict(x=574, y=744, width=66, height=34, rx=4, w=D2_LIGHT)),
    ("dc", "rect", dict(x=687, y=744, width=65, height=34, rx=4, w=D2_LIGHT)),
    ("dc", "rect", dict(x=341, y=862, width=83, height=46, rx=23, w=D2_LIGHT)),
    ("dc", "rect", dict(x=663, y=862, width=83, height=46, rx=23, w=D2_LIGHT)),
    (None, "rect", dict(x=496, y=926, width=95, height=45, rx=22.5, w=D2_LIGHT)),
]
DELTA2_GAUGE = dict(cx=542.5, cy=548, r=86, w=26)
DELTA2_PADS = {"dc": (306, 708, 475, 253, 31)}
# Crop to the drawing so the portrait glyph fills the 96 px height of the box.
DELTA2_VIEWBOX = (62, 54, 962, 1328)


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


def gauge_track(g=GAUGE):
    return f'M{g["cx"] - g["r"]} {g["cy"]} A{g["r"]} {g["r"]} 0 0 1 {g["cx"] + g["r"]} {g["cy"]}'


def write_svg(name, shapes, gauge, pads, viewbox, label):
    x, y, w, h = viewbox
    lines = [
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{x} {y} {w} {h}" width="104" height="96" fill="none" stroke="currentColor" '
        f'stroke-linecap="round" stroke-linejoin="round" role="img" aria-label="{label}">',
        f"  <!-- Generated by tools/gen_station_glyph.py from {name}-outline.svg (owner's drawing). Do not edit by hand. -->",
    ]
    for pad, (px, py, pw, ph, rx) in pads.items():
        lines.append(f'  <rect id="{pad}-pad" x="{px}" y="{py}" width="{pw}" height="{ph}" rx="{rx}" fill="none" stroke="none"/>')
    groups = {}
    for group, kind, a in shapes:
        groups.setdefault(group, []).append(svg_shape(kind, a))
    for group, items in groups.items():
        if group is None:
            lines += [f"  {i}" for i in items]
        else:
            lines.append(f'  <g id="{group}">')
            lines += [f"    {i}" for i in items]
            lines.append("  </g>")
    lines.append(f'  <path id="gauge-track" d="{gauge_track(gauge)}" stroke-width="{gauge["w"]}" opacity=".35"/>')
    lines.append(f'  <path id="gauge-value" d="{gauge_track(gauge)}" stroke-width="{gauge["w"]}"/>')
    lines.append("</svg>")
    (ROOT / f"assets/station/{name}.svg").write_text("\n".join(lines) + "\n", encoding="utf-8")


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
    write_svg("eb3a", SHAPES, GAUGE, PADS, (0, 0, 800, 740), "Bluetti EB3A")
    write_svg("delta2", DELTA2_SHAPES, DELTA2_GAUGE, DELTA2_PADS, DELTA2_VIEWBOX, "EcoFlow DELTA 2")
    write_xaml()
    print("wrote assets/station/eb3a.svg, assets/station/delta2.svg and the EB3A glyph in src/Upsy/Flyout/FlyoutWindow.xaml")
