# Proposal

## Why

The owner drew a new mascot for upsy: a battery with a raised fist. It replaces the daisy battery in every brand surface. The current app icon is also one drawing scaled to every size, and the flyout footer and dialog title bars show a 256 px bitmap shrunk to 16 px, so the brand is a blur where users see it most.

## What Changes

- New mascot in `assets/brand/upsy-mascot.svg`: the owner's drawing in the mid-century palette (mustard body, teal cap and belly, rust cheeks, cream, ink), without the exclamation marks next to the fist.
- Art per size for the app icon: a hand-placed 16 px icon (`upsy-icon-16.svg`), a small icon for 20-32 px (`upsy-icon-32.svg`), the mascot from 40 px up. The old bust-on-a-tile icon (`upsy-icon.svg`) is removed.
- `tools/gen_brand_icons.py` writes `upsy.ico`, the WPF bitmaps and the README hero from these SVGs.
- The flyout footer and dialog title bars show the 16 px or 32 px bitmap for the current DPI; dialog windows take their icon from `upsy.ico`.
- Mock-up Brand section, DESIGN.md, PRODUCT.md, README and AGENTS.md describe the new brand. The palette drops olive, which the art does not use.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. The brand is art and documentation; no spec requirement changes (`skip_specs: true`).

## Impact

- Files: `assets/brand/*`, `src/Upsy/Assets/*`, `src/Upsy/Upsy.csproj`, the four XAML files with the 16-DIP brand image, `src/Upsy/Dialogs/DialogWindow.cs`, a new `BrandIcon` control, `design/upsy.design.html`, `docs/images/mascot.png`, DESIGN.md, PRODUCT.md, README.md, AGENTS.md.
- New developer tools for the generator only: `rsvg-convert` (librsvg) and ImageMagick 7. The app gets no new dependency.

## Out of scope

- The tray icon with the percentage: it shows data, not the brand.
- Redrawing the mascot.

## Later

- Re-render `docs/images/flyout-*.png` with `upsy.exe --render` on Windows so README screenshots show the new footer icon.
- Check which icon the Windows notification header shows for NotifyIcon balloons; the mock-up shows the app icon there.
