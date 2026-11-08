# Tasks

## 1. Brand art

- [x] 1.1 Replace `assets/brand/upsy-mascot.svg` with the owner's drawing in the mid-century palette without the exclamation marks; verify by rendering it and comparing with the owner's file (shapes equal, three paths fewer).
- [x] 1.2 Add `upsy-icon-16.svg` and `upsy-icon-32.svg`, remove `upsy-icon.svg`; verify by rendering 16/20/24/32 px on light and dark backgrounds.

## 2. Mock-up

- [x] 2.1 Update `design/upsy.design.html`: header, footers, dialog bars and toasts use the new icons; Brand section shows the mascot, the size ladder 64-16 px, the palette without olive and the new description; verify with a headless screenshot of the Brand and Notifications frames in light and dark.
- [x] 2.2 Review the mock-up with impeccable and /ux-crux-review; verify that no blocking finding is open.

## 3. Generated bitmaps

- [x] 3.1 Add `tools/gen_brand_icons.py`; verify that it writes `upsy.ico` (16/20/24/32/40/48/64/96/256), `upsy-16.png`, `upsy-32.png` and `docs/images/mascot.png`, and that a second run leaves `git status` clean.

## 4. App

- [x] 4.1 Add `BrandIcon` and use it in the flyout footer and the three dialog title bars; dialog `Window.Icon` from `upsy.ico`; verify with the quality gate (build with 0 warnings, test, format).
- [x] 4.2 Hardware check on Windows at 125 % (the only scale on the test machine): the footer and the sign-in, Add station and confirm bars show the 32 px art, the .exe shows the new icon, the app connects to the stations; verified by renders and the extracted .exe icon.
- [ ] 4.4 Deferred: at 100 % the footer and dialog bars show the 16 px art; the taskbar and Alt+Tab show the new icon (needs a 100 % display and an open dialog).
- [x] 4.3 Re-render `docs/images` with `upsy.exe --render` on Windows (render saves at 192 dpi; `BrandIcon` picks the 32 px art at any scale above 100 %, so 125 % gives the same result as 200 %); the five flyout screenshots are the renders at 50 %, and a diff against the old ones shows only the footer icon and the clock; verify that the README screenshots show the new footer icon.

## 5. Docs

- [x] 5.1 Update DESIGN.md Brand, PRODUCT.md Brand Commitments, README (hero alt text, tools list) and AGENTS.md (generator, its tools); verify with a grep that no doc outside the archive describes the daisy art or `upsy-icon.svg`.
