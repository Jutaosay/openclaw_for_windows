# OpenClaw Manager 优化建议

针对当前代码库（v3.0.4）的全面 review 结果。每条建议都标明：所在文件/行、问题描述、影响、以及可执行的修复方向。

按重要性分为三档：

- **P0（高优先级 / 正确性 & 架构性问题）**
- **P1（中优先级 / 明显的性能、可维护性收益）**
- **P2（低优先级 / 锦上添花）**

---

## 当前部署画像（来自 `%LOCALAPPDATA%\OpenClaw\settings.json`）

下面所有建议都按 **该用户的真实部署** 校准过：

| 项 | 值 | 备注 |
|---|---|---|
| Gateway URL | `https://ai.falsemeet.site` | 自定义域 → Cloudflare Tunnel → VPS 上的 OpenClaw Gateway |
| 环境数量 | 1（"Default"） | WebView2Data 实际只有 `Default_21B111CB` 一个 profile，约 **79 MB** |
| 心跳 interval | 30 s（默认 45 s）| 已比默认更激进 |
| 心跳 failureThreshold | 3（默认 2）| 已比默认更宽容（容忍 90 s 的 CF 抖动） |
| 心跳 connectingThreshold | 3（默认 4）| 已比默认更激进 |
| reconnectDelayMs | 500（默认 1200）| 已大幅降低首次重连延迟 |
| maxReconnectDelayMs | 30000（默认 45000）| 已比默认更短 |
| hardRefreshCooldownSeconds | 45（默认 75）| 已比默认更短 |
| backgroundResumeThresholdSeconds | 10（默认 10）| 默认 |
| 日志文件夹 | 29 个 `openclaw-YYYY-MM-DD.log`，今日 ≈ 109 KB | **完全没有 retention，文件持续累积** |

> 说明：用户已经手动调小了多个超时常量来对抗 Cloudflare Tunnel 偶发的 100 s idle 断链——这意味着关于"恢复策略 / 重连节奏"的优化要避免反向破坏这套已经调好的参数。下文 #9、#13、新增的 P1-#31（CF Tunnel 节奏校准）都基于这一组真实数值。

OpenClaw 上游一些与本壳子直接相关的事实（来自 `openclaw/openclaw` 与 `docs.openclaw.ai`）：

- Control UI 是 **Vite + Lit SPA**，由 Gateway 直接 serve，可通过 `gateway.controlUi.basePath` 加前缀（如 `/openclaw`）。
- Control UI 走 **`wss://<host>/` 根路径** 与 Gateway 通信——CF Tunnel 的路由规则必须把 `/` 当成 catch-all 命中 Gateway，且 `/dashboard` 这种特定路径要排在 catch-all **前面**。
- Gateway 有 `gateway.controlUi.allowedOrigins` 配置；本壳子 [HostedUiBridge 的 JS 探测](src/OpenClaw/Services/HostedUiBridge.cs) 中专门识别 "origin rejected" / "trusted-proxy" 等错误，与上游错误码一一对应。
- 上游 issue [#26765](https://github.com/openclaw/openclaw/issues/26765)：从 `*.trycloudflare.com` 临时隧道访问 Control UI 会因 Origin 校验拒绝；用户当前用的是自定义域 `ai.falsemeet.site`，不会踩这个坑，但若以后切临时隧道做演示需注意。

---

## P0 — 正确性与架构

### 1. WebView2 多环境隔离的实现可能并未真正生效（当前是潜在风险）

[Services/WebViewService.cs:117-134](src/OpenClaw/Services/WebViewService.cs#L117-L134)

```csharp
Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataFolder);
await _webView.EnsureCoreWebView2Async();
```

每次切换环境都重置进程级环境变量。**WebView2 在同一个进程中只能有一份 `userDataFolder`**：第一次 `EnsureCoreWebView2Async()` 决定了运行时 Browser 进程使用的 user-data 目录，之后再修改环境变量、再创建新的 `WebView2` 控件，新的控件会复用已经存在的 Browser 进程，于是环境隔离失效。

**当前部署的具体影响**：用户目前 settings.json 里只配置了一个环境（`Default → ai.falsemeet.site`），WebView2Data 也只有 `Default_21B111CB` 一个 79 MB 的 profile，**这个 bug 现在还没暴露**。但只要某一天再加一个环境（比如 staging / 测试），新环境会"看上去隔离了"（因为目录名变了、cookie / localStorage 似乎也分了），但 Browser 进程级的 cache、IndexedDB、HSTS 等其实仍然来自首次创建的环境——表现是难复现的认证残留 / 状态污染，且 README 宣传的"per-environment session isolation"会被打破。

**修复方向**（既然现在还没多环境，用户可以选择最便宜的方案）：
- **最简：单 profile 模式**。把 `GetUserDataFolderForEnvironment()` 改回固定 `WebView2Data` 单目录，环境切换通过 `Navigate(url)` 完成，不再重建 WebView2。"清除环境会话"调用 `coreWebView.Profile.ClearBrowsingDataAsync()`。这套方案能直接干掉 [`MainWindow.WebView.cs` 整个 recreation 状态机](src/OpenClaw/MainWindow.WebView.cs)（约 180 行），减少切环境时的闪屏和 ~3-5 s 重建延迟。
- **最稳：multi-profile API**。WebView2 1.0.2592+ 的 `CoreWebView2ControllerOptions.ProfileName` 支持同进程多 profile。配 `CoreWebView2Environment.CreateAsync(null, sharedUserDataFolder, options)`，每个 environment 用不同的 profile name。
- 如果坚持每环境一个独立 user-data folder，必须接受"切环境必杀进程"——目前的 `MainWindow.RecreateWebViewAsync` 只是替换了控件，并不会让 WebView2 的子进程真正退出。

### 2. `settings.json` 写入不是原子操作

[Services/ConfigurationService.cs:90-91](src/OpenClaw/Services/ConfigurationService.cs#L90-L91)

```csharp
var json = JsonSerializer.Serialize(Settings, AppSettingsJsonContext.Default.AppSettings);
File.WriteAllText(SettingsFilePath, json);
```

`File.WriteAllText` 不是原子的——进程在写到一半时崩溃 / 断电 / 磁盘满，会留下空文件或截断的 JSON，下次启动会被解析失败的兜底逻辑覆盖为 default，**用户的所有 environments 配置丢失**。

**修复**：写入临时文件 + `File.Replace` / `File.Move(overwrite: true)`：

```csharp
var tempPath = SettingsFilePath + ".tmp";
File.WriteAllText(tempPath, json);
if (File.Exists(SettingsFilePath))
    File.Replace(tempPath, SettingsFilePath, destinationBackupFileName: null);
else
    File.Move(tempPath, SettingsFilePath);
```

### 3. `CommunityToolkit.Mvvm` 已被引入但完全没用

[OpenClaw.csproj:25](src/OpenClaw/OpenClaw.csproj#L25)、[Helpers/SimpleCommand.cs](src/OpenClaw/Helpers/SimpleCommand.cs)、所有 ViewModel

- 项目依赖 `CommunityToolkit.Mvvm 8.*`（每次构建都付出包体积代价），但代码里所有 INPC 都是手写的：[MainViewModel.Shared.cs:32-47](src/OpenClaw/ViewModels/MainViewModel.Shared.cs#L32-L47)、[ViewModels/SettingsViewModel.cs:323-326](src/OpenClaw/ViewModels/SettingsViewModel.cs#L323-L326)、[Models/EnvironmentConfig.cs:68-77](src/OpenClaw/Models/EnvironmentConfig.cs#L68-L77)、[ViewModels/HeartbeatIndicatorViewModel.cs:14-34](src/OpenClaw/ViewModels/HeartbeatIndicatorViewModel.cs#L14-L34) 全部都是。
- 所有命令都用了自定义的 `SimpleCommand` / `AsyncCommand`，[`SimpleCommand`](src/OpenClaw/Helpers/SimpleCommand.cs#L10-L23) 甚至连 `CanExecute` 都不支持（永远返回 `true`），且 `CanExecuteChanged` 用 `#pragma warning disable CS0067` 压住了警告。

**修复**：
- ViewModel 改为继承 `ObservableObject`，把 19 个 `MainViewModel.*` 文件中的 `private T _field; public T X { get => _field; set => SetProperty(ref _field, value); }` 用 `[ObservableProperty] private T _x;` 替换——在 [MainViewModel.Telemetry.Properties.cs](src/OpenClaw/ViewModels/MainViewModel.Telemetry.Properties.cs)、[MainViewModel.Core.Properties.cs](src/OpenClaw/ViewModels/MainViewModel.Core.Properties.cs) 这种纯属性的 partial 中可以一次性删掉一半代码。
- 把 `SimpleCommand` / `AsyncCommand` 全部替换为 `[RelayCommand] private void Reload() => …` / `[RelayCommand] private async Task RunDiagnosticsAsync()`，可一并删除 [MainViewModel.Commands.Properties.cs](src/OpenClaw/ViewModels/MainViewModel.Commands.Properties.cs) 与 [MainViewModel.Commands.cs](src/OpenClaw/ViewModels/MainViewModel.Commands.cs) 中冗余的 ICommand 字段。
- 删除 `SimpleCommand.cs`、`MainViewModel.Shared.cs` 里的 `OnPropertyChanged` / `SetProperty`。

预计 ViewModel 整体可减少 30%-40% 行数，并解锁 `[NotifyPropertyChangedFor]`、`[NotifyCanExecuteChangedFor]` 等编译期联动。

### 4. partial 类拆分过度，反而增加维护成本

文件大小（来自 `wc -l`）：

| 文件 | 行数 |
|---|---|
| `MainViewModel.cs` | 22（仅构造函数）|
| `MainViewModel.Collections.cs` | 14 |
| `MainViewModel.CollectionFactory.cs` | 19 |
| `MainViewModel.Commands.Properties.cs` | 14 |
| `MainViewModel.Indicators.cs` | 39 |
| `MainViewModel.Shared.cs` | 48（基础设施 mixin）|
| `ShellSessionCoordinator.Helpers.cs` | 40 |
| `ShellSessionCoordinator.Events.cs` | 45（10 个 1 行的事件转发）|
| `ShellSessionCoordinator.Telemetry.cs` | 59 |
| `ShellSessionCoordinator.State.cs` | 70 |
| `ShellSessionCoordinator.StateEffects.cs` | 72 |
| `ShellSessionCoordinator.RecoveryStateTransitions.cs` | 82 |

`MainViewModel` 共 18 个 partial、`ShellSessionCoordinator` 共 13 个 partial，但所有字段集中在 `MainViewModel.Fields.cs` / `ShellSessionCoordinator.cs`，其它 partial 共享同一份 mutable state——本质是 God class，partial 分割只是把行数分散而已，并没有改变耦合，反而增加："要找一个字段在哪用 → 全文搜索 12 个文件"的认知成本。

[ShellSessionCoordinator.Events.cs](src/OpenClaw/Services/ShellSessionCoordinator.Events.cs) 是最极端的例子：每个事件转发都是 `=> HandleX(args);`，10 个 1 行函数单独占一个文件，纯属"为了拆而拆"。

**修复方向**：
- 合并极小 partial：把 `Events.cs`、`Helpers.cs`、`Telemetry.cs`、`State.cs`、`StateEffects.cs`、`RecoveryStateTransitions.cs` 合回 `ShellSessionCoordinator.cs`。如果担心文件大，可以按"功能 → 嵌套类"拆分（`RecoveryDriver`、`HealthTracker`），让边界变成真正的"对象边界"而不是"文件边界"。
- `MainViewModel.cs` + `Collections.cs` + `CollectionFactory.cs` + `Commands.Properties.cs` + `Indicators.cs` + `RunIndicators.cs` 完全可以合并；状态字段集中爆发说明该分的不是文件，而是把 indicator 行为提到独立的 `IndicatorPresenter` / `RunIndicatorAnimator` 类。

简单经验值：partial 文件 < 50 行就基本是过度拆分。

### 5. 测试项目通过 stub 类型重新声明生产 API

[tests/OpenClaw.Tests/OpenClaw.Tests.csproj](tests/OpenClaw.Tests/OpenClaw.Tests.csproj) `<Compile Include="..\..\src\OpenClaw\Services\ShellSessionCoordinator.*.cs" Link="…" />`
[tests/OpenClaw.Tests/TestSupport.cs:25-193](tests/OpenClaw.Tests/TestSupport.cs#L25-L193)

测试无法直接引用 `OpenClaw.csproj`（因为它是 `WinExe` + `UseWinUI=true`，引入 WinUI 后测试无法以 `net10.0` 跑命令行），所以 `TestSupport.cs` 用 `namespace OpenClaw.Services { public sealed class WebViewService { … } public sealed class HostedUiBridge { … } public enum ConnectionState { … } public sealed record ControlUiProbeSnapshot(…) { … } }` **手动重新声明了**这些类型——这意味着：
- 生产代码的 `ControlUiProbeSnapshot` 修改任意字段顺序，测试侧不会编译错误（因为是另一份类型），但行为会偏离。
- 现有的 `Reset cancels in-flight reconnect`、`Background resume reconnects` 等 11 个 case 一直能跑通，但只是验证"stub 也满足契约"。
- 新增字段（如 `ControlUiProbeSnapshot.IsBusy`）需要在两个文件里同步维护。

**修复方向**：
- 把 `ShellSessionCoordinator` + `RecoveryModels` + `RecoveryPolicyOptions` 抽到 `OpenClaw.Core.csproj`（纯 `net10.0`，不依赖 WinUI），生产侧 `OpenClaw.csproj` 直接引用。测试项目也只引用 `OpenClaw.Core`。
- 把当前对 `App.Configuration.Settings.Heartbeat` / `App.Logger` 的静态依赖改成构造注入（见下面 P0-#7）。

### 6. 多个 `public async void` 形成隐藏的 swallow 异常路径

[Services/WebViewService.cs:234](src/OpenClaw/Services/WebViewService.cs#L234) `public async void Stop()`
[Services/WebViewService.cs:940](src/OpenClaw/Services/WebViewService.cs#L940) `private async void OnNavigationCompleted(...)`
[Services/ShellSessionCoordinator.Events.cs:27](src/OpenClaw/Services/ShellSessionCoordinator.Events.cs#L27) `private async void OnEventGapDetected(...)`
[Services/ShellSessionCoordinator.Events.cs:44](src/OpenClaw/Services/ShellSessionCoordinator.Events.cs#L44) `private async void OnHeartbeatFailed(...)`
[MainWindow.Lifecycle.cs:143](src/OpenClaw/MainWindow.Lifecycle.cs#L143) `private async void OnWindowVisibleAsync()`
[Views/SettingsDialog.Actions.cs:59](src/OpenClaw/Views/SettingsDialog.Actions.cs#L59) `private async void OnClearEnvironmentSessionClick(...)`

特别危险的是 `WebViewService.Stop()`：这是公共 API，调用者无法 `await`，里面的 `TryAbortActiveRunAsync()` / `InjectStopCommandAsync()` 抛异常会**直接终止进程**（unhandled in async void）。

**修复**：
- `Stop()` 改为 `Task StopAsync()`，调用方 `await ViewModel.StopAsync()`（命令换成 `[RelayCommand] async Task StopAsync()`）。
- 事件处理器 `async void` 是 WinUI 的常态，但每个都需要 `try { await … } catch (Exception ex) { App.Logger.Error(…); }` 兜底；目前 `OnEventGapDetected` 用 `=> await HandleEventGapDetectedAsync(args)` 直接转发，里面的异常没人接。

### 7. 全局静态服务定位让任何东西都不可测试 / 不可替换

[App.xaml.cs:30,35,40](src/OpenClaw/App.xaml.cs#L30-L40)
```csharp
public static ConfigurationService Configuration { get; } = new();
public static LoggingService Logger { get; } = new();
public static Window? MainWindow { get; private set; }
```

加上 [Services/AppTelemetry.cs](src/OpenClaw/Services/AppTelemetry.cs) 这个静态 `Func<int>?` 提供器，构成了 4 层全局指针：

```
App.Configuration → ConfigurationService
App.Logger        → LoggingService
App.MainWindow    → Window
AppTelemetry.DeferredSaveRequestsProvider → ConfigurationService
```

`MainViewModel.Shared.cs:14` 直接 `App.MainWindow?.DispatcherQueue.TryEnqueue(...)`，意味着 ViewModel 与 Window 类型耦合死。

**修复方向**：引入轻量 DI（`Microsoft.Extensions.DependencyInjection`），在 `App.OnLaunched` 注册 services；MainWindow / ViewModel 通过构造函数接收 `IConfigurationService` / `ILoggingService` / `IUiDispatcher`；可以删掉 `AppTelemetry`，让 `ShellSessionCoordinator` 直接注入 `ConfigurationService`。

---

## P1 — 性能 & 维护性

### 8. 日志服务路径上每次写都用反射 JSON 序列化

[Services/LoggingService.cs:71](src/OpenClaw/Services/LoggingService.cs#L71)
```csharp
EnqueueLine(JsonSerializer.Serialize(logEntry));
```

`AppSettingsJsonContext` 已经为 settings 启用了 `JsonSerializerContext` source-gen，但 `StructuredLogEntry` 不在其中，反射 + 装箱 `Context = context`（任意 object）每条日志一次反射开销。本项目日志量不大，但 `App.Logger.Info(eventKey, new { ... })` 这种调用在 [WebViewService](src/OpenClaw/Services/WebViewService.cs#L330)、[HostedUiBridge polling](src/OpenClaw/Services/HostedUiBridge.cs)、[ShellSessionCoordinator](src/OpenClaw/Services/ShellSessionCoordinator.RecoveryLifecycle.cs#L124-L131) 几乎每秒都触发。

**修复**：
- 给 `StructuredLogEntry` 加 `[JsonSerializable]` 进 `AppSettingsJsonContext`（或独立 `LoggingJsonContext`）。但因为 `Context` 是 `object?`，需要换成 `Dictionary<string, object?>` 或 `JsonElement`，或者直接用 `Utf8JsonWriter` 手写。
- 更激进：换成 [`Microsoft.Extensions.Logging`](https://learn.microsoft.com/dotnet/core/extensions/logging) + `LoggerMessage` source generator，可以零分配并消除装箱。

### 9. `ControlUiLatencyService` 用 ICMP ping 测量 Web 延迟（用户当前部署下完全失灵）

[Services/ControlUiLatencyService.cs:151-170](src/OpenClaw/Services/ControlUiLatencyService.cs#L151-L170)

3 s 一次 `Ping.SendPingAsync(host, 2000)`，但用户当前 gateway 是 `https://ai.falsemeet.site` —— 走 Cloudflare Tunnel：

- Cloudflare 边缘默认 **丢弃 ICMP**（`cloudflared` 隧道根本不转发 ICMP）。
- 即使个别 ASN 透传 ICMP，回到的 RTT 是 **客户端 → Cloudflare 最近 PoP** 的延迟，与 **PoP → 隧道 → VPS Gateway → Control UI** 的实际响应延迟差一个数量级，对用户感知毫无指示意义。
- 后果：右上角"-- ms"或"Stale"几乎是常态——结合代码中 `_lastSuccessSnapshot.IsSuccess` 永远 `false` 时 fallback 到 `Unknown`，用户看到的就是空 / `--`，**这个延迟徽章对当前部署相当于没有**。

**修复**（按收益降序）：
1. 用 HTTP HEAD 测真实 RTT（复用现有的 `HeartbeatHttpClient`，30 s 一次，本来就在打 Gateway 根路径）：
   ```csharp
   var sw = Stopwatch.StartNew();
   using var resp = await HeartbeatHttpClient.SendAsync(
       new HttpRequestMessage(HttpMethod.Head, controlUiUrl),
       HttpCompletionOption.ResponseHeadersRead, token);
   sw.Stop();
   PublishLatency(sw.ElapsedMilliseconds);
   ```
   读 `cf-ray` 响应头还能顺便提示走没走 Cloudflare（已经在 `ProbeGatewayTransportAsync` 里用过这一招）。
2. 直接复用 `HeartbeatProbeResult`：在 `WebViewService.ProbeGatewayTransportAsync` 加一个 `Stopwatch`，由心跳同时驱动延迟徽章——**直接删除 `ControlUiLatencyService` 整个独立的 3 s 定时器**。这步还能解决 `RefreshResourceScheduling` 中"延迟服务和心跳两条独立的 start/stop 路径"的重复。
3. 如果保留 ICMP 作为快速本地探测，**至少**对 `*.workers.dev` / `*.cloudflareaccess.com` / 自定义域 (Host 段) 自动降级到 HTTP 探测路径——但比起删掉，这里没有维护它的理由。

### 10. 旧 `WebView2` 实例没有显式关闭

[MainWindow.WebView.cs:124-143](src/OpenClaw/MainWindow.WebView.cs#L124-L143)

```csharp
WebViewHost.Children.Clear();
WebViewHost.Children.Add(nextWebView);
```

`WebViewHost.Children.Clear()` 把旧 `WebView2` 从可视树移除，但没有调用 `oldWebView.Close()` / `oldCoreWebView2.Close()`，下层 Browser process 不会立即回收。每次环境切换都会累积一份后台进程，直到 GC + COM 释放才解除。在频繁切换环境时容易看到任务管理器里 `msedgewebview2.exe` 数量上涨。

**修复**：
```csharp
foreach (var child in WebViewHost.Children.OfType<WebView2>().ToArray())
{
    child.Close(); // 显式关闭
}
WebViewHost.Children.Clear();
```

`WebViewService.DetachCurrentWebView()` 也只是解除事件订阅与字段置空，没有调用 `_webView.Close()`。

### 11. csproj 使用 floating 版本号，CI 不可重现

[src/OpenClaw/OpenClaw.csproj:23-25](src/OpenClaw/OpenClaw.csproj#L23-L25)
```xml
<PackageReference Include="Microsoft.WindowsAppSDK" Version="1.8.*" />
<PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.*" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
```

`8.*` / `1.8.*` / `10.0.*` 是 floating，意味着今天构建用 `8.4.0`、明天可能解析到 `8.5.0`，行为可能改变。CI 没有 `packages.lock.json` 锁定。

**修复**（建议参考下面这一组当前稳定版本，2026-04-30 Microsoft NuGet 上可解析到的最新非 preview）：

```xml
<PackageReference Include="Microsoft.WindowsAppSDK" Version="1.8.250515001" />
<PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.26100.59" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
```

> 上面的 patch 版本号请在升级时用 `dotnet list package` 校准——重点是 **不要让小版本号落到 `*`**。

同时在 csproj 中加 `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` 并把 `packages.lock.json` 入版本控制，CI 用 `dotnet restore --locked-mode`。

### 12. `Directory.Build.props` 中的兼容开关已无意义

[Directory.Build.props:6](Directory.Build.props#L6)
```xml
<RestorePackagesConfig>true</RestorePackagesConfig>
```

注释说"如果未来加入 packages.config 项目"——但 SDK-style 项目不存在 packages.config，此开关在没有 .NET Framework 项目时纯属噪声。可以删掉。

### 13. `LogViewerDialog` 全文加载 + Split 不可扩展（已经开始累积）

[Views/LogViewerDialog.xaml.cs:32-43](src/OpenClaw/Views/LogViewerDialog.xaml.cs#L32-L43)
```csharp
var content = File.ReadAllText(logFile);
var lines = content.Split('\n');
```

**当前实测**：`%LOCALAPPDATA%\OpenClaw\logs\` 已经累积 29 个文件（最早是 2026-04-01，今日文件 ~109 KB）。每天约 100 KB × 365 天 = 35 MB / 年；如果开 `enableVerboseRecoveryLogging: true`（用户当前是 `false`，但 Settings → DevTools 里就是一键开关）日志量会暴涨到每天 5-10 MB 量级。

`File.ReadAllText` 把整个文件复制成字符串，再 `Split` 复制一份字符串数组，再 `Skip(lines.Length - 500)` 复制最后 500 行——一次操作三份内存副本。日志没有 retention policy。

**修复**（一次到位，工作量都不大）：
1. **流式读取最后 500 行**：

   ```csharp
   var tail = new Queue<string>(500);
   foreach (var line in File.ReadLines(logFile))
   {
       if (tail.Count == 500) tail.Dequeue();
       tail.Enqueue(line);
   }
   LogContent.Text = string.Join('\n', tail);
   ```
2. **`LoggingService` 加日志清理**（写入路径里顺手做）：每次 `WriteBatch` 后 / 或在 `LoggingService` 构造时跑一次：
   ```csharp
   var cutoff = DateTime.UtcNow.AddDays(-14);
   foreach (var f in Directory.EnumerateFiles(LogFolder, "openclaw-*.log"))
   {
       if (File.GetLastWriteTimeUtc(f) < cutoff) File.Delete(f);
   }
   ```
   保留 14 天对当前 ~100 KB/天 的体量足够，开 verbose 时也能控制在 < 100 MB。
3. 再激进一点：单文件超过 5 MB 就滚到 `openclaw-2026-04-30.1.log`、`.2.log`，避免 `LogViewerDialog` 即使是同一天也读到大文件。

### 14. `WindowFrameHelper.RefreshNonClientFrame` 触发同步 Win32 重绘

[Helpers/WindowFrameHelper.cs:281-301](src/OpenClaw/Helpers/WindowFrameHelper.cs#L281-L301)

每次主题切换 / 窗口激活会触发：
1. `SetWindowPos(... NOMOVE | NOSIZE | FRAMECHANGED)`
2. `RedrawWindow(... INVALIDATE | UPDATENOW | FRAME)` ← 同步重绘
3. `DwmFlush()` ← 阻塞到下一次 vsync

[MainWindow.Theme.cs:53-65](src/OpenClaw/MainWindow.Theme.cs#L53-L65) `ApplyTheme` 又把这个流程通过 `dispatcherQueue.TryEnqueue` 排了**最多三轮**（`useSizeNudgeOnDarkTransition` + `includeTrailingRefresh`），暗黑切换过去时还会 `AppWindow.Resize(width, height + 1)` 再 resize 回来（[Helpers/WindowFrameHelper.cs:159-176](src/OpenClaw/Helpers/WindowFrameHelper.cs#L159-L176)）。

这是 WindowsAppSDK 1.x 已知的 title-bar 同步 bug 的 workaround，[DEVELOPMENT_NOTES.md](DEVELOPMENT_NOTES.md) 应该有上下文。但每次主题点击都跑一次 size nudge + dispatcher 三跳是可以观察到的肉眼闪烁。

**修复方向**：
- 升级 WindowsAppSDK 到最新 1.8.x 后跑一次去掉 `useSizeNudgeOnDarkTransition` / `includeTrailingRefresh` / `redrawWindow` 的 try：很多 SDK 1.7+ 版本已经修复了相关 bug。
- 把多次 dispatcher 排队改为一次，把 `redrawWindow=true` 改为 `false`（仅 invalidate，不强制 update）。
- 不要在每次 `OnWindowActivated` 都跑这个流程——`_hasPerformedInitialTitleBarRefresh` 字段已经做了首次保护，但 `OnRootActualThemeChanged` 仍是每次都跑。

### 15. 主题按钮每次点击都创建新 `SolidColorBrush`

[MainWindow.Theme.cs:41-51](src/OpenClaw/MainWindow.Theme.cs#L41-L51)
```csharp
button.Background = isSelected
    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 230, 240, 255))
    : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
button.Foreground = isSelected
    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 37, 99, 235))
    : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
```

颜色是硬编码的，没有跟系统主题色 / accent color 联动，且每次点击会创建 4 个新 Brush。[MainViewModel.Fields.cs:20-23](src/OpenClaw/ViewModels/MainViewModel.Fields.cs#L20-L23) 已经把状态色作为静态 brush，主题按钮也应该这么做。

**修复**：
- 把这两个 brush 提到 `App.xaml` 的 `<ResourceDictionary>` 里，按 `Light` / `Dark` 主题区分（应用 `ThemeResource`）。
- 用 `ThemeResource` `AccentFillColorDefaultBrush` / `SystemAccentColor` 替代硬编码 RGB。

### 16. heartbeat / run indicator 每帧触发 12-24 次 PropertyChanged

[ViewModels/MainViewModel.Heartbeat.cs:72-80](src/OpenClaw/ViewModels/MainViewModel.Heartbeat.cs#L72-L80)
```csharp
for (var index = 0; index < HeartbeatIndicators.Count - 1; index++)
{
    HeartbeatIndicators[index].FillBrush = HeartbeatIndicators[index + 1].FillBrush;
    HeartbeatIndicators[index].FillOpacity = HeartbeatIndicators[index + 1].FillOpacity;
}
```

[`HeartbeatIndicatorViewModel.FillBrush` 的 setter](src/OpenClaw/ViewModels/HeartbeatIndicatorViewModel.cs#L16-L24) 没有 equality check，每次赋值无条件触发 PropertyChanged → x:Bind → 触发 Ellipse Fill 重新绑定。`ApplyRunIndicators` 同样问题（每 430ms 一次）。

**修复**：
```csharp
public Brush FillBrush
{
    get => _fillBrush;
    set
    {
        if (!ReferenceEquals(_fillBrush, value))
        {
            _fillBrush = value;
            PropertyChanged?.Invoke(this, EventArgsCache.FillBrush);
        }
    }
}
private static class EventArgsCache
{
    public static readonly PropertyChangedEventArgs FillBrush = new(nameof(FillBrush));
    public static readonly PropertyChangedEventArgs FillOpacity = new(nameof(FillOpacity));
}
```

或者直接换成 `[ObservableProperty]`（toolkit 自带 equality 检查 + 缓存 EventArgs）。

### 17. 静态 Brush 在静态字段初始化器中创建

[ViewModels/MainViewModel.Fields.cs:20-23](src/OpenClaw/ViewModels/MainViewModel.Fields.cs#L20-L23)

```csharp
private static readonly Brush NeutralBrush = CreateBrush(107, 114, 128);
```

`SolidColorBrush` 是 `DependencyObject` 子类，必须在 UI 线程上创建。第一次访问 `MainViewModel` 类型（也就是 `MainWindow.ViewModel { get; } = new();` 的字段初始化器，发生在 `MainWindow` 构造时，UI 线程上）会触发静态初始化器，目前应当是安全的。但任何"先在后台线程意外触发 `MainViewModel` 类初始化"的代码改动都会让这些 brush 在错误线程被创建并报 `RPC_E_WRONG_THREAD`。

**修复**：把这些 brush 移到 `App.xaml` 的 `ResourceDictionary` 里（顺便支持主题适配），ViewModel 通过 `Application.Current.Resources["StatusSuccessBrush"]` 访问。

### 18. `LogLifecycleEventOnce` 的去重 key 用 JSON 序列化

[Services/WebViewService.cs:1078-1091](src/OpenClaw/Services/WebViewService.cs#L1078-L1091)
```csharp
var logKey = context is null
    ? eventName
    : $"{eventName}:{JsonSerializer.Serialize(context)}";
```

每次 navigation start/complete 调用都把 `context` 反射序列化一次，只为做"上次和这次是否一样"的对比。同样的调用模式在 [`ConfigurationService.SaveDeferred`](src/OpenClaw/Services/ConfigurationService.cs#L106-L118) 与 [`InspectControlUiStateAsync` 的 instrumentation 日志](src/OpenClaw/Services/WebViewService.cs#L327-L350) 中也存在。

**修复**：
- 大多数情况下 `eventName` 单独就足够去重（`"navigation.start"`、`"navigation.completed"`）。
- 如果非要带 url 一起 dedup，`context.ToString()` 或 `(context as { Uri: var uri })?.uri` 直接取字段。

### 19. `NormalizeSettings` 解析 raw JSON 两次

[Services/ConfigurationService.cs:179-220](src/OpenClaw/Services/ConfigurationService.cs#L179-L220)

`Load()` 先 `JsonSerializer.Deserialize` 一次解析整个 JSON，紧接着 `NormalizeSettings` 又 `JsonDocument.Parse(rawJson)` 第二次仅为了检测 `heartbeatIntervalSeconds` 这个 legacy 字段是否存在。两次解析。

**修复**：在 `AppSettings` 上加 `[JsonInclude] public int? LegacyHeartbeatIntervalSeconds { get; set; }`（绑定到旧字段名 `heartbeatIntervalSeconds`），然后 normalize 只看反序列化后的对象图。或者用自定义 `JsonConverter` 在反序列化时直接迁移。

### 20. 测试覆盖盲点

[tests/OpenClaw.Tests/Program.cs](tests/OpenClaw.Tests/Program.cs) 11 个 case 覆盖 `ShellSessionCoordinator` 的核心恢复路径，但完全没覆盖：

- `ConfigurationService.NormalizeSettings`：legacy `heartbeatIntervalSeconds` 迁移、JSON 损坏后的兜底是关键路径，但没有测试。
- `WebViewService.BuildEnvironmentFolderName`：sanitize + truncate + SHA256 后缀的纯函数，最容易写 unit test 的，但没测。
- `SettingsViewModel.SaveAll` 的 validation pipeline：`SettingsValidationDuplicateEnvironment`、URL scheme 检查（`ws://`、相对 URL）、空名 guard 都没单测。

这些是用户最容易踩坑的场景。建议优先补这三块的 unit test。

---

### 31. 心跳节奏 vs Cloudflare Tunnel 的 100 s 空闲断链

[Models/RecoveryPolicyOptions.cs:82-108](src/OpenClaw/Models/RecoveryPolicyOptions.cs#L82-L108) + 用户实际 settings.json

用户已经把 `heartbeat.intervalSeconds` 调到 30 s（默认 45 s），`failureThreshold = 3`，`connectingThreshold = 3` —— 说明已经踩过 CF Tunnel 链路抖动的坑。但代码里 [`HeartbeatReloadCooldown = TimeSpan.FromSeconds(75)`](src/OpenClaw/Services/WebViewService.cs#L51) 是**硬编码**的，**不读 `recoveryPolicy.hardRefreshCooldownSeconds`**，所以用户在 settings 里把 `hardRefreshCooldownSeconds` 改成 `45` 实际上**只对 `ShellSessionCoordinator` 那条恢复路径生效**——`WebViewService.TryScheduleHeartbeatReload` 这条路径仍然在用 75 s 硬编码冷却，导致两条恢复路径不一致。

**修复**：
- 把 `HeartbeatReloadCooldown` 从 `static readonly` 字段改成读取 `App.Configuration.Settings.RecoveryPolicy.HardRefreshCooldownSeconds`（或在 `StartHeartbeat` 时一次性传进来作为字段保存）。
- 同时把 [`HeartbeatHttpClient.Timeout = 10s`](src/OpenClaw/Services/WebViewService.cs#L52) 评估一下：CF Tunnel 冷启动 + 上游 Gateway 还没 ready 时，10 s 偶尔会过短；如果观察到日志里 `Gateway heartbeat request failed: TaskCanceledException` 偏多，可以提到 15 s 或做成可配置。

**校准建议（针对当前 ai.falsemeet.site 的 CF Tunnel 部署）**：

| 参数 | 用户值 | 建议值 | 理由 |
|---|---|---|---|
| `heartbeat.intervalSeconds` | 30 | 保持 30 | CF idle 默认 100 s，30 s × failureThreshold(3) = 90 s 正好顶住 |
| `heartbeat.failureThreshold` | 3 | 保持 3 | 同上 |
| `recoveryPolicy.hardRefreshCooldownSeconds` | 45 | **同步代码硬编码到 ≥ 45** | 同 #31 |
| `recoveryPolicy.transportIdleSuspicionSeconds` | 60（默认） | 保持 60 | 略低于 100 s 的 CF idle |
| `recoveryPolicy.eventIdleSuspicionSeconds` | 120（默认） | 90 | 与心跳节奏对齐，CF Tunnel 下事件链路一旦超过 90 s 没动静多半已经死了 |

### 32. JS Bridge 探测被 OpenClaw 上游 DOM 结构耦合

[Services/HostedUiBridge.cs:267-329](src/OpenClaw/Services/HostedUiBridge.cs#L267-L329)

bridge 通过模糊匹配 `text` 关键字识别 OpenClaw Control UI 各种状态：

```js
const tokenMissingMatch = matchAny(text, [
  'auth_token_missing', 'token missing', 'missing shared token'
]);
const trustedProxyLoopbackMatch = matchAny(text, [
  'trusted_proxy_loopback_source', 'loopback-source trusted-proxy', ...
]);
```

这些 token 直接来自 OpenClaw Gateway 服务端的错误码 / 字符串。问题：
- 上游 OpenClaw（`openclaw/openclaw`）在每个版本都可能改 i18n / 错误措辞——v3.0.4 兼容现在的 `ai.falsemeet.site` 部署，但用户每次升级 OpenClaw Gateway 都有静默断兼容的风险。
- bridge 用 `Array.from(document.querySelectorAll(...))` + 关键字命中——OpenClaw Control UI 是 Vite + Lit SPA，Lit 的 Shadow DOM 可能让 `document.querySelectorAll` 看不到内部组件——只有那些泄漏到 Light DOM 的状态文本才能被命中。**这意味着如果 OpenClaw 团队哪天把状态信息收进 Shadow DOM，整套 native 状态识别就静默失效**。

**修复方向**（按收益降序）：
1. **优先用 OpenClaw 已暴露的 JS API**。bridge 已经探测了 `window.__openclaw?.chat?.abort` 之类入口（[HostedUiBridge.cs:533-546](src/OpenClaw/Services/HostedUiBridge.cs#L533-L546)），同样应当优先调用 `window.__openclaw?.getStatus()` 或 `window.__openclaw?.errors`（**先去 OpenClaw 上游确认这些 API 是否存在**——如果不存在，应当向 OpenClaw 提 issue 让上游暴露稳定 API，比 native 侧抓 DOM 字符串可持续得多）。
2. **订阅 `/__openclaw/control-ui-config.json`**：上游文档明确这个端点返回 Control UI 的运行时配置且与 gateway 同源（要走 auth）。可以替代 `bridge` 中"识别是否处于 auth-required 状态"的关键字匹配——直接 fetch 端点，401 就是 auth issue，404 就是路径错。
3. **退而求其次**：把所有错误关键字（[Services/HostedUiBridge.cs:267-329](src/OpenClaw/Services/HostedUiBridge.cs#L267-L329)）抽到 `bridge-keywords.json` 资源文件，version 化（如 `keywords.openclaw-v3.json`），与 OpenClaw 上游版本绑定。当用户 Gateway 升级时只需更新 keyword 文件而不用改代码。
4. 写一个针对 `ai.falsemeet.site` 实际 DOM 的端到端测试 fixture（保存几个真实 HTML 快照），验证关键字命中——避免上游升级时静默失效。

### 33. 心跳 HTTP 探测路径未与 OpenClaw 的 Control UI basePath 协调

[Services/WebViewService.cs:792-828](src/OpenClaw/Services/WebViewService.cs#L792-L828) `ProbeGatewayTransportAsync`

心跳是直接向 `gatewayUrl` 根路径打 GET，从 `cf-ray` 头判断"经过了 Cloudflare"。但上游 OpenClaw 支持 `gateway.controlUi.basePath`（如 `/openclaw`）——**用户当前是根路径所以 OK**，但如果团队部署多套实例共用一个域用 path 区分（比如 `https://ai.falsemeet.site/staging`），这个 GET `/` 会探到错的服务（VPS 上别的 web 服务、Cloudflare 错误页等），返回 200 还会被误判成 healthy。

**修复**：
- 优先：心跳应该探 OpenClaw 的健康端点。从 `/__openclaw/control-ui-config.json` 拿 config（HEAD 即可），它一定是 Gateway 才会响应的端点，且需要 auth（401 / 200 都说明 Gateway 在线）。
- 兜底：把 `gatewayUrl` 而不是其根域作为探测目标——目前代码就是用 `gatewayUrl` 本身（这里没问题）；但应该明确**禁止把 `gatewayUrl` 截到根域**，并在 settings dialog 的 URL 校验里加一句 hint："如果 Gateway 在 basePath 下，URL 末尾要带 path"。

---

## P2 — 低优先级 / 锦上添花

### 21. `AppMetadata.CurrentVersion` 与 csproj `<Version>` 重复

[Helpers/AppMetadata.cs:12](src/OpenClaw/Helpers/AppMetadata.cs#L12) `CurrentVersion = "3.0.4"`
[OpenClaw.csproj:17-19](src/OpenClaw/OpenClaw.csproj#L17-L19) `<Version>3.0.4</Version>`

下次发版时容易漏改一处。`GetDisplayVersion()` 已经能从 Assembly metadata 读出来，把 `CurrentVersion` 常量删掉，让所有用到该常量的地方调用 `GetDisplayVersion()`。

### 22. `DwmSetWindowAttribute` 的返回值未检查

[Helpers/WindowFrameHelper.cs:196-199](src/OpenClaw/Helpers/WindowFrameHelper.cs#L196-L199)

`DwmWindowAttributeBorderColor`(34) / `CaptionColor`(35) / `TextColor`(36) 是 Windows 11 22H2+ 才支持的属性。在更早的 Windows 10 上，这几个 P/Invoke 会返回 `E_INVALIDARG`，调用本身无害但应当有 fallback 路径或日志，避免不同 Windows 版本下出现"标题栏颜色一致"的预期被默默打破。README 标注最低支持 Windows 10 1809，与该假设不一致。

### 23. `ConfigurationService` 的所有 setter 都触发 `Save`

环境列表的 toolbar 调用 `App.Configuration.Settings.SelectedEnvironmentName = …; SaveDeferred();`（[MainViewModel.Environment.cs:43-44](src/OpenClaw/ViewModels/MainViewModel.Environment.cs#L43-L44)），主题切换调用 `Settings.AppTheme = …; SaveDeferred();`（[MainWindow.Theme.cs:20-21](src/OpenClaw/MainWindow.Theme.cs#L20-L21)），窗口尺寸用立即 `Save()`（[MainWindow.Lifecycle.cs:49](src/OpenClaw/MainWindow.Lifecycle.cs#L49)）。`AppSettings` 是 mutable plain class，没有任何"脏"标记或属性变更通知，所有保存逻辑全靠调用方手动调 `Save()`。

如果想避免漏调，可以：
- 让 `AppSettings` 实现 `INotifyPropertyChanged`（已经有 `EnvironmentConfig` 这么做了），`ConfigurationService` 订阅，自动触发 `SaveDeferred`。
- 否则就要确保 settings 写入路径完全集中——目前已经接近这个状态，把 `MainWindow.SaveWindowBounds`（同步 `Save`）改成 `SaveDeferred` 即可省掉 close 时的同步阻塞，最后 `OnWindowClosed` 的 `App.Configuration.FlushDeferredSave()` 已经在做对应的 flush。

### 24. `SettingsViewModel` 的"未提交编辑"逻辑可能丢更改

[ViewModels/SettingsViewModel.cs:152-189](src/OpenClaw/ViewModels/SettingsViewModel.cs#L152-L189)

`TryApplyEdit` 只在用户显式点击 "Apply" 时把 `EditName` / `EditUrl` 写回 `_selectedEnvironment`。如果用户改了 URL → 点击别的环境 → `SelectedEnvironment` 变化触发 `LoadEditFields()` 把表单重置，**之前编辑没保存的内容直接丢失，且没有任何提示**。

`SettingsDialog.Actions.cs:105-117` 在 `TrySaveSettings` 时调一次 `TryApplyEdit`，但同样的保护没在 list selection change 时做。

**修复**：在 `SelectedEnvironment` setter 里检测 dirty + 弹确认；或者把 `EditName` / `EditUrl` 直接 two-way 绑定到 `SelectedEnvironment.Name` / `GatewayUrl`（环境对象本身就是 INPC）跳过中间编辑层。

### 25. `Bridge` JS 脚本里的 polling 节奏不会跟随 host 状态

[Services/HostedUiBridge.cs:636-674](src/OpenClaw/Services/HostedUiBridge.cs#L636-L674)

`pollInterval = snapshot.phase === 'connected' ? 15000 : 4000` 是硬编码。后台时切到 12-15s，可见时 1.2s。但当 native 侧已经知道连接就绪，不需要 JS bridge 高频轮询时，没有办法关掉/调慢它。可以扩展 `onCommand` 增加一个 `set_poll_interval` 命令让 native 在 idle 时降速。

### 26. `TotalReconnectAttempts` / `TotalSoftResyncAttempts` 等遥测计数器永不重置

[ShellSessionCoordinator.cs:39-42](src/OpenClaw/Services/ShellSessionCoordinator.cs#L39-L42) 中的 `_total*` 计数只在 `Reset()` 之外永远累加。`Reset()` 不重置 `_total*` 字段。如果用户切换环境很多次，"累计 reconnect 次数"会无限上升，告警阈值会失真。Diagnostics 面板展示的也是这些数字。

**修复**：`Reset()` 把 `_total*` 一并清零；或拆分"当前环境窗口" vs "session 累计"两套计数。

### 27. `AppTelemetry` 静态 provider 是 hack

[Services/AppTelemetry.cs](src/OpenClaw/Services/AppTelemetry.cs) 整个文件就是给 `ShellSessionCoordinator.RefreshInstrumentationCounters` 提供 `_deferredSaveRequests` 计数的桥梁。直接把 `ConfigurationService` 注入进 coordinator（在 `AttachAsync` 时传入）就能去掉这一整个全局缓冲层。

### 28. `WebViewService.Stop` 的 JS 注入是同步阻塞 ExecuteScript

[Services/WebViewService.cs:387-503](src/OpenClaw/Services/WebViewService.cs#L387-L503) 的 `InjectStopCommandAsync` 注入了一段 ~100 行 JS。脚本定义了 `setNativeValue`、模拟 `Enter` keypress 等 hack——大部分网页都不需要这种暴力注入。如果远端 Control UI 已经暴露 `window.__openclaw.chat.abort()`（[`TryAbortActiveRunAsync` 第一段](src/OpenClaw/Services/WebViewService.cs#L508-L570) 就在试这个），就完全不需要再尝试 `/stop` 文本注入。

**建议**：让 hosted Control UI 必须实现 `window.__openClawHostBridge.stop()`（已经有 bridge），native 侧简单调 `bridge.SendCommandAsync("stop")`，删掉 fallback 注入逻辑（约 100 行 JS + 错误处理），降低跨版本破坏面。

### 29. `OpenClaw.sln` 没有静态分析 / Code Quality 配置

`Directory.Build.props` 只配了 `RestorePackagesConfig`，没有 `<TreatWarningsAsErrors>`、`<EnableNETAnalyzers>`（默认开但建议显式开）、`<AnalysisLevel>latest-recommended</AnalysisLevel>`。建议加上以提早暴露 nullable 警告 / async-void / sync-over-async 之类问题。

### 30. `ApplicationDataPath` 路径硬编码 "OpenClaw"

[ConfigurationService.cs:14-15](src/OpenClaw/Services/ConfigurationService.cs#L14-L15)、[LoggingService.cs:15-16](src/OpenClaw/Services/LoggingService.cs#L15-L16)、[WebViewService.cs:1344-1349](src/OpenClaw/Services/WebViewService.cs#L1344-L1349) 都各自 `Path.Combine(LocalApplicationData, "OpenClaw", ...)`。三处独立硬编码，改名时容易漏。提到 `AppMetadata.LocalDataRoot` 一处定义即可。

---

## 建议落地顺序

针对当前 **单环境 / Cloudflare Tunnel / VPS Gateway** 的实际部署，按收益密度排序：

1. **第一批（CF Tunnel 直接相关，立刻可见的收益）**：
   - **#9** 删 ICMP ping，HTTP HEAD 测延迟 —— CF Tunnel 下右上角延迟徽章本来就空着
   - **#31** 让 `HeartbeatReloadCooldown` 读 settings.json 的 `hardRefreshCooldownSeconds` —— 修复用户已经调过 settings 但没生效的部分
   - **#13** 日志流式读 + 14 天 retention —— 已经 29 天了
   - **#33** 心跳探测改用 `/__openclaw/control-ui-config.json` —— 防止用户未来加 basePath 误判
2. **第二批（不改架构、收益大）**：#2（atomic save）、#10（webview close）、#11（pin versions）、#16/#17（brush 缓存）、#21（version single source）。纯局部修改，无破坏性。
3. **第三批（中等破坏）**：#3（迁移到 CommunityToolkit.Mvvm）、#4（合并过度的 partial）、#6（async void → Task）、#15（theme brush 移到资源字典）、#28（删除 JS 注入 fallback）、**#32**（解耦 OpenClaw 上游 DOM 字符串）。ViewModel 改造和 bridge 重构，建议在独立 PR / 分支。
4. **第四批（架构性重构）**：
   - **#1** WebView2 隔离 —— **既然用户当前只有一个 environment**，最便宜的修复是 **直接退化成单 profile 模式**，把 `MainWindow.WebView.cs` 的 ~180 行 recreation 状态机一并删掉（需求侧也支持这个简化：Cloudflare Tunnel + 远程 Gateway 的部署里，一个客户端通常只对应一台 VPS）。如果真要保多环境则上 multi-profile API。
   - **#5**（拆 Core 项目让测试用真类型）、**#7**（DI 替换静态单例）、**#27**（删 AppTelemetry）。这些放最后，且每一项独立 PR，每个都需要重新跑 11 个现有 test + 新加的覆盖（#20）。

---

review 范围：v3.0.4（2026-04-29），src/OpenClaw 全部源码、tests/OpenClaw.Tests 测试、build 配置文件、`%LOCALAPPDATA%\OpenClaw\settings.json` 实际部署。未审查 Strings/.resw 与 Assets/。

参考资料：
- [openclaw/openclaw#26765](https://github.com/openclaw/openclaw/issues/26765) — trycloudflare.com 的 Origin 校验问题（用户当前自定义域不受影响）
- [simple10/openclaw-stack — CLOUDFLARE-TUNNEL.md](https://github.com/simple10/openclaw-stack/blob/main/docs/CLOUDFLARE-TUNNEL.md) — CF Tunnel 路由规则、catch-all 顺序
- [openclaw/openclaw — docs/gateway/remote.md](https://github.com/openclaw/openclaw) — remote gateway 推荐做法
- [docs.openclaw.ai — Control UI](https://docs.openclaw.ai/web/control-ui) — `controlUi.basePath` / `allowedOrigins` / `__openclaw/control-ui-config.json`
