# OpenClaw Windows Native Management Client — Implementation Plan

Build a WinUI 3 + WebView2 remote management shell for OpenClaw Gateway on Windows. The app is a thin native shell around the existing remote OpenClaw Web UI, targeting .NET 10 with Windows App SDK 1.8.5.

## User Review Required

> [!IMPORTANT]
> **Project Name**: I'll use `OpenClawManager` as the project/solution name. Change this if you prefer another name from the spec (e.g., `OpenClawControl.Windows` or `OpenClawRemote.WinUI`).

> [!IMPORTANT]
> **Target Framework**: .NET 10 (`net10.0-windows10.0.26100.0`) with Windows App SDK 1.8.5. Research confirms this combination is compatible. There is a **reported rendering issue** with WebView2 on early .NET 10 builds, but WinAppSDK 1.8.5 includes fixes. We will validate in Phase 0.

> [!IMPORTANT]
> **Scope**: This plan covers **Phase 0 (Setup) + Phase 1 (MVP Foundation)**. This means the core shell, WebView2 hosting, environment management, settings, reload, open-in-browser, stop, clear session, connection status, basic error UI, file upload, and logging. Phases 2–5 from the spec will follow in subsequent iterations.

> [!WARNING]
> **Packaging**: The app will be created as a **packaged WinUI 3 Desktop** app (MSIX) since unpackaged mode has known WebView2 issues. This means you'll need to trust a developer certificate for sideloading during development.

---

## Proposed Changes

### Solution Structure

```
Claw_winui3/
├── OpenClawManager.sln
├── src/
│   └── OpenClawManager/
│       ├── OpenClawManager.csproj
│       ├── Package.appxmanifest
│       ├── app.manifest
│       ├── App.xaml / App.xaml.cs
│       ├── MainWindow.xaml / MainWindow.xaml.cs
│       ├── Assets/                          # App icons and images
│       ├── Strings/
│       │   └── en-us/
│       │       └── Resources.resw           # Centralized English strings
│       ├── Models/
│       │   ├── EnvironmentConfig.cs         # Gateway environment model
│       │   └── AppSettings.cs               # Application settings model
│       ├── Services/
│       │   ├── ConfigurationService.cs      # Load/save JSON config
│       │   ├── WebViewService.cs            # WebView2 lifecycle management
│       │   └── LoggingService.cs            # Structured local logging
│       ├── ViewModels/
│       │   ├── MainViewModel.cs             # Main window state
│       │   └── SettingsViewModel.cs         # Settings page state
│       ├── Views/
│       │   └── SettingsDialog.xaml / .cs     # Settings/environment management
│       └── Helpers/
│           ├── StringResources.cs           # Typed string resource accessor
│           └── RelayCommand.cs              # ICommand implementation
```

---

### Core Project Setup

#### [NEW] [OpenClawManager.sln](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/OpenClawManager.sln)

Solution file referencing the main project.

#### [NEW] [OpenClawManager.csproj](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/OpenClawManager.csproj)

- Target: `net10.0-windows10.0.26100.0`
- References: `Microsoft.WindowsAppSDK` 1.8.5, `Microsoft.Windows.SDK.BuildTools`, `CommunityToolkit.Mvvm` (for MVVM helpers)
- Output type: WinExe
- UseWinUI: true

#### [NEW] [Package.appxmanifest](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/Package.appxmanifest)

MSIX packaging manifest with app identity, capabilities (internet access, file access), and display name.

#### [NEW] [App.xaml](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/App.xaml) / [App.xaml.cs](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/App.xaml.cs)

- Application entry point
- Initialize services (configuration, logging)
- Create and activate `MainWindow`
- Global exception handling

---

### Main Window (Shell)

#### [NEW] [MainWindow.xaml](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/MainWindow.xaml) / [MainWindow.xaml.cs](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/MainWindow.xaml.cs)

Layout following the Information Architecture from the spec:

- **Top Bar**: Custom title bar area with:
  - Environment selector (`ComboBox`)
  - Current Gateway URL display
  - Connect / Reload button
  - Stop button
  - Open in Browser button
  - Settings button (opens dialog)
- **Main Area**: `WebView2` control filling remaining space
- **Bottom Status Bar**: `InfoBar` / text block showing:
  - Connection state (connected / reconnecting / auth failed / offline)
  - Current environment name
  - Loading / idle indicator

Key behaviors:
- Window size and position remembered across sessions
- WebView2 initialized with custom user data folder under `%LOCALAPPDATA%\OpenClawManager`
- Navigation events monitored for connection state
- Title bar uses Mica material (WinUI 3 theming)

---

### Models

#### [NEW] [EnvironmentConfig.cs](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/Models/EnvironmentConfig.cs)

```csharp
public class EnvironmentConfig
{
    public string Name { get; set; }        // e.g. "Production", "Test"
    public string GatewayUrl { get; set; }  // e.g. "https://my-claw.example.com"
    public bool IsDefault { get; set; }
}
```

#### [NEW] [AppSettings.cs](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/Models/AppSettings.cs)

```csharp
public class AppSettings
{
    public List<EnvironmentConfig> Environments { get; set; }
    public string SelectedEnvironmentName { get; set; }
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public double WindowLeft { get; set; }
    public double WindowTop { get; set; }
}
```

---

### Services

#### [NEW] [ConfigurationService.cs](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/Services/ConfigurationService.cs)

- Load/save `AppSettings` as JSON to `%LOCALAPPDATA%\OpenClawManager\settings.json`
- Uses `System.Text.Json` with source generators for AOT-friendliness
- Thread-safe, auto-saves on change

#### [NEW] [WebViewService.cs](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/Services/WebViewService.cs)

- Initialize `CoreWebView2` with custom environment (user data folder)
- Navigate to gateway URL
- Reload, clear browsing data
- Execute JavaScript (for Stop command injection)
- Monitor navigation/load events for connection status
- Handle WebView2 initialization failures gracefully

#### [NEW] [LoggingService.cs](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/Services/LoggingService.cs)

- Write structured log entries (JSON lines) to `%LOCALAPPDATA%\OpenClawManager\logs\`
- Rotate log files by date
- Levels: Info, Warning, Error
- Log: startup, navigation, errors, user actions

---

### ViewModels (MVVM)

#### [NEW] [MainViewModel.cs](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/ViewModels/MainViewModel.cs)

- Exposes: `Environments`, `SelectedEnvironment`, `ConnectionStatus`, `IsLoading`, `StatusMessage`
- Commands: `ReloadCommand`, `StopCommand`, `OpenInBrowserCommand`, `ClearSessionCommand`, `OpenSettingsCommand`
- Reacts to environment changes → navigates WebView2

#### [NEW] [SettingsViewModel.cs](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/ViewModels/SettingsViewModel.cs)

- CRUD operations for environments
- Save/cancel behavior
- Validation (URL format, name uniqueness)

---

### Views

#### [NEW] [SettingsDialog.xaml](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/Views/SettingsDialog.xaml) / [SettingsDialog.xaml.cs](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/Views/SettingsDialog.xaml.cs)

- `ContentDialog` for managing environments
- List of environments with Add / Edit / Remove
- Fields: Name, Gateway URL, Set as Default
- Save / Cancel actions

---

### Helpers

#### [NEW] [StringResources.cs](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/Helpers/StringResources.cs)

Typed accessor for `.resw` string resources. Enables centralized English strings and future i18n.

#### [NEW] [RelayCommand.cs](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/Helpers/RelayCommand.cs)

Simple `ICommand` implementation (or use `CommunityToolkit.Mvvm`'s `[RelayCommand]` attribute).

---

### String Resources

#### [NEW] [Resources.resw](file:///c:/Users/Zen/Repo/Codings/Claw_winui3/src/OpenClawManager/Strings/en-us/Resources.resw)

All user-facing English strings centralized here:
- Button labels (Reload, Stop, Open in Browser, Settings, Clear Session)
- Status messages (Connected, Reconnecting, Error, Offline)
- Settings dialog labels
- Error messages

---

### Key Implementation Details

**Stop Button (MVP)**:
- Inject JavaScript into WebView2 page context to trigger `/stop` command
- Isolated in `WebViewService` for easy replacement when a cleaner API becomes available
- Selector/injection logic centralized per spec risk mitigation

**File/Image Upload**:
- WebView2 natively supports `<input type="file">` via its built-in file picker
- For clipboard image paste: intercept `Ctrl+V`, read clipboard bitmap, convert to file, inject into page
- Drag-and-drop: forward drop events from native window to WebView2

**Connection Status**:
- Monitor `WebView2.NavigationCompleted` (success/failure)
- Monitor `WebView2.CoreWebView2.ProcessFailed` events
- Periodic lightweight health check (optional, could check if page responded)

---

## Verification Plan

### Build Verification
1. Run `dotnet build` from the project directory:
   ```
   dotnet build c:\Users\Zen\Repo\Codings\Claw_winui3\src\OpenClawManager\OpenClawManager.csproj
   ```
2. Confirm zero errors and zero warnings (or only expected warnings)

### Launch Verification
1. Run the app from Visual Studio or via:
   ```
   dotnet run --project c:\Users\Zen\Repo\Codings\Claw_winui3\src\OpenClawManager\OpenClawManager.csproj
   ```
2. Confirm the main window appears with the expected layout (top bar, WebView2 area, status bar)

### WebView2 Verification
1. Configure a test environment URL (e.g., `https://www.bing.com` or a real OpenClaw gateway URL)
2. Confirm the page loads inside the WebView2 area
3. Confirm navigation events fire correctly (status bar updates)

### Manual Testing (User)
Since this is a GUI desktop app with WebView2 embedding, most testing must be manual:

1. **Launch**: App opens, window appears with Mica title bar
2. **Environment Management**: Open Settings → Add environment → Save → See it in selector
3. **Navigation**: Select environment → WebView2 loads the URL → Status bar shows "Connected"
4. **Reload**: Click Reload → Page refreshes
5. **Open in Browser**: Click Open in Browser → Default browser opens with the same URL
6. **Stop**: Click Stop → Verify JS injection runs (may need a real OpenClaw gateway)
7. **Clear Session**: Click Clear Session → WebView2 browsing data cleared
8. **Error State**: Disconnect network → Status bar shows error state
9. **Window Memory**: Resize window → Close → Reopen → Window restores to previous size

> [!NOTE]
> Unit tests for `ConfigurationService` and `Models` can be added later as a separate test project. For Phase 0+1, manual smoke testing is the primary verification method given the heavy GUI/WebView2 nature of the app.
