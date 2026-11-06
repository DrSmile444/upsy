"""Generate the app icon and brand bitmaps from the brand SVGs in assets/brand.

Each size uses the art drawn for it:

  16 px            upsy-icon-16.svg   hand-placed pixels
  20, 24, 32 px    upsy-icon-32.svg   small-size variant
  40 px and up     upsy-mascot.svg    the mascot, trimmed to a square

Outputs:

  src/Upsy/Assets/upsy.ico        application and window icon
  src/Upsy/Assets/upsy-16.png     BrandIcon at 100 % scale (flyout footer, dialog title bars)
  src/Upsy/Assets/upsy-32.png     BrandIcon above 100 %
  docs/images/mascot.png          README hero

Needs rsvg-convert (librsvg) and ImageMagick 7 (`magick`) on PATH.
Run: python tools/gen_brand_icons.py
"""
import shutil
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BRAND = ROOT / "assets" / "brand"
ASSETS = ROOT / "src" / "Upsy" / "Assets"

ICON_16 = BRAND / "upsy-icon-16.svg"
ICON_32 = BRAND / "upsy-icon-32.svg"
MASCOT = BRAND / "upsy-mascot.svg"

ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 256]

# No metadata or timestamps, so a rerun on unchanged SVGs leaves the files byte-identical.
STABLE = ["-strip", "-define", "png:exclude-chunks=date,time"]


def art(size: int) -> Path:
    return ICON_16 if size == 16 else ICON_32 if size <= 32 else MASCOT


def run(*args: str) -> None:
    subprocess.run(args, check=True)


def render(size: int, out: Path, tmp: Path) -> Path:
    svg = art(size)
    raw = tmp / f"raw-{size}.png"
    if svg == MASCOT:
        # Render large, trim to the figure, pad to a square, then scale down.
        run("rsvg-convert", "-w", "1024", str(svg), "-o", str(raw))
        run("magick", str(raw), "-trim", "+repage", "-background", "none", "-gravity", "center",
            "-extent", "%[fx:max(w,h)*1.04]x%[fx:max(w,h)*1.04]", "-filter", "Lanczos",
            "-resize", f"{size}x{size}", *STABLE, f"PNG32:{out}")
    else:
        run("rsvg-convert", "-w", str(size), "-h", str(size), str(svg), "-o", str(raw))
        run("magick", str(raw), *STABLE, f"PNG32:{out}")
    return out


def main() -> None:
    for tool in ("rsvg-convert", "magick"):
        if shutil.which(tool) is None:
            raise SystemExit(f"{tool} is not on PATH")
    with tempfile.TemporaryDirectory() as t:
        tmp = Path(t)
        frames = [str(render(size, tmp / f"{size}.png", tmp)) for size in ICO_SIZES]
        run("magick", *frames, *STABLE, str(ASSETS / "upsy.ico"))
    with tempfile.TemporaryDirectory() as t:
        tmp = Path(t)
        render(16, ASSETS / "upsy-16.png", tmp)
        render(32, ASSETS / "upsy-32.png", tmp)
        render(240, ROOT / "docs" / "images" / "mascot.png", tmp)
    print("wrote upsy.ico, upsy-16.png, upsy-32.png, docs/images/mascot.png")


if __name__ == "__main__":
    main()
