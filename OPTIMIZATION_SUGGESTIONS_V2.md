# OpenClaw Manager 优化建议 V2

**版本**：v3.0.5（在 V1 评审 v3.0.4 之后由 Codex 改了 16 个文件）
**部署画像**：与 V1 一致——单环境 `https://ai.falsemeet.site`（Cloudflare Tunnel → VPS Gateway），79 MB WebView2 profile，已经手动调过 heartbeat / 重连参数。
**本文目标**：

1. 校验 V1 各项的落地情况（哪些做了、哪些没做、哪些做了一半）
2. 指出 Codex 改动中新引入的小问题
3. 给出更新后的剩余落地清单

---

## 1. V1 项目落地情况一览

> 共 33 条建议。**P0 / P1 / P2 / 31-33 分类沿用 V1 编号**。Codex 这一轮主要打掉了 V1 的 P0-#2、#6 + P1 的 #9、#10、#13、#16、#21、#26、#31，以及加了 4 个新单测——一次比较密集的"局部性 + 高确定性"修复批。

| # | V1 题目 | 状态 | 证据 |
|---|---|---|---|
| **1** | WebView2 多环境隔离失效 | ⏳ 未处理 | [Services/WebViewService.cs:127-132](src/OpenClaw/Services/WebViewService.cs#L127-L132) 仍然 `Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", ...)`。`MainWindow.WebView.cs` 的 recreation 状态机也仍在。 |
| **2** | settings.json 非原子写 | ✅ 完成 | 新建 [Helpers/AtomicFileWriter.cs](src/OpenClaw/Helpers/AtomicFileWriter.cs)（temp + `File.Replace` + `Guid` 命名，错误路径会清理 tmp 与 backup），[ConfigurationService.cs:92](src/OpenClaw/Services/ConfigurationService.cs#L92) 已改用。**注意一点小瑕疵**——见 §2-A。 |
| **3** | CommunityToolkit.Mvvm 引而不用 | ⏳ 未处理 | `SimpleCommand` / `AsyncCommand` 仍在；`SetProperty` 手写仍在。 |
| **4** | partial 类拆分过度 | ⏳ 未处理 | `MainViewModel` / `ShellSessionCoordinator` 文件结构未变。 |
| **5** | 测试用 stub 重声明生产类型 | 🟡 部分 | `OpenClaw.Tests.csproj` 现在 `<Compile Include="…/Helpers/AtomicFileWriter.cs">` + `LogFileUtilities.cs` 直接 link 了真类型 → 这两个新 helper 走的是真代码。但 `TestSupport.cs` 中的 `WebViewService` / `HostedUiBridge` / `ControlUiProbeSnapshot` stub **还在**——根因（共享 OpenClaw.Core 项目）没动。 |
| **6** | public async void | ✅ 完成 | [WebViewService.cs:234](src/OpenClaw/Services/WebViewService.cs#L234) `public async Task StopAsync()`；[MainViewModel.Commands.cs:14](src/OpenClaw/ViewModels/MainViewModel.Commands.cs#L14) 已改用 `AsyncCommand(OnStopAsync, OnAsyncCommandFailed)`。**剩余 async void 见 §3-B**。 |
| **7** | 全局静态服务定位 | ⏳ 未处理 | `App.Configuration` / `App.Logger` / `App.MainWindow` / `AppTelemetry` 全在。 |
| **8** | 日志反射 JSON 序列化 | ⏳ 未处理 | [LoggingService.cs:74](src/OpenClaw/Services/LoggingService.cs#L74) 仍然 `JsonSerializer.Serialize(logEntry)` 反射路径。 |
| **9** | ICMP ping 测延迟 | ✅ 完成 | [ControlUiLatencyService.cs](src/OpenClaw/Services/ControlUiLatencyService.cs) 整体重写：HTTP HEAD + `Stopwatch` + `cf-ray` 提示，5 s 超时。**右上角延迟徽章在 CF Tunnel 下终于能工作了**。 |
| **10** | 旧 WebView2 没显式 Close | ✅ 完成 | [MainWindow.WebView.cs:132-135](src/OpenClaw/MainWindow.WebView.cs#L132-L135) 加了 `child.Close()`。 |
| **11** | csproj floating 版本 | ⏳ 未处理 | csproj 仍 `1.8.*` / `10.0.*` / `8.*`（仅版本号 3.0.4→3.0.5）。 |
| **12** | Directory.Build.props 兼容开关 | ⏳ 未处理 | `RestorePackagesConfig=true` 仍在。 |
| **13** | 日志全文加载 + 无 retention | ✅ 完成 | 新建 [Helpers/LogFileUtilities.cs](src/OpenClaw/Helpers/LogFileUtilities.cs)（流式 tail + `DeleteExpiredLogs`），[LogViewerDialog.xaml.cs:32-37](src/OpenClaw/Views/LogViewerDialog.xaml.cs#L32-L37) 用了，[LoggingService.cs:30](src/OpenClaw/Services/LoggingService.cs#L30) 启动时跑 14 天 retention。 |
| **14** | WindowFrameHelper 重绘 | ⏳ 未处理 | MainWindow.Theme.cs 与 helper 都没动。 |
| **15** | 主题按钮新建 Brush | ⏳ 未处理 | MainWindow.Theme.cs 未动。 |
| **16** | 指示灯 PropertyChanged 风暴 | ✅ 完成 | [HeartbeatIndicatorViewModel.cs:21,36-39](src/OpenClaw/ViewModels/HeartbeatIndicatorViewModel.cs#L21) 加了 `ReferenceEquals` / `Math.Abs < epsilon` 检查 + 缓存的 `EventArgsCache`。 |
| **17** | 静态 Brush 字段初始化 | ⏳ 未处理 | MainViewModel.Fields.cs 未动。 |
| **18** | LogLifecycleEventOnce 序列化 dedup | ⏳ 未处理 | WebViewService 的 `LogLifecycleEventOnce` 路径未动。 |
| **19** | NormalizeSettings 解析两次 | ⏳ 未处理 | ConfigurationService 未动。 |
| **20** | 测试覆盖盲点 | 🟡 部分 | 加了 4 个测试：`ResetClearsRecoveryAttemptTotalsAsync`、`AtomicWriterReplacesExistingContent`、`LogTailReaderReturnsFinalLines`、`LogRetentionRemovesOnlyExpiredOpenClawLogs`——**都是新 helper 的覆盖**。但 V1 列出的关键空白（`NormalizeSettings` 的 legacy 迁移、`BuildEnvironmentFolderName` sanitize、`SettingsViewModel.SaveAll` validation）**仍未覆盖**。 |
| **21** | AppMetadata.CurrentVersion 重复 | ✅ 完成 | [Helpers/AppMetadata.cs:11-19](src/OpenClaw/Helpers/AppMetadata.cs#L11-L19) 删除了常量，只剩 `GetDisplayVersion()`。 |
| **22** | DwmSetWindowAttribute 返回值 | ⏳ 未处理 | WindowFrameHelper.cs 未动。 |
| **23** | Settings setter 都触发 Save | ⏳ 未处理 | 未动。 |
| **24** | SettingsViewModel 未提交编辑 | ⏳ 未处理 | 未动。 |
| **25** | Bridge JS polling 节奏 | ⏳ 未处理 | HostedUiBridge.cs 未动。 |
| **26** | Total* 计数器永不重置 | ✅ 完成 | [ShellSessionCoordinator.Host.cs:93-95](src/OpenClaw/Services/ShellSessionCoordinator.Host.cs#L93-L95) `Reset()` 现在清零 `_totalReconnectAttempts` 等。新单测 `ResetClearsRecoveryAttemptTotalsAsync` 覆盖。 |
| **27** | AppTelemetry 静态 hack | ⏳ 未处理 | AppTelemetry.cs 仍是静态 provider。 |
| **28** | Stop JS 注入 fallback | ⏳ 未处理 | InjectStopCommandAsync 仍在。 |
| **29** | sln/Directory.Build 静态分析 | ⏳ 未处理 | 未动。 |
| **30** | 路径硬编码 "OpenClaw" | ⏳ 未处理 | 三处仍各自 `Path.Combine(..., "OpenClaw", ...)`。 |
| **31** | HeartbeatReloadCooldown 硬编码 75 s | ✅ 完成 | [WebViewService.cs:881-886](src/OpenClaw/Services/WebViewService.cs#L881-L886) 新增 `GetHeartbeatReloadCooldown()` 读 `Settings.RecoveryPolicy.HardRefreshCooldownSeconds`，每次调用都查（用户改 settings 立刻生效）。**终于和 ShellSessionCoordinator 用同一套配置**——用户 settings.json 里写的 45 s 现在真生效了。 |
| **32** | Bridge JS 与 OpenClaw DOM 耦合 | ⏳ 未处理 | HostedUiBridge.cs 未动。 |
| **33** | 心跳路径未协调 basePath | ⏳ 未处理 | `ProbeGatewayTransportAsync` 未动。 |

**汇总**：33 条中 9 条 ✅ 完成 + 2 条 🟡 部分 + 22 条 ⏳ 未处理。这一轮把"局部、低风险、CF Tunnel 直接相关"的全部清掉了，剩下的都是架构性 / MVVM 重构 / 上游耦合。

---

## 2. Codex 改动中新引入的小问题

### A. `AtomicFileWriter.cs:18-19` — backup 文件命名比 V1 建议复杂，但没有大问题，只有一个边角

```csharp
var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
var backupPath = $"{fullPath}.{Guid.NewGuid():N}.bak";
var backupCreated = false;
…
File.Replace(tempPath, fullPath, backupPath, ignoreMetadataErrors: true);
backupCreated = true;
```

`File.Replace` 在 Windows 上是原子的（要么完整成功要么完整失败），所以 `backupCreated = true` 紧跟在 Replace 后面是对的。**唯一小瑕疵**：如果 `File.Replace` 抛 `IOException`（极罕见，比如目标文件被另一个进程独占），`finally` 不会去清 backup——但此时 backup 也并不存在（Replace 失败 = 没产生 backup）。**实际是无害的**，可以不修。

不过有一个 *real* 小事：`AtomicFileWriter.WriteAllText` 没有 `Encoding` 参数，默认用 UTF-8 BOM-less（这里 OK），但 V1 旧的 `File.WriteAllText(SettingsFilePath, json)` 也是同样默认——行为一致，不算回归。

### B. `WebViewService.GetHeartbeatReloadCooldown` 的 `?.` 是冗余的

[WebViewService.cs:883](src/OpenClaw/Services/WebViewService.cs#L883)
```csharp
var seconds = App.Configuration.Settings.RecoveryPolicy?.HardRefreshCooldownSeconds
    ?? (int)DefaultHeartbeatReloadCooldown.TotalSeconds;
```

但 [Models/AppSettings.cs:61](src/OpenClaw/Models/AppSettings.cs#L61) 的 `RecoveryPolicy` 是 **非 nullable**：

```csharp
public RecoveryPolicyOptions RecoveryPolicy { get; set; } = new();
```

所以 `RecoveryPolicy?.HardRefreshCooldownSeconds` 永远不会触发 `??` 的右侧。NRT 模式下编译器会报 `IDE0029` / `CS8602` 类的提示。**修复**：

```csharp
var seconds = App.Configuration.Settings.RecoveryPolicy.HardRefreshCooldownSeconds;
return TimeSpan.FromSeconds(Math.Max(0, seconds));
```

不过——如果你预期 settings 反序列化时 `RecoveryPolicy` 节点缺失会 → null（旧版 settings.json 兼容场景），那 `?.` 反而是有意义的，应该把 `RecoveryPolicy` 标成 `RecoveryPolicyOptions?`。**建议二选一**：要么去掉 `?.`，要么把字段标 nullable + 在 `NormalizeSettings` 里 `??= new()` 兜底。当前的"非 nullable 字段 + 调用方 `?.`"是一种自相矛盾的写法。

> 同样的用法还出现在 `Settings.Heartbeat ??= new HeartbeatOptions();`（[ConfigurationService.cs:182](src/OpenClaw/Services/ConfigurationService.cs#L182)）——`Heartbeat` 字段是非 nullable 但代码用 `??=`。说明项目里"反序列化后字段可能为 null"的隐含假设并没有体现在类型签名里，多处地方都在按"也许是 null"防御。**建议统一一下**：要么相信 source-gen JSON 永远填非空（删 `?.` 与 `??=`），要么把所有 options 字段改成 nullable。

### C. `LoggingService` 在静态字段初始化器里跑同步 I/O

[LoggingService.cs:30](src/OpenClaw/Services/LoggingService.cs#L30)
```csharp
public LoggingService()
{
    Directory.CreateDirectory(LogFolder);
    LogFileUtilities.DeleteExpiredLogs(LogFolder, LogRetention, DateTimeOffset.UtcNow);
    _writerTask = Task.Run(ProcessQueueAsync);
}
```

`App.Logger { get; } = new LoggingService();` 是静态字段初始化器——意味着 **App 类首次访问触发**：

- `Directory.CreateDirectory` 同步 I/O（OK，本来就会跑）
- `LogFileUtilities.DeleteExpiredLogs` —— 遍历 `%LOCALAPPDATA%\OpenClaw\logs` 所有 `openclaw-*.log`、按 `LastWriteTimeUtc` 比较、可能逐个 `File.Delete`。**用户当前的 29 个文件没问题**；如果磁盘 I/O 慢或文件多了（比如 verbose logging 后开过几个月），启动会被 block 住。

**修复**（不紧急）：把 retention 从构造函数移到首次写日志时一次性触发（用 `Interlocked.CompareExchange` 标志位控制只跑一次），或者直接在 `Task.Run(ProcessQueueAsync)` 启动后异步执行——异步路径已经在了，直接放进去就行。

### D. `ControlUiLatencyService.ProbeAsync` 用 HTTP HEAD —— 极少数情况下被 OpenClaw Gateway 拒绝

[ControlUiLatencyService.cs:156](src/OpenClaw/Services/ControlUiLatencyService.cs#L156) `new HttpRequestMessage(HttpMethod.Head, probeUri)`

- OpenClaw Gateway 对根路径 GET 是肯定支持的（Control UI HTML），但 `HEAD` 不在所有部署里都被显式支持——Vite 静态服务和 Lit SPA 的根路径 HEAD 在大多数 web 服务器是 200，但 **Cloudflare Tunnel + 某些上游中间件（特别是基于 ASGI / Express） 默认不响应 HEAD，会回 405**。
- 当前代码即便 405 也算成功（`response.StatusCode` 被序列化进 `Detail`），徽章会显示 `... ms / HTTP 405 via Cloudflare`——不是 bug，但徽章 detail 里看到 405 用户会困惑。
- 也有一种角度：如果将来 Gateway 确实不接受 HEAD，5 s 超时偶尔触发 → 显示 `Stale`。

**建议**：
- 优先发 `GET /__openclaw/control-ui-config.json`（OpenClaw 上游文档明确声明这个端点存在）—— 401/200 两种状态都是 Gateway 在线。比 HEAD 通用，且自动 covers V1 #33（basePath 协调）。
- 兜底再 fall back 到 `GET probeUri` （不要 HEAD），并截短 body 用 `HttpCompletionOption.ResponseHeadersRead` —— 已经在用了。

### E. Package.appxmanifest 版本号是手动同步

[Package.appxmanifest:12](src/OpenClaw/Package.appxmanifest#L12) `Version="3.0.5.0"` 必须每次发版手动改——和 V1 #21（`AppMetadata.CurrentVersion`）是同类问题，但这次 Codex 没顺手解决。

**修复**：用 `AppxManifest` 的 `$(Version)` 替换 + MSBuild target，或者 csproj 里 `<GenerateAppxManifestPackageVersion>true</GenerateAppxManifestPackageVersion>` 让 SDK 同步。详见 [WindowsAppSDK 文档](https://learn.microsoft.com/windows/apps/windows-app-sdk/manage-app-package-identities)。

### F. README.md 里的版本号还是 v3.0.4

代码已经 3.0.5（csproj + manifest 同步了），但 README "Recent Changes" 段最新一条仍是 v3.0.4 (2026-04-29)。如果 3.0.5 的改动是这次 Codex 跑的，应该在 README 里加一段 "v3.0.5 (2026-04-30)" 把本轮 8 项落地的内容写出来。

### G. 测试用 stub 类型 + 真 helper 同时存在 → 部分类型已"半真半假"

`OpenClaw.Tests.csproj` 现在直接编译了：
- `Helpers/AtomicFileWriter.cs` ✅ 真类型（无 WinUI 依赖）
- `Helpers/LogFileUtilities.cs` ✅ 真类型
- `Models/RecoveryModels.cs` ✅ 真类型
- `Models/RecoveryPolicyOptions.cs` ✅ 真类型
- `Services/ShellSessionCoordinator.*.cs` ✅ 真类型

但 `TestSupport.cs` 还有：
- `OpenClaw.Services.WebViewService` 🟡 stub
- `OpenClaw.Services.HostedUiBridge` 🟡 stub
- `OpenClaw.Services.ControlUiProbeSnapshot` 🟡 stub
- `OpenClaw.Services.LoggingService` 🟡 stub
- `OpenClaw.App` 🟡 stub
- `OpenClaw.Models.RecoveryPolicyOptions` & `OpenClaw.Models.HeartbeatOptions` 🟡 这两个本来在 `RecoveryPolicyOptions.cs` 里，现在 csproj 已经 `<Compile Include="…/RecoveryPolicyOptions.cs">` 真类型 + TestSupport 也声明 stub —— **要么编译冲突，要么 TestSupport 里的声明被 stub 覆盖**。让我快速验证下。

实际上 TestSupport.cs 的 `TestAppSettings` 里只是 *拿* `RecoveryPolicyOptions` / `HeartbeatOptions` 类型作为属性 ——使用的是真类型，没有重声明。但 `OpenClaw.Services.LoggingService` 是个 `internal sealed class` stub，**而真 LoggingService 在生产代码里是 `public class`** —— 现在 csproj 没有 link `LoggingService.cs` 真文件，所以 stub 会被使用。但**新代码** [LogFileUtilities.cs](src/OpenClaw/Helpers/LogFileUtilities.cs) 不依赖 `LoggingService`，OK；如果未来在 helper 里 `App.Logger.Info(...)` 调一下就要小心 stub vs 真类型签名。

**总体**：这种"一半 link 一半 stub"的状态比 V1 时更脆。建议这一批后立刻拆 `OpenClaw.Core.csproj`（V1 #5）—— 不然每次 Codex 把更多 helper 移进去，stubs 与真类型的"接缝"会越来越不一致。

### H. 没有解决 V1 #11（floating versions），仍然是 CI 不可重现

`1.8.*` 等会让今天的 build 与下个月的 build 行为不一致；这本来是与 #2、#13 同等重要的"低成本一次性"项，建议这一批顺手做。

---

## 3. 仍然遗留的 V1 项目（按当前剩余优先级排序）

> 已经把 ✅ 与 🟡 移除，按"修了能立刻看到收益"重新排：

### 🟥 立刻可做的（局部、低风险、收益明确）

1. **#2-A、#2-B 善后**：`AtomicFileWriter` 与 `GetHeartbeatReloadCooldown` 的 `?.` 一致性（§2-B 详述）。10 行修改。
2. **#11 pin NuGet 版本** —— 给 csproj 锁定到具体小版本号（`Microsoft.WindowsAppSDK 1.8.250515001`、`CommunityToolkit.Mvvm 8.4.0`、`Microsoft.Windows.SDK.BuildTools 10.0.26100.59`，请用 `dotnet list package --outdated` 复核），加 `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` + 提交 `packages.lock.json`。
3. **#12 删 `RestorePackagesConfig=true`** —— 这是 packages.config 时代的兼容开关，SDK-style 项目里没用。
4. **#33 心跳路径协调 basePath** —— 把 `ProbeGatewayTransportAsync` 改成 GET `/__openclaw/control-ui-config.json`，401/200 都算 healthy。结合 §2-D 一并改 LatencyService。
5. **§2-C** 把日志 retention 移到异步路径，避免启动时同步 file delete。
6. **§2-E、§2-F** 让 Package.appxmanifest 与 README 在版本号管理上不再手动同步。
7. **#15 主题按钮 hard-coded brush** —— 移到 `App.xaml` `ResourceDictionary`，做主题/accent 适配。改动量很小，但视觉收益明显。
8. **#18 `LogLifecycleEventOnce` 的 dedup key** —— 不要 `JsonSerializer.Serialize(context)`，用 `eventName` + 直接字段比较。3-5 处调用即可改完。
9. **#22 `DwmSetWindowAttribute` 返回值检查** —— Windows 10 1809-21H2 上几个 attribute 会被忽略，加日志或文档说明，避免后续同事踩坑。

### 🟧 中等改动（一个 PR 范围内）

10. **#3 迁移到 CommunityToolkit.Mvvm**：`[ObservableProperty]` / `[RelayCommand]` 替换 18 个 ViewModel partial 中的手写 INPC 与 `SimpleCommand` —— 估算可减少 ~600 行样板。
11. **#4 合并极小 partial**：`MainViewModel.Collections.cs`(14 行)、`MainViewModel.CollectionFactory.cs`(19 行)、`MainViewModel.Commands.Properties.cs`(14 行)、`ShellSessionCoordinator.Events.cs`(45 行) 等可以与各自主文件合并。或者反过来，把行为提到嵌套类（`IndicatorPresenter`、`RecoveryDriver`），**用对象边界替代文件边界**。
12. **#14、#17 主题与 brush 在 UI 线程的隐藏耦合**：`MainViewModel.Fields.cs` 里的 static `SolidColorBrush` 移到 XAML 资源；`WindowFrameHelper` 的多次 dispatcher refresh + size nudge 在新 SDK 上重测看能否简化。
13. **#28 删 Stop 命令的 100 行 JS fallback**：当前 `InjectStopCommandAsync` 模拟 keyboard event + textarea 写入；上游 OpenClaw 已经有 `chat.abort()` 路径，bridge 也已经在用——**直接删 fallback**，bridge 路径走不通时就用 `coreWebView.Stop()`，少 100 行 + 减少跨上游版本破坏面。
14. **#32 解耦 OpenClaw 上游 DOM 字符串**：把 `HostedUiBridge.cs` 里 ~30 个 `matchAny(text, [...])` 关键字抽到资源 JSON / version-pinned；或优先用 `window.__openclaw.*` 暴露的 API。

### 🟦 架构性（多个 PR / 单独分支）

15. **#1 WebView2 隔离 → 单 profile 模式**：用户当前只有 1 个环境，最便宜的修复是直接退化到单 profile，删掉 `MainWindow.WebView.cs` 的 ~180 行 recreation 状态机。如果坚持多环境则上 `CoreWebView2ControllerOptions.ProfileName`（multi-profile API）。
16. **#5 拆 `OpenClaw.Core.csproj`**：让 `WebViewService` / `HostedUiBridge` 也能在测试里用真类型（结合 §2-G 上面看到的"半真半假" 现状，这个建议比 V1 时更紧迫）。
17. **#7 引入 DI**：`Microsoft.Extensions.DependencyInjection` 替换 `App.Configuration` / `App.Logger` / `App.MainWindow` 静态访问，顺带消化 #27 `AppTelemetry`。
18. **#20 真补测试空白**：当前只补了 helper 测试。`ConfigurationService.NormalizeSettings`（legacy `heartbeatIntervalSeconds` 迁移）、`SettingsViewModel.SaveAll`（duplicate name / scheme validation）、`WebViewService.BuildEnvironmentFolderName`（Path.GetInvalidFileNameChars sanitize + 48 字符截断 + SHA256 后缀）—— **这三块都是纯函数，写 unit test 收益最高**。

---

## 4. 推荐落地顺序（V2 调整版）

第一批（立刻、< 1 小时）：
- §2-B（去掉冗余 `?.`）
- §2-C（启动时 retention 移到异步）
- §2-E（manifest 版本号自动同步）
- §2-F（README 加 v3.0.5 changelog）
- #11（pin NuGet versions）
- #12（删 RestorePackagesConfig）

第二批（CF Tunnel / 上游协议相关）：
- §2-D + #33（HEAD → `/__openclaw/control-ui-config.json`）
- #18（dedup key 不要 Serialize）

第三批（中等改动）：
- #15（主题 brush 移资源字典）
- #28（删 Stop JS 注入 fallback）
- #20（补三块纯函数 unit test）

第四批（中等破坏，推荐独立 PR）：
- #3（CommunityToolkit.Mvvm 迁移）
- #4（合并 partial）
- #32（解耦 bridge 关键字）

第五批（架构性，单 PR + 单独分支）：
- #1（单 profile 模式 + 删 WebView 重建状态机）
- #5（拆 OpenClaw.Core）
- #7 + #27（DI 替换静态单例）

---

## 5. 给 Codex 这一轮的 commit message 建议

如果还没合：

```
v3.0.5: incremental hardening

- Atomic settings.json write via temp + File.Replace (V1 #2)
- WebViewService.Stop → StopAsync (V1 #6)
- Latency badge: ICMP → HTTP HEAD with cf-ray detection (V1 #9)
- Explicit WebView2.Close before host clear (V1 #10)
- Streaming log tail + 14-day retention (V1 #13)
- HeartbeatIndicatorViewModel: equality check + cached EventArgs (V1 #16)
- AppMetadata.CurrentVersion → assembly-derived (V1 #21)
- Reset() now clears Total* counters (V1 #26)
- HardRefreshCooldown read from settings (V1 #31)
- Tests: +4 new cases for atomic writer / log tail / retention / reset
```

review 范围：v3.0.4 → v3.0.5 之间的 16 个变更文件，对照 OPTIMIZATION_SUGGESTIONS.md 的 33 条建议。其它文件（`MainViewModel.*.cs` 大多数 partial、`MainWindow.Theme.cs`、`HostedUiBridge.cs`、`Helpers/WindowFrameHelper.cs`、`Views/SettingsDialog.*.cs` 等）未在本轮修改，状态与 V1 一致。
