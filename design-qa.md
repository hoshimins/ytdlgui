# Design QA

## Comparison

- Target: `C:\Users\makun\.codex\generated_images\019f58d2-bf9c-7ec2-83a7-9d88c3eb3435\exec-198ab18f-c599-45aa-a18c-ea306947fd9e.png`
- Implementation: `D:\Document\Program\YtdlGUI\artifacts\qa\wpf-metadata-final.jpg`
- Viewport: 1426 x 1017 logical pixels, light theme
- State: valid YouTube URL inspected; real title, channel, duration, date, and thumbnail displayed
- Theme override: `--qa-light` and `--qa-dark` can be used for repeatable visual verification without changing the Windows system theme.

## Result

The implementation preserves the target's Fluent-style visual hierarchy: a quiet neutral background, one blue accent, a dominant URL field, a real thumbnail confirmation card, three preset choices, a clear primary action, a restrained progress surface, and collapsed secondary controls. Native WPF controls retain keyboard behavior and visible focus treatment. Real long-form metadata fits without clipping at the tested desktop viewport.

## Findings

- P3, icons: The implementation intentionally omits decorative preset and folder glyphs present in the concept. This reduces visual noise and avoids inconsistent text-glyph substitutes; labels remain unambiguous.
- P3, density: The implementation leaves more open space below the primary workflow than the concept. This does not affect scanning order or operation and gives expanded settings/log content room to grow.
- P3, state: The target depicts a 67% active download while the implementation evidence depicts the ready state at 0%. The same progress surface exposes percentage, speed, ETA, folder action, and switches to cancellation during download.
- P3, image metadata: Resolution is shown as an em dash when yt-dlp returns a null top-level width/height. The app now treats that provider result safely rather than failing metadata inspection.

## Checks

- Typography: Segoe UI/Windows Fluent defaults, clear label/title/body hierarchy, no clipped real metadata.
- Layout: consistent outer margin, aligned controls, 8px-class rounded surfaces, no overlap at the tested viewport.
- Color and contrast: neutral light surfaces, blue selected/primary state, visible disabled state, red inline error state. The dark theme uses a dedicated dark foreground brush on its bright accent button instead of fixed white text.
- Dark theme verification: launched the published app with `--qa-dark` and confirmed the enabled primary button on the actual WPF screen. The calculated text contrast is 7.26:1 normally and 7.91:1 on hover.
- Release metadata verification: launched the published app with `--qa-light` and confirmed that the footer displays `Movie Downloader 1.0.0`; the executable file version is `1.0.0.0`.
- Assets: real yt-dlp thumbnail used; no placeholder illustration or CSS/SVG substitute.
- Interaction: URL inspection is asynchronous; presets, browse, download, cancel, expanders, and clear action are native controls.
- Accessibility: tab-focusable native controls, system theme support, visible focus cues, resizable window, minimum size guard.

final result: passed
