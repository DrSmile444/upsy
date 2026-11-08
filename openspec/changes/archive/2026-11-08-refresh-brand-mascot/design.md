# Design

## Context

The brand appears in four places: the .exe icon (`src/Upsy/Assets/upsy.ico`, also used by Windows for the taskbar, Explorer and notifications), a 16-DIP image in the flyout footer and in the custom title bars of the sign-in, add-station and confirm dialogs (all `upsy-256.png` scaled down), the dialog `Window.Icon` (the same PNG), and the README hero. The mock-up frames that show them: "Flyout: on battery" and "Flyout: two stations" (footer), "Sign in to EcoFlow" and "Add station" (dialog bars), "Notifications", and "Brand" in `design/upsy.design.html`.

## Goals / Non-Goals

**Goals:**
- Each icon size shows art drawn for it.
- Every bitmap is generated from the SVGs by one script.

**Non-Goals:**
- Vector brand art inside WPF (no SVG library in the app; two bitmaps cover 100-200 % DPI).

## Decisions

- **Art per size.** 16 px: pixel art on a 16 grid; 20-32 px: a 32-grid icon with a heavier outline, bigger eyes and no highlight; 40 px and up: the mascot trimmed to a square. Rejected: one drawing scaled to every size (the face turns to noise under 32 px).
- **Mascot source.** The owner's drawing, recoloured, shapes untouched. Rejected: a redraw (owner rejected it); the blue original palette (owner chose mid-century); a light-teal variant (lowest contrast on a dark taskbar at 20-24 px).
- **WPF images.** `BrandIcon`, an `Image` subclass, shows `upsy-16.png` at a DPI scale of 1 and `upsy-32.png` above, and picks again on `DpiChanged`. Rejected: one 32 px PNG for all scales (the 16 px art is never used at 100 %); an .ico as `Image.Source` (WPF does not pick a frame by DPI).
- **Window icon.** `Window.Icon` loads the multi-frame `upsy.ico` as a WPF resource, so Windows gets the small and large frames. The `upsy.ico` embedded resource had no reader and becomes this resource. Rejected: `upsy-256.png` (Windows scales one bitmap for the title bar and Alt+Tab). With both uses gone, `upsy-256.png` is removed.
- **Generator.** `tools/gen_brand_icons.py` shells out to `rsvg-convert` and ImageMagick, strips metadata and PNG date chunks so reruns do not change the files. Rejected: a standard-library rasteriser (none exists); hand-exported files (they drift from the SVGs).
- **Palette.** Mustard, teal, rust, cream, ink. Olive is dropped because no brand art uses it.

## Risks / Trade-offs

- [The WPF build and the DPI switch cannot run on the owner's Mac] → a hardware-check task on Windows at 100 % and 150 %.
- [Explorer keeps the old icon in its cache after an update] → the install notes say to sign out or restart Explorer if the old icon stays.
- [Generator output depends on librsvg and ImageMagick versions] → outputs are committed; rerun only when an SVG changes.
