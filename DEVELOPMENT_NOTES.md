# OpenClaw Development Notes

## WinUI 3 Window Chrome And Theme Sync

This note records the debugging lesson from the v3.0.4 top-edge artifact fix.

### Symptom

The main window showed a 1px line at the very top of the custom title bar. On first launch in light mode the line appeared lighter than the title-bar surface. After switching between dark and light themes, the same edge could become black.

### Root Cause

The artifact was not a normal XAML border. It came from mixing three different ownership layers for the same visual edge:

- XAML title-bar surface (`AppTitleBar`)
- WinUI `AppWindow.TitleBar`
- native DWM caption and border attributes

The earlier `TopEdgeCover` workaround made the problem harder to reason about because it painted another 1px layer over the window. Removing that cover alone was not enough, because DWM still owned the real non-client border.

The stable fix was to make every layer use the same concrete color:

- `AppTitleBar.Background`
- `AppWindow.TitleBar.BackgroundColor`
- `AppWindow.TitleBar.InactiveBackgroundColor`
- `DWMWA_CAPTION_COLOR`
- `DWMWA_BORDER_COLOR`

Avoid relying on `Colors.Transparent` for title-bar surfaces that are not caption buttons. Avoid `DWMWA_BORDER_COLOR = COLOR_NONE` when the visual goal is a seamless colored top edge; set an explicit border color instead.

### Debugging Rules

- Treat custom title-bar artifacts as a multi-layer problem first, not as a XAML layout problem.
- Sample pixels from screenshots before changing code. A 1px white, black, or mismatched line usually reveals whether XAML, Mica, or DWM owns the visible edge.
- Do not cover native frame bugs with an extra XAML strip unless the native layer has already been proven impossible to control.
- Theme changes must go through the full native frame refresh path. Updating only managed XAML colors can leave DWM using stale light/dark state.
- Keep the main window and settings window on the same `WindowFrameHelper` contract so fixes do not diverge.

### Implementation Checklist

When changing window chrome or theme behavior:

1. Update the XAML title-bar surface.
2. Update `AppWindow.TitleBar` foreground, background, inactive, hover, and pressed colors.
3. Update DWM immersive dark mode, caption color, text color, and border color.
4. Refresh the non-client frame after theme changes.
5. Verify both startup theme and runtime dark/light switching.

Commands used for baseline verification:

```powershell
dotnet build OpenClaw.sln -c Debug -p:Platform=x64 --no-restore
dotnet run --project tests\OpenClaw.Tests\OpenClaw.Tests.csproj -c Debug --no-restore
```
