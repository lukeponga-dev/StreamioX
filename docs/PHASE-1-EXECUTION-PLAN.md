# StreamioX — Phase 1 Execution Plan

## Goal
Deliver a stable, native UWP Xbox shell with a controller-first navigation rail, a Home landing page, consistent TV safe-area handling, and predictable back/focus behavior. This phase does not add Stremio network calls or playback dependencies.

## Current repository baseline
- Solution: `StreamioX.slnx`
- App: `StreamioX.App`
- Target: `net10.0-windows10.0.26100.0`, UWP, x86/x64/arm64
- Existing UI: UWP `NavigationView`, nested content `Frame`, Home/Discover/Library/Add-ons/Player/Diagnostics/Settings routes
- Existing focus helper: `StreamioX.App.Helpers.FocusHelper`
- Existing theme dictionaries: `Themes/Colors.xaml` and `Themes/XboxFocus.xaml`

## Compatibility gate
The project declares both `<UseUwp>true</UseUwp>` and `<PublishAot>true</PublishAot>`. Do not assume this combination is supported by the installed UWP/.NET toolchain. Keep the existing target settings unchanged in Phase 1 and verify build/package compatibility on the Windows development machine before treating NativeAOT as a supported deployment path. Do not migrate to WinUI 3 or GDK in this phase.

## Tasks
1. Build `StreamioX.slnx` for x64 and record existing failures before changing framework settings.
2. Keep TV safe-area padding at the shell boundary only: 48 px left/right and 27 px top/bottom.
3. Keep the left navigation rail open, hide its toggle, and use a fixed 232 px pane (72 px compact width).
4. Ensure D-pad navigation, A activation, B/back behavior, cold-launch Home focus, and page focus restoration.
5. Keep the Home hero and Discover entry point; remove duplicate safe-area padding from child pages.
6. Build x64 Debug and Release; verify focus and safe-area behavior at 1080p and 4K on the target runtime.

## Acceptance criteria
- App launches into Home without a blank content frame.
- Home, Discover, Library, Add-ons, Player, Diagnostics, and Settings are reachable by controller.
- Back navigates page history first; with no history, focus returns to the selected shell destination.
- TV safe-area padding is applied exactly once.
- No framework, NativeAOT, or package-target changes without a successful local compatibility build.

## Out of scope
Stremio authentication, catalogs, add-on protocol changes, stream resolution, torrent bridge, playback, new dependencies, and performance claims not measured on console hardware.
