# OpenClaw 优化建议 v2

> 基于对 `src/OpenClaw` 全量源码（50+ 源文件、6 个代码层）的深度审查，按优先级分类列出可执行的优化建议。
> 本版新增 Cloudflare Tunnel 专项分析与多项深层问题发现。

## 本轮 Review 校准（2026-04-28）

本轮代码 review 新增 4 条 P2 级别建议，已落实到下文对应章节：

- `WebViewService.InitializeAsync` 仍通过 `WEBVIEW2_USER_DATA_FOLDER` 设置 WebView2 profile，已补充到 [1.2](#112-webview2-userdatafolder-改用显式-environment-创建)。
- `ShellSessionCoordinator.Events` 中服务事件入口仍存在 `async void` 风险，已补充到 [4.1](#41-async-void-事件处理器改为-async-task)。
- `ControlUiLatencyService.Stop()` 停止后可能让 UI 残留旧延迟值，新增 [5.7](#57-controluilatencyservicestop-发布-unknown-状态)。
- 版本号实际分散在 `OpenClaw.csproj`、`app.manifest`、`Package.appxmanifest`、`AppMetadata.cs` 4 处，已校正 [7.1](#71-版本号一致性)。

---

## 目录

- [一、架构优化（高优先级）](#一架构优化高优先级)
  - [1.1 引入依赖注入，消除静态服务定位器](#11-引入依赖注入消除静态服务定位器)
  - [1.2 WebView2 userDataFolder 改用显式 Environment 创建](#112-webview2-userdatafolder-改用显式-environment-创建)
  - [1.3 使用 CommunityToolkit.Mvvm 全面替换手动 MVVM 样板代码](#13-使用-communitytoolkitmvvm-全面替换手动-mvvm-样板代码)
  - [1.4 MainViewModel 服务实例改为构造函数注入](#14-mainviewmodel-服务实例改为构造函数注入)
- [二、JavaScript Bridge 优化（高优先级）](#二javascript-bridge-优化高优先级)
  - [2.1 提取 JS 脚本为独立文件](#21-提取-js-脚本为独立文件)
  - [2.2 字符串转义安全加固](#22-字符串转义安全加固)
  - [2.3 SendCommandAsync JSON 注入风险](#23-sendcommandasync-json-注入风险)
- [三、Cloudflare Tunnel 专项优化（高优先级）](#三cloudflare-tunnel-专项优化高优先级)
  - [3.1 IsTunnelDetected 检测逻辑增强](#31-istunneldetected-检测逻辑增强)
  - [3.2 ICMP Ping 对 Tunnel 场景无效](#32-icmp-ping-对-tunnel-场景无效)
  - [3.3 Heartbeat 间隔与 Tunnel 空闲超时协同](#33-heartbeat-间隔与-tunnel-空闲超时协同)
  - [3.4 恢复策略 Tunnel 感知优化](#34-恢复策略-tunnel-感知优化)
  - [3.5 Heartbeat 静态 HttpClient 与 Cloudflare DNS TTL 冲突](#35-heartbeat-静态-httpclient-与-cloudflare-dns-ttl-冲突)
- [四、并发与异步安全（高优先级）](#四并发与异步安全高优先级)
  - [4.1 async void 事件处理器改为 async Task](#41-async-void-事件处理器改为-async-task)
  - [4.2 WebViewService.Stop 修复 async void](#42-webviewservicestop-修复-async-void)
  - [4.3 恢复操作 lock 块内避免 await](#43-恢复操作-lock-块内避免-await)
- [五、代码质量优化（中优先级）](#五代码质量优化中优先级)
  - [5.1 MainWindow 代码隐藏深度分析](#51-mainwindow-代码隐藏深度分析)
  - [5.2 SimpleCommand 修复 CanExecuteChanged](#52-simplecommand-修复-canexecutechanged)
  - [5.2.1 HeartbeatIndicatorViewModel 频繁创建 SolidColorBrush](#521-heartbeatindicatorviewmodel-频繁创建-solidcolorbrush-导致-gc-压力)
  - [5.2.2 WindowFrameHelper 魔法数字和 P/Invoke 错误处理缺失](#522-windowframehelper-魔法数字和-pinvoke-错误处理缺失)
  - [5.3 HttpClient 优化（DiagnosticService + WebViewService）](#53-httpclient-优化diagnosicservice--webviewservice)
  - [5.4 LoggingService Dispose 超时调整](#54-loggingservice-dispose-超时调整)
  - [5.5 消除魔法数字](#55-消除魔法数字)
  - [5.6 Navigation 自动重试潜在死循环](#56-navigation-自动重试潜在死循环)
  - [5.7 ControlUiLatencyService.Stop 发布 Unknown 状态](#57-controluilatencyservicestop-发布-unknown-状态)
- [六、安全加固（中优先级）](#六安全加固中优先级)
  - [6.1 配置敏感信息加密](#61-配置敏感信息加密)
  - [6.2 WebView2 脚本执行安全边界](#62-webview2-脚本执行安全边界)
  - [6.3 RunOnUiThread 松耦合 App.MainWindow](#63-runonuithread-松耦合-appmainwindow)
- [七、细节优化（低优先级）](#七细节优化低优先级)
  - [7.1 版本号一致性](#71-版本号一致性)
  - [7.2 StringResources 线程安全与启动验证](#72-stringresources-线程安全与启动验证)
  - [7.3 异常处理规范化](#73-异常处理规范化)
  - [7.4 HeartbeatIntervalSeconds 冗余字段](#74-heartbeatintervalseconds-冗余字段)
  - [7.5 ConfigurationService SaveDeferred 改用 PeriodicTimer](#75-configurationervice-savedeferred-改用-periodictimer)
  - [7.6 App.xaml.cs 冗余窗口追踪](#76-appxamlcs-冗余窗口追踪)
  - [7.7 App.xaml.cs UnhandledException 处理过于薄弱](#77-appxamlcs-unhandledexception-处理过于薄弱)
- [八、实施路线图建议](#八实施路线图建议)

---

## 一、架构优化（高优先级）

### 1.1 引入依赖注入，消除静态服务定位器

**现状问题**

当前几乎所有类都通过 `App.Logger`、`App.Configuration` 静态属性访问服务：

```csharp
// App.xaml.cs
public static ConfigurationService Configuration { get; } = new();
public static LoggingService Logger { get; } = new();

// 各处调用
App.Logger.Info("...");
App.Configuration.Settings.AppTheme;
```

这种方式的问题：
- 隐藏了类的真实依赖关系，可读性差
- 无法进行单元测试（无法 mock）
- 违反单一职责原则（App 类承担了全局服务容器角色）
- 服务生命周期不可控

**优化方案**

引入 `Microsoft.Extensions.DependencyInjection`，构建应用容器：

```csharp
// App.OnLaunched 中
var services = new ServiceCollection();

services.AddSingleton<LoggingService>();
services.AddSingleton<ConfigurationService>(sp =>
{
    var config = new ConfigurationService();
    config.Load();
    return config;
});
services.AddSingleton<WebViewService>();
services.AddSingleton<HostedUiBridge>();
services.AddSingleton<ShellSessionCoordinator>();
services.AddSingleton<ControlUiLatencyService>();
services.AddSingleton<MainViewModel>();

services.AddTransient<MainWindow>();

var provider = services.BuildServiceProvider();

var window = provider.GetRequiredService<MainWindow>();
window.Activate();
```

各服务通过构造函数注入：

```csharp
public class WebViewService
{
    private readonly LoggingService _logger;

    public WebViewService(LoggingService logger)
    {
        _logger = logger;
    }
}
```

**预期收益**
- 依赖关系显式化，可读性大幅提升
- 支持单元测试 mock
- 服务生命周期统一管理
- 便于后续功能扩展

**工作量**：中（需要重构约 30+ 个文件的依赖注入方式）

---

### 1.2 WebView2 userDataFolder 改用显式 Environment 创建

**现状问题**

`WebViewService.InitializeAsync` 使用进程级环境变量设置 userData 文件夹：

```csharp
// WebViewService.cs:132
Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataFolder);
await _webView.EnsureCoreWebView2Async();
```

这是进程级别的设置，如果未来需要同时管理多个 WebView2 实例（如多环境并行），会产生竞态条件。

**本轮 Review 补充**

当前代码虽然只有单一主 WebView 顺序初始化，但 `WEBVIEW2_USER_DATA_FOLDER` 仍是进程级状态。后续只要引入并行 WebView、预加载、设置页预览、测试宿主或多窗口，就可能出现 profile 串用。该项应优先确认当前 WinUI/WebView2 SDK 是否支持 per-control/per-environment 的 `CoreWebView2Environment` 创建方式，并尽快移除全局环境变量依赖。

**优化方案**

使用 `CoreWebView2Environment.CreateAsync` 显式指定 userDataFolder：

```csharp
public async Task InitializeAsync(WebView2 webView, string environmentName)
{
    DetachCurrentWebView();
    _webView = webView;
    _coreWebView = null;
    CurrentEnvironmentName = environmentName;
    _isInitialized = false;

    try
    {
        var userDataFolder = GetUserDataFolderForEnvironment(environmentName);
        Directory.CreateDirectory(userDataFolder);

        var environment = await CoreWebView2Environment.CreateAsync(
            userDataFolder: userDataFolder);

        await _webView.EnsureCoreWebView2Async(environment);
        _coreWebView = _webView.CoreWebView2;
        // ... 其余初始化逻辑
    }
    catch (Exception ex)
    {
        // ...
    }
}
```

**预期收益**
- 消除进程级状态污染
- 支持多实例并发初始化
- 更符合 WebView2 官方推荐做法

**工作量**：低（仅修改 `WebViewService.InitializeAsync` 方法）

---

### 1.3 使用 CommunityToolkit.Mvvm 全面替换手动 MVVM 样板代码

**现状问题**

项目中同时存在两处未充分利用 CommunityToolkit.Mvvm 的场景：

**（a）自定义命令类**：自定义了 `SimpleCommand` 和 `AsyncCommand`，`CanExecuteChanged` 被完全禁用（suppress warning CS0067），命令永远不会重新评估可执行状态。

```csharp
// Helpers/SimpleCommand.cs
public class SimpleCommand : ICommand
{
#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _action();
}
```

**（b）MainViewModel 中约 25 个属性的手动 INotifyPropertyChanged 实现**：

```csharp
// MainViewModel.Shared.cs — 手动实现
protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
{
    if (EqualityComparer<T>.Default.Equals(field, value)) return false;
    field = value;
    OnPropertyChanged(name);
    return true;
}

// MainViewModel.Core.Properties.cs — 每个属性都写一次
public string StatusMessage
{
    get => _statusMessage;
    private set => SetProperty(ref _statusMessage, value);
}
```

这意味着在 `MainViewModel.Core.Properties.cs`、`MainViewModel.Status.cs`、`MainViewModel.Environment.cs` 等文件中，每个属性都要手动写 backing field、getter、setter、`SetProperty` 调用，总计数百行样板代码。

**优化方案**

同时使用 CommunityToolkit.Mvvm 的两个核心功能：

```csharp
// Commands
ReloadCommand = new RelayCommand(OnReload);
RunDiagnosticsCommand = new AsyncRelayCommand(OnRunDiagnosticsAsync);

// Properties — 使用 [ObservableProperty]
[ObservableProperty]
private string _statusMessage = string.Empty;

[ObservableProperty]
private bool _isLoading;
// 编译器自动生成 StatusMessage 和 IsLoading 属性 + PropertyChanged 通知
```

对于有额外逻辑的属性（如 `IsLoading` 需要同时通知 `LoadingVisibility`），使用 partial method：

```csharp
partial void OnIsLoadingChanged(bool value)
{
    OnPropertyChanged(nameof(LoadingVisibility));
}
```

**预期收益**
- 消除约 60 行自定义命令代码
- 消除 MainViewModel 中约 200+ 行手动属性样板代码
- 内置 `CanExecuteChanged` 支持
- 内置异常处理和线程安全
- `[ObservableProperty]` 生成的代码在编译期完成，零运行时开销

**工作量**：低（社区工具包的增量源码生成器，重构风险可控）

---

### 1.4 MainViewModel 服务实例改为构造函数注入

**现状问题**

`MainViewModel.Fields.cs` 中服务以 `new` 直接实例化：

```csharp
private readonly WebViewService _webViewService = new();
private readonly HostedUiBridge _hostedUiBridge = new();
private readonly ControlUiLatencyService _latencyService = new();
```

Coordinator 在 `InitializeCoordinator()` 中 new：

```csharp
_coordinator = new ShellSessionCoordinator();
```

这意味着 MainViewModel 无法脱离具体实现进行测试，且服务生命周期与 ViewModel 强绑定。

**优化方案**

```csharp
public class MainViewModel
{
    private readonly WebViewService _webViewService;
    private readonly HostedUiBridge _hostedUiBridge;
    private readonly ControlUiLatencyService _latencyService;
    private readonly ShellSessionCoordinator _coordinator;

    public MainViewModel(
        WebViewService webViewService,
        HostedUiBridge hostedUiBridge,
        ControlUiLatencyService latencyService,
        ShellSessionCoordinator coordinator)
    {
        _webViewService = webViewService;
        _hostedUiBridge = hostedUiBridge;
        _latencyService = latencyService;
        _coordinator = coordinator;
        // ...
    }
}
```

**工作量**：中（配合 1.1 一起实施）

---

## 二、JavaScript Bridge 优化（高优先级）

### 2.1 提取 JS 脚本为独立文件

**现状问题**

`HostedUiBridge.BuildBridgeScript()` 方法内联了约 680 行 JavaScript 代码。问题：
- JS 代码缺乏语法高亮和 IDE 支持
- 难以编写单元测试
- 修改后需要重新编译整个 C# 项目
- 字符串拼接容易出错

**优化方案**

将 JS 代码提取为嵌入资源文件：

```
OpenClaw/
  Resources/
    BridgeScript.js          # 原始 JS 脚本
    BridgeScript.strings.json # 本地化字符串映射
```

JS 模板中使用占位符，C# 侧加载后注入。

**预期收益**
- JS 代码获得完整的 IDE 支持（lint、格式化、类型检查）
- 可独立编写 JS 单元测试
- 模板化注入降低字符串拼接错误风险
- 支持热更新（可选）

**工作量**：中

---

### 2.2 字符串转义安全加固

**现状问题**

`JsString` 方法的转义不完整：

```csharp
private static string JsString(string value)
{
    return value
        .Replace("\\", "\\\\")
        .Replace("'", "\\'")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n");
}
```

遗漏了：反引号 `` ` ``、`</script>` 标签闭合、`\u2028`/`\u2029` 行分隔符。

**优化方案**

使用 `System.Text.Json.JsonSerializer.Serialize(value)` 确保所有特殊字符被正确转义。

**工作量**：低

---

### 2.3 SendCommandAsync JSON 注入风险

**现状问题**

`HostedUiBridge.SendCommandAsync` 中通过字符串拼接构造脚本：

```csharp
var message = new { kind = "command", command, payload };
var json = JsonSerializer.Serialize(message);
var script = $"(async () => await window.__openClawHostBridge?.onCommand?.({json}) ?? false)()";
var raw = await coreWebView.ExecuteScriptAsync(script);
```

如果 `command` 或 `payload` 包含恶意构造的内容，`JsonSerializer.Serialize` 虽然会转义字符串值，但整个 JSON 对象被直接嵌入到 IIFE 中执行。虽然当前 payload 是应用内部控制的结构化数据，风险较低，但最佳实践是将命令与参数分离传递。

**优化方案**

```csharp
var script = $"(async () => await window.__openClawHostBridge?.onCommand?.({commandJson}, {payloadJson}) ?? false)()";
```

或在 JS 侧使用 `postMessage` 替代 `ExecuteScriptAsync` 传递命令。

**工作量**：低

---

## 三、Cloudflare Tunnel 专项优化（高优先级）

> 基于你使用的 Cloudflare Tunnel 架构（本地 OpenClaw → Cloudflare Edge → VPS Origin），以下优化针对此网络拓扑。

### 3.1 IsTunnelDetected 检测逻辑增强

**现状问题**

`ShellSessionCoordinator.Helpers.cs` 中的 Tunnel 检测仅基于 URL 字符串匹配：

```csharp
private bool IsTunnelDetected()
{
    if (string.IsNullOrEmpty(_currentGatewayUrl)) return false;
    return _currentGatewayUrl.Contains("cloudflare") ||
           _currentGatewayUrl.Contains("workers.dev") ||
           _currentGatewayUrl.Contains("trycloudflare.com");
}
```

问题：
- 大小写敏感（`Contains` 未指定 `StringComparison`）
- 如果用户自定义域名通过 Cloudflare CNAME 接入（非 `*.trycloudflare.com`），无法检测
- 字符串匹配脆弱，URL 中任意位置出现 "cloudflare" 都会命中

**优化方案**

```csharp
private bool IsTunnelDetected()
{
    if (string.IsNullOrEmpty(_currentGatewayUrl)) return false;

    if (!Uri.TryCreate(_currentGatewayUrl, UriKind.Absolute, out var uri))
        return false;

    var host = uri.Host.ToLowerInvariant();
    return host.EndsWith(".trycloudflare.com") ||
           host.EndsWith(".workers.dev") ||
           host.EndsWith(".cfargotunnel.com");
}
```

同时建议在 `EnvironmentConfig` 中增加 `IsCloudflareTunnel` 布尔字段，让用户显式标记。

**工作量**：低

### 3.2 ICMP Ping 对 Tunnel 场景无效

**现状问题**

`ControlUiLatencyService` 使用 ICMP Ping 测量延迟：

```csharp
using var ping = new Ping();
var reply = await ping.SendPingAsync(host, PingTimeoutMilliseconds);
```

当 Gateway URL 是 Cloudflare Tunnel 域名（如 `xxx.trycloudflare.com`）时，ICMP Ping 实际测量的是到 Cloudflare Edge 的延迟，而非到 VPS Origin 的延迟。这导致：
- Latency 指标不能反映真实端到端延迟
- Cloudflare Edge 通常支持 ICMP，即使 Origin 完全不可达，Ping 仍可能成功
- 延迟数据在 Tunnel 场景下误导性强

**优化方案**

方案 A（推荐）：对 Tunnel 场景改用 HTTP 探测延迟

```csharp
private async Task<ControlUiLatencySnapshot> ProbeAsync(string host, bool isTunnel)
{
    if (isTunnel)
    {
        // 对 Tunnel 场景，改用 HTTP GET 测量 RTT
        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await _httpclient.GetAsync(_currentUrl, HttpCompletionOption.ResponseHeadersRead);
            sw.Stop();
            return ControlUiLatencySnapshot.Success(host, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ControlUiLatencySnapshot.Failure(host, ex.Message);
        }
    }

    // 非 Tunnel 场景继续使用 ICMP
    using var ping = new Ping();
    var reply = await ping.SendPingAsync(host, PingTimeoutMilliseconds);
    return reply.Status == IPStatus.Success
        ? ControlUiLatencySnapshot.Success(host, reply.RoundtripTime)
        : ControlUiLatencySnapshot.Failure(host, reply.Status.ToString());
}
```

方案 B：直接禁用 Tunnel 场景下的 Latency 显示，避免误导用户。

**工作量**：中

### 3.3 Heartbeat 间隔与 Tunnel 空闲超时协同

**现状**

当前 Heartbeat 默认间隔 30 秒，注释说明 "works well with Cloudflare Tunnel / reverse proxy idle timeouts (60-100s)"。Cloudflare Tunnel 的 `cloudflared` 默认 HTTP 空闲超时约为 60-100 秒。

**优化建议**

1. 当 `IsTunnelDetected()` 为 true 时，自动将 Heartbeat 间隔调整为不超过 25 秒（留足余量）
2. 在 Heartbeat probe 中检测 `cf-ray` 响应头（已有），如果连续多次未出现 `cf-ray`，说明请求可能未通过 Cloudflare，发出警告
3. 增加 Heartbeat 失败时的 Tunnel 感知日志：

```csharp
if (IsTunnelDetected() && probe.Status == HeartbeatProbeStatus.Failure)
{
    _logger.Warning("Heartbeat failed in Tunnel mode. Check: " +
        "1) cloudflared process status, " +
        "2) origin server health, " +
        "3) Tunnel registration validity.");
}
```

**工作量**：低

### 3.4 恢复策略 Tunnel 感知优化

**现状问题**

`ShellSessionCoordinator` 的恢复策略（Reconnect → SoftResync → HardRefresh）对所有网络拓扑一视同仁。在 Tunnel 场景下：

- Tunnel 断连后恢复较慢（需要重新建立 `cloudflared` 连接）
- 当前的 `ReconnectDelayMs = 1200` 和 `MaxReconnectDelayMs = 45000` 对 Tunnel 场景可能不够
- `HardRefreshCooldownSeconds = 75` 与 Heartbeat 的 75 秒 reload cooldown 相同，但在 Tunnel 场景下可能需要更长

**优化方案**

```csharp
private TimeSpan CalculateReconnectDelay()
{
    var baseDelay = _recoveryOptions.ReconnectDelayMs;
    var backoff = Math.Pow(_recoveryOptions.ReconnectBackoffMultiplier, _reconnectAttempts - 1);
    var delay = baseDelay * backoff;
    delay = Math.Min(delay, _recoveryOptions.MaxReconnectDelayMs);

    // Tunnel 场景增加额外延迟
    if (IsTunnelDetected())
    {
        delay = Math.Max(delay, 3000); // 最小 3 秒
        if (_reconnectAttempts > 2)
        {
            delay = Math.Max(delay, 10000); // 第 3 次起最小 10 秒
        }
    }

    return TimeSpan.FromMilliseconds(delay);
}
```

**工作量**：低

### 3.5 Heartbeat 静态 HttpClient 与 Cloudflare DNS TTL 冲突

**现状问题**

`WebViewService` 中 Heartbeat 使用静态 `HttpClient`：

```csharp
// WebViewService.cs:52
private static readonly HttpClient HeartbeatHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
```

在 Cloudflare Tunnel 场景下，这会引发 **DNS TTL 与连接池的交互问题**：

1. **Cloudflare DNS TTL 极短**：`*.trycloudflare.com` 的 DNS TTL 约 1 分钟（Cloudflare 的 Anycast 路由会频繁变更 Edge 节点 IP）。当你使用 `cloudflared` 本地隧道时，每次 `cloudflared` 重启或 Cloudflare 轮换 Edge 节点，域名解析结果都会变化。

2. **Windows DNS 缓存**：Windows 本地 DNS Client 服务（`Dnscache`）会缓存 DNS 解析结果，默认 TTL 可能长于 Cloudflare 的 1 分钟。在缓存过期前，`HttpClient` 仍会向旧 IP 发起请求。

3. **HttpClient 连接池不重解析 DNS**：`static readonly HttpClient` 维护持久的 TCP 连接池。即使 DNS 缓存已刷新，已建立的连接不会重新解析 DNS。当 Cloudflare Edge IP 变更后，`HttpClient` 会持续向旧 IP 发送请求，导致 Heartbeat 失败。

4. **失败表现**：Heartbeat 连续失败 → 触发 `HeartbeatFailed` → WebView2 反复刷新 → 但刷新后的 WebView2 内部使用独立的网络栈（Chromium），反而可能正常连接。**结果就是 Heartbeat 误报，触发不必要的 WebView 刷新。**

**优化方案**

方案 A（推荐）：Tunnel 场景下为 Heartbeat 创建独立 HttpClient，每次请求后主动释放连接

```csharp
private static HttpClient CreateHeartbeatClient(bool isTunnel)
{
    var handler = new SocketsHttpHandler
    {
        PooledConnectionLifetime = isTunnel
            ? TimeSpan.FromMinutes(1)  // 1 分钟后强制重建连接，触发 DNS 重解析
            : Timeout.InfiniteTimeSpan,
        PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
        MaxConnectionsPerServer = 1,
        ConnectTimeout = TimeSpan.FromSeconds(5),
    };

    return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
}
```

方案 B：每次 Heartbeat 探测前主动刷新 DNS 缓存

```csharp
private async Task<HeartbeatProbeResult> ProbeGatewayTransportAsync(string url, CancellationToken token)
{
    if (IsTunnelDetected())
    {
        // 强制 DNS 重解析，清除旧缓存
        var uri = new Uri(url);
        _ = Dns.GetHostEntry(uri.Host);
    }

    // ... 原有请求逻辑
}
```

方案 C：在 Heartbeat 失败判断中增加 DNS 变更检测

```csharp
private string? _lastResolvedIp;

private async Task<HeartbeatProbeResult> ProbeGatewayTransportAsync(string url, CancellationToken token)
{
    var uri = new Uri(url);
    var currentIp = TryResolveCurrentIp(uri.Host);

    if (IsTunnelDetected() && _lastResolvedIp is not null && currentIp is not null && currentIp != _lastResolvedIp)
    {
        App.Logger.Info($"DNS IP changed from {_lastResolvedIp} to {currentIp}, recreating HeartbeatHttpClient.");
        HeartbeatHttpClient.Dispose();
        HeartbeatHttpClient = CreateHeartbeatClient(isTunnel: true);
    }

    _lastResolvedIp = currentIp;
    // ... 原有请求逻辑
}
```

**推荐组合使用方案 A + 方案 C**：设置 `PooledConnectionLifetime = 1min` 防止连接长期持有旧 IP，同时检测 DNS 变化时主动重建客户端。

**额外影响 — WebView2 内部网络栈**：

WebView2 使用独立的 Chromium 网络栈，不受 `HttpClient` 连接池影响。但 WebView2 同样受 Windows DNS 缓存影响。当 DNS 变更后：
- Heartbeat（HttpClient）→ 可能仍连旧 IP → 失败
- WebView2 → 可能仍连旧 IP → 连接超时
- 两者同时失败 → 触发恢复流程 → WebView 重建 → 重新导航 → 此时 DNS 可能已刷新 → 成功

这意味着 **Tunnel 场景下 DNS 变更会导致约 1-2 分钟的"假性断连窗口"**。建议在恢复策略中对此场景增加容错（见 3.4）。

**工作量**：中

---

## 四、并发与异步安全（高优先级）

### 4.1 async void 事件处理器改为 async Task

**现状问题**

全项目共发现 **8 个 `async void` 实例**，分布在 4 个文件中：

```
ShellSessionCoordinator.Events.cs  —  OnEventGapDetected()           (line 8)
ShellSessionCoordinator.Events.cs  —  OnHeartbeatFailed()            (line 12)
WebViewService.cs                  —  Stop()                         (line 234)
WebViewService.cs                  —  OnNavigationCompleted()        (line 940)
MainWindow.Lifecycle.cs            —  OnWindowVisibleAsync()         (line 134)
MainWindow.Commands.cs             —  OnViewLogsRequested()          (line 106)
MainWindow.Commands.cs             —  OnAboutClick()                 (line 120)
SimpleCommand.cs                   —  Observe(Task task)             (line 51)
```

MainWindow 层的三个 `async void` 实例：

```csharp
// MainWindow.Lifecycle.cs:134 — 窗口可见性变化
private async void OnWindowVisibleAsync(object sender, WindowEventArgs args)
{
    await OnHostVisibilityChangedAsync(_appWindow.Visible);
}

// MainWindow.Commands.cs:106 — 日志查看器
private async void OnViewLogsRequested()
{
    await ShowLogViewerAsync();
}

// MainWindow.Commands.cs:120 — 关于对话框
private async void OnAboutClick(object sender, RoutedEventArgs e)
{
    var dialog = new AboutDialog { XamlRoot = this.Content.XamlRoot };
    await dialog.ShowAsync();
}
```

`async void` 的问题：
- 异常无法被调用方捕获，可能导致进程崩溃
- 无法等待完成，测试困难
- 在恢复操作中如果抛出异常，整个应用可能崩溃
- 对话框/窗口操作中 `await` 失败时异常直接抛出到消息循环

**本轮 Review 补充**

`ShellSessionCoordinator.Events.cs` 中的 `OnEventGapDetected` 和 `OnHeartbeatFailed` 不是普通 UI click handler，而是恢复协调器的服务事件入口。它们会触发 reconnect、soft resync、hard refresh 等关键路径。当前内部实现已有局部 try-catch，但事件入口仍是 `async void`，未来如果新增前置/后置逻辑或遗漏异常处理，异常会直接进入全局异常链。建议将这两个入口优先改为带异常观察的 fire-and-forget helper，或把事件模型升级为 `Func<T, Task>`。

**优化方案**

```csharp
// 改为 async Task，调用方使用 Fire-and-forget 模式但带异常保护
private void OnEventGapDetected(EventGapEventArgs args)
{
    _ = HandleEventGapDetectedAsync(args).ContinueWith(t =>
    {
        if (t.IsFaulted) _logger.Error($"EventGap handler error: {t.Exception?.GetBaseException().Message}");
    }, TaskContinuationOptions.OnlyOnFaulted);
}

private void OnHeartbeatFailed(string message)
{
    _ = HandleHeartbeatFailedAsync(message).ContinueWith(t =>
    {
        if (t.IsFaulted) _logger.Error($"HeartbeatFailed handler error: {t.Exception?.GetBaseException().Message}");
    }, TaskContinuationOptions.OnlyOnFaulted);
}

// MainWindow 层使用 SimpleCommand.Observe 包裹
private void OnViewLogsRequested(object sender, RoutedEventArgs e)
{
    SimpleCommand.Observe(ShowLogViewerAsync());
}

private void OnAboutClick(object sender, RoutedEventArgs e)
{
    var dialog = new AboutDialog { XamlRoot = this.Content.XamlRoot };
    SimpleCommand.Observe(dialog.ShowAsync());
}
```

或更简洁地，在 `HandleEventGapDetectedAsync` / `HandleHeartbeatFailedAsync` 内部包裹 try-catch。

**工作量**：低

### 4.2 WebViewService.Stop 修复 async void

**现状问题**

```csharp
public async void Stop()
{
    var coreWebView = GetCoreWebView();
    if (coreWebView is null) return;

    var aborted = await TryAbortActiveRunAsync();
    if (aborted) { /* ... */ return; }
    // ...
}
```

`async void` 方法同样存在异常无法捕获的问题。Stop 操作如果失败（如 COMException），异常会直接抛出到调用方无法捕获。

**优化方案**

```csharp
public async Task StopAsync()
{
    // ... 相同逻辑
}

// 保留同步 Stop 作为便捷方法
public void Stop()
{
    _ = StopAsync().ContinueWith(t =>
    {
        if (t.IsFaulted) App.Logger.Warning($"Stop failed: {t.Exception?.GetBaseException().Message}");
    }, TaskContinuationOptions.OnlyOnFaulted);
}
```

**工作量**：低

### 4.3 恢复操作 lock 块内避免 await

**现状问题**

`ShellSessionCoordinator.RecoveryLifecycle.cs` 中 `TryStartRecoveryOperation` 使用 `lock` 保护关键区域：

```csharp
lock (_recoveryGate)
{
    if (ShouldThrottleRecoveryOperation(operation, reason)) return null;
    var startedAt = DateTimeOffset.Now;
    var cancellationSource = PrepareRecoveryCancellationSource();
    var attempt = RegisterRecoveryOperationStart(operation, reason, startedAt);
    context = new RecoveryOperationContext(operation, reason, attempt, cancellationSource);
}
```

当前代码在 lock 块内没有 await，这是正确的。但 `PrepareRecoveryCancellationSource` 中 `_recoveryCts?.Cancel()` 可能触发回调，如果回调中也尝试获取 `_recoveryGate` 锁，会导致死锁。

**优化建议**

确保所有在 `_recoveryCts.Cancel()` 回调中可能执行的代码不会尝试获取 `_recoveryGate` 锁。当前代码看起来是安全的，但建议在代码注释中明确这一点。

**工作量**：极低（仅添加注释）

---

## 五、代码质量优化（中优先级）

### 5.1 MainWindow 代码隐藏深度分析

**现状问题**

MainWindow 由 7 个 partial class 文件 + 1 个 XAML 文件组成。整体采用 partial class 拆分是合理的做法，但深入分析后存在以下问题：

#### 5.1.1 内存泄漏风险（4 处）

**rootElement 事件订阅未取消**（`MainWindow.Initialization.cs` 和 `SettingsDialog.Initialization.cs`）：

```csharp
// MainWindow.Initialization.cs — AttachRootEventHandlers
rootElement.Loaded += OnRootLoaded;
rootElement.ActualThemeChanged += OnRootActualThemeChanged;

// SettingsDialog.Initialization.cs:27-28 — 同样的模式
rootElement.Loaded += OnRootLoaded;
rootElement.ActualThemeChanged += OnRootActualThemeChanged;
```

这两个事件在窗口生命周期中不会被取消订阅。如果 `Content` 被替换，旧 rootElement 的事件处理器的闭包会持有窗口的强引用，导致 GC 无法回收。**MainWindow 和 SettingsDialog 都有此问题**。

**SettingsDialog 状态累积**（`MainWindow.Commands.cs`）：

`_settingsWindow` 字段在窗口关闭时置 null 并取消事件订阅，但 SettingsDialog 内部的表单状态（已修改但未保存的字段）会在每次重新打开时通过 `SyncWithCurrentSettings()` 重新初始化。如果 SettingsDialog 内部持有对 WebView 或 Coordinator 的引用，可能在跨窗口打开期间积累状态。

**WebView2 COM 对象释放延迟**（`MainWindow.WebView.cs`）：

```csharp
// RecreateWebViewAsync — 先 Clear 再 Add
WebViewHost.Children.Clear();
await InitializeWebViewAsync();
```

`Children.Clear()` 仅从视觉树移除控件，不保证 WebView2 底层 COM 对象立即释放。在快速连续触发重建时（如 DNS 变更后 Heartbeat 连续失败），可能导致：
- 旧 WebView2 的 COM 对象泄漏，占用原生内存
- 多个 WebView2 进程同时存在，竞争 userDataFolder 锁
- `InitializeAsync` 抛出 "WebView2 environment already in use" 异常

**优化方案**：

```csharp
// WebViewHost.Children.Clear() 前显式释放 COM
if (WebViewHost.Children.FirstOrDefault() is WebView2 oldWebView)
{
    oldWebView.Close();  // 触发 COM 释放
}
WebViewHost.Children.Clear();
```

#### 5.1.2 UI 线程阻塞

**App.Configuration.Save() 同步 I/O**（`MainWindow.Lifecycle.cs`）：

```csharp
private void OnWindowClosed(object sender, WindowEventArgs args)
{
    App.Configuration.Save();  // 同步写入磁盘
}
```

`Save()` 方法内部执行同步文件写入。在窗口关闭期间，这会导致主线程阻塞，如果磁盘 I/O 慢（如机械硬盘、网络驱动器），用户会感知到窗口关闭延迟。

**优化方案**：

```csharp
private void OnWindowClosed(object sender, WindowEventArgs args)
{
    _ = App.Configuration.SaveAsync().ContinueWith(t =>
    {
        if (t.IsFaulted)
            System.Diagnostics.Debug.WriteLine($"Save failed: {t.Exception?.GetBaseException().Message}");
    });
}
```

#### 5.1.3 同步布局强制（UpdateTitleBarColors）

`MainWindow.Theme.cs` 的 `RefreshTitleBarVisualState` 中：

```csharp
AppTitleBar.InvalidateMeasure();
AppTitleBar.InvalidateArrange();
rootElement.InvalidateMeasure();
rootElement.InvalidateArrange();
rootElement.UpdateLayout();  // 强制同步布局计算
```

`UpdateLayout()` 会阻塞当前线程直到布局系统完成所有挂起的测量和排列操作。在主题切换期间调用此方法会导致可见的 UI 卡顿，尤其是在窗口尺寸较大或布局复杂时。

**注意**：`SettingsDialog.Theme.cs` 的 `RefreshTitleBarVisualState` 中也存在完全相同的 `UpdateLayout()` 调用（line 71）。

**优化方案**：移除 `UpdateLayout()`，仅保留 `InvalidateMeasure()` + `InvalidateArrange()`，让布局系统在下一个渲染帧异步处理。如果确实需要同步布局结果，考虑使用 `DispatcherQueue.TryEnqueue` 延迟执行。

#### 5.1.4 紧耦合模式

`App.Configuration` 在 **8 个文件**中被直接引用：

**MainWindow 层（5 处）：**
- `MainWindow.Theme.cs` — `App.Configuration.Settings.AppTheme`
- `MainWindow.Lifecycle.cs` — `App.Configuration.Save()`, `App.Configuration.Settings.*`（4 处）
- `MainWindow.Commands.cs` — 间接通过 SettingsDialog

**ViewModel 层（12+ 处）：**
- `MainViewModel.Environment.cs` — `App.Configuration.Settings.Environments`（2 处）、`App.Configuration.GetSelectedEnvironment()`、`App.Configuration.Settings.SelectedEnvironmentName`、`App.Configuration.SaveDeferred()`
- `MainViewModel.Heartbeat.cs` — `App.Configuration.Settings.Heartbeat.EnableHeartbeat`、`App.Configuration.Settings.Heartbeat.IntervalSeconds`
- `MainViewModel.Status.cs` — `App.Configuration.Settings.Diagnostics.EnableVerboseRecoveryLogging`
- `MainViewModel.Commands.cs` — `App.Configuration.DeferredSaveRequests`、`App.Configuration.DeferredSaveCoalescedRequests`
- `MainViewModel.Lifecycle.cs` — 间接通过 `_coordinator`

`App.Logger` 在 **5 个文件**中被直接引用：
- `MainViewModel.Commands.cs` — `App.Logger.Info()`, `App.Logger.Error()`
- `MainViewModel.Lifecycle.cs` — `App.Logger.Info()`（5 处）
- `MainViewModel.Status.cs` — `App.Logger.Info()`（telemetry 日志）
- `MainWindow.Commands.cs` — `App.Logger.Error()`
- `MainWindow.Initialization.cs` — `App.Logger.Info()`

这使得 MainViewModel 和 MainWindow 都无法脱离 App 静态上下文进行独立测试。建议通过 ViewModel 或构造函数注入来解耦。

#### 5.1.5 硬编码资源路径

```csharp
// MainWindow.Initialization.cs
Icon = new Uri("Assets\\WindowIcon.ico");
```

硬编码的路径在项目结构调整时会静默失效。建议使用 `Path.Combine(AppContext.BaseDirectory, "Assets", "WindowIcon.ico")` 或在 csproj 中定义常量。

**注意**：`SettingsDialog.ConfigureWindowChrome()` 中也存在完全相同的硬编码路径：

```csharp
// SettingsDialog.Initialization.cs:15
AppWindow.SetIcon("Assets\\WindowIcon.ico");
```

两处应统一为一个常量。

**工作量**：中（5.1.3 和 5.1.4 需要配合架构调整）

---

### 5.1.6 MainViewModel.Dispose() 从未被调用

**现状问题**

`MainViewModel` 已经正确实现了 `IDisposable` 接口：

```csharp
// MainViewModel.cs:12
public partial class MainViewModel : INotifyPropertyChanged, IDisposable
{
    // ...
    public void Dispose() { /* 清理服务订阅和释放资源 */ }
}
```

但 MainWindow 中从未调用 `ViewModel.Dispose()`。窗口关闭时：

```csharp
// MainWindow.Lifecycle.cs
private void OnWindowClosed(object sender, WindowEventArgs args)
{
    App.Configuration.Save();  // 只有这一行
}
```

这意味着：
- WebViewService、HostedUiBridge、ControlUiLatencyService、ShellSessionCoordinator 的 Dispose 从未被调用
- 事件订阅永远不会被取消，可能导致 GC 无法回收
- 定时器（heartbeat、latency probe）可能继续运行
- COM 资源（WebView2）可能不会被释放

此外，`WebViewService.Dispose()` 仅取消事件订阅并置 null，未调用 `coreWebView.Close()` 释放 COM 对象：

```csharp
// WebViewService.cs:1043
public void Dispose()
{
    DetachCurrentWebView();   // 仅 Unsubscribe + null 赋值
    _retryCts?.Dispose();
}

// DetachCurrentWebView 内部：
_webView = null;
_coreWebView = null;  // 未调用 coreWebView.Close()
```

**优化方案**

```csharp
// MainWindow.Lifecycle.cs
private void OnWindowClosed(object sender, WindowEventArgs args)
{
    ViewModel.Dispose();
    _ = App.Configuration.SaveAsync();
}

// WebViewService.Dispose()
public void Dispose()
{
    if (_coreWebView is not null)
    {
        _coreWebView.Close();  // 释放 COM 资源
    }
    DetachCurrentWebView();
    _retryCts?.Dispose();
}
```

**工作量**：极低（两行代码）

---

### 5.2 SimpleCommand 修复 CanExecuteChanged

**如果暂时不替换为 RelayCommand**，至少修复 `CanExecuteChanged` 的问题：

```csharp
public class SimpleCommand : ICommand
{
    private readonly Action _action;
    private readonly Func<bool>? _canExecute;

    public SimpleCommand(Action action, Func<bool>? canExecute = null)
    {
        _action = action;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _action();

    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

**工作量**：低

---

### 5.2.1 HeartbeatIndicatorViewModel 频繁创建 SolidColorBrush 导致 GC 压力

**现状问题**

```csharp
// HeartbeatIndicatorViewModel.cs
public Brush FillBrush
{
    get => _fillBrush;
    set
    {
        _fillBrush = value;  // 每次赋值创建新的 SolidColorBrush
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FillBrush)));
    }
}
```

Heartbeat 和 Run 指示器共 24 个，每个指示器在动画更新时都通过 `new SolidColorBrush(color)` 创建新画刷对象。430ms 刷新一次意味着每秒可能创建 2-4 个新 `SolidColorBrush` 实例，对 Gen 0 GC 产生不必要的压力。

此外，`HeartbeatIndicatorViewModel` 中的默认灰色 `Color.FromArgb(255, 107, 114, 128)` 是硬编码的，不跟随系统主题变化。

**优化方案**

预创建一组共享的静态 SolidColorBrush 实例，通过颜色查找复用：

```csharp
private static readonly Dictionary<Color, SolidColorBrush> _brushCache = new()
{
    { Color.FromArgb(255, 107, 114, 128), new SolidColorBrush(Color.FromArgb(255, 107, 114, 128)) },
    { Color.FromArgb(255, 34, 197, 94), new SolidColorBrush(Color.FromArgb(255, 34, 197, 94)) },
    // ... 其他颜色
};
```

或在 XAML 中将颜色定义为 `StaticResource`，绑定改为 `Color` 而非 `Brush`：

```xml
<Ellipse Fill="{x:Bind FillColor, Mode=OneWay}" />
```

**工作量**：低

---

### 5.2.2 WindowFrameHelper 魔法数字和 P/Invoke 错误处理缺失

**现状问题**

`WindowFrameHelper.cs` 中存在大量魔法数字：

```csharp
Color.FromArgb(255, 32, 32, 32)    // 深色背景
Color.FromArgb(255, 243, 243, 243) // 浅色背景
Color.FromArgb(96, 255, 255, 255)  // 深色按钮悬停 (alpha=96)
Color.FromArgb(20, 0, 0, 0)        // 浅色按钮悬停 (alpha=20)
Color.FromArgb(144, 255, 255, 255) // 深色按钮按下
Color.FromArgb(36, 0, 0, 0)        // 浅色按钮按下
```

此外，所有 P/Invoke 调用（`DwmSetWindowAttribute`、`SetWindowPos`、`RedrawWindow`）都没有检查返回值：

```csharp
DwmSetWindowAttribute(hwnd, DwmWindowAttributeUseImmersiveDarkMode, ref useDarkMode, sizeof(int));
// 如果 DWM 服务不可用（如远程桌面会话），这里会静默失败
```

**优化方案**

提取颜色常量，增加 P/Invoke 返回值检查：

```csharp
private static bool DwmSetWindowAttributeSafe(IntPtr hwnd, int attr, ref uint value, int size)
{
    var result = DwmSetWindowAttribute(hwnd, attr, ref value, size);
    if (result != 0)
    {
        App.Logger.Warning($"DwmSetWindowAttribute failed for attribute {attr}: HRESULT {result}");
    }
    return result == 0;
}
```

**工作量**：低

---

### 5.3 HttpClient 优化（DiagnosticService + WebViewService）

**现状问题**

两个地方都有静态 HttpClient 问题：

```csharp
// DiagnosticService.cs
private static readonly HttpClient SharedHttpClient = CreateHttpClient();
// Timeout = 10s，没有 User-Agent，没有 SocketsHttpHandler 优化

// WebViewService.cs
private static readonly HttpClient HeartbeatHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
// 同样问题
```

**优化方案**

```csharp
private static readonly HttpClient SharedHttpClient = new(new SocketsHttpHandler
{
    ConnectTimeout = TimeSpan.FromSeconds(5),
    EnableMultipleHttp2Connections = false,
})
{
    Timeout = TimeSpan.FromSeconds(5),
    DefaultRequestHeaders =
    {
        UserAgent = { new ProductInfoHeaderValue("OpenClaw", "3.0.3") },
    },
};
```

两处都需要修改。

**工作量**：低

---

### 5.4 LoggingService Dispose 超时调整

**现状问题**

```csharp
_writerTask.Wait(TimeSpan.FromSeconds(2));
```

2 秒在日志量大时可能不够。

**优化方案**

```csharp
_writerTask.Wait(TimeSpan.FromSeconds(5));
```

或改为异步 flush 模式。

**工作量**：低

---

### 5.5 消除魔法数字

将散落在代码中的魔法数字提取为命名常量。仅 MainWindow 层就存在 **20+ 个魔法数字**：

**MainWindow.Initialization.cs**：

```csharp
timer.Interval = TimeSpan.FromMilliseconds(430);  // → RunIndicatorTickIntervalMs
timer.Interval = TimeSpan.FromMilliseconds(150);  // → WebViewRecreationDebounceMs
```

**MainWindow.xaml（仅列举部分）**：

```xml
<!-- 窗口标题栏 -->
RowDefinition Height="37"              <!-- → TitleBarHeight -->
Image Width="16" Height="16"           <!-- → TitleBarIconSize -->
TextBlock FontSize="13"                <!-- → TitleBarFontSize -->
StackPanel Spacing="8" Margin="12,0"   <!-- → TitleBarPadding, TitleBarSpacing -->

<!-- 命令栏 -->
ComboBox MinWidth="160"                <!-- → EnvSelectorMinWidth -->
TextBlock MaxWidth="400"               <!-- → UrlDisplayMaxWidth -->
Border MinWidth="320" MaxWidth="620"   <!-- → StatusBarMinWidth, StatusBarMaxWidth -->
Border CornerRadius="16"               <!-- → StatusBarCornerRadius -->
Border CornerRadius="14"               <!-- → LatencyBadgeCornerRadius -->

<!-- 状态指示器 -->
Ellipse Width="5" Height="5"           <!-- → IndicatorDotSize -->
ItemsRepeater Spacing="4"              <!-- → IndicatorDotSpacing -->
Grid MinHeight="24"                    <!-- → StatusLabelMinHeight -->

<!-- 加载环 -->
ProgressRing Width="48" Height="48"    <!-- → LoadingRingSize -->

<!-- 状态栏 -->
Ellipse Width="8" Height="8"           <!-- → StatusDotSize -->
TextBlock FontSize="12"                <!-- → StatusBarFontSize -->
```

**MainWindow.Theme.cs**：

```csharp
Color.FromArgb(255, 230, 240, 255)  // → ThemeSelectedBackground
Color.FromArgb(255, 37, 99, 235)    // → ThemeSelectedForeground
Color.FromArgb(0, 0, 0, 0)          // → ThemeUnselectedBackground (透明)
Color.FromArgb(255, 40, 40, 40)     // → DarkInactiveBackground
Color.FromArgb(255, 248, 248, 248)  // → LightInactiveBackground
AppIcon.Opacity = isWindowActive ? 1.0 : 0.72  // → InactiveIconOpacity
```

**建议统一常量定义区域**：

```csharp
// Constants/WindowConstants.cs
internal static class WindowConstants
{
    // 标题栏
    public const double TitleBarHeight = 37;
    public const double TitleBarIconSize = 16;
    public const double TitleBarFontSize = 13;
    public const double TitleBarPaddingLeft = 12;

    // 状态栏
    public const double StatusBarMinWidth = 320;
    public const double StatusBarMaxWidth = 620;
    public const double StatusBarCornerRadius = 16;
    public const double LatencyBadgeCornerRadius = 14;
    public const double IndicatorDotSize = 5;
    public const double IndicatorDotSpacing = 4;
    public const double LoadingRingSize = 48;
    public const double StatusDotSize = 8;
    public const double InactiveIconOpacity = 0.72;

    // 定时器
    public const double RunIndicatorTickIntervalMs = 430;
    public const double WebViewRecreationDebounceMs = 150;

    // 主题色
    public static readonly Color ThemeSelectedBackground = Color.FromArgb(255, 230, 240, 255);
    public static readonly Color ThemeSelectedForeground = Color.FromArgb(255, 37, 99, 235);
    public static readonly Color ThemeUnselectedBackground = Color.FromArgb(0, 0, 0, 0);
}
```

XAML 中可通过 `{x:Static}` 或 `StaticResource` 引入这些常量。

**工作量**：低

---

### 5.6 Navigation 自动重试潜在死循环

**现状问题**

`WebViewService.OnNavigationCompleted` 中，当连接失败时会自动重试：

```csharp
if (_retryCount < MaxRetries && !string.IsNullOrEmpty(_lastNavigatedUrl))
{
    _retryCount++;
    await Task.Delay(RetryDelay, token);
    coreWebView.Navigate(_lastNavigatedUrl);
}
```

但 `Navigate()` 方法内部会重置 `_retryCount = 0`：

```csharp
public void Navigate(string url)
{
    // ...
    _retryCount = 0;  // 每次新导航都重置计数器
    // ...
}
```

这意味着如果自动重试触发的 `Navigate` 再次失败，重试计数器已被重置，会再重试 3 次。理论上不会死循环（因为 `Navigate` 是在 `OnNavigationCompleted` 失败分支中调用的，而新导航的 `OnNavigationCompleted` 也会走失败分支），但重试逻辑的意图不够清晰。

**优化建议**

在自动重试时不调用 `Navigate()`，而是直接调用 `coreWebView.Navigate()` 绕过计数器重置：

```csharp
// 在 OnNavigationCompleted 的失败分支中
if (_retryCount < MaxRetries)
{
    _retryCount++;
    // 直接导航，不重置 _retryCount
    try { coreWebView.Navigate(_lastNavigatedUrl); }
    catch { /* ... */ }
    return;
}
```

**工作量**：低

---

### 5.7 ControlUiLatencyService.Stop 发布 Unknown 状态

**现状问题**

`ControlUiLatencyService.Stop()` 会清空内部状态并释放 timer/CTS，但不会向订阅方发布一次 `ControlUiLatencySnapshot.Unknown`：

```csharp
public void Stop()
{
    _currentUrl = null;
    _currentHost = null;
    _lastSuccessSnapshot = ControlUiLatencySnapshot.Unknown;
    _lastPublishedSnapshot = ControlUiLatencySnapshot.Unknown;
    // ...
}
```

`MainViewModel.RefreshResourceScheduling()` 在窗口隐藏、WebView 未初始化、环境为空时会调用 `_latencyService.Stop()`。由于 UI 没有收到 Unknown snapshot，顶部 latency badge 可能继续显示最后一次成功或 stale 的延迟值，造成“已经停止探测但 UI 仍像在线”的误导。

**优化方案**

方案 A：`Stop()` 在释放资源前或状态重置后发布 Unknown：

```csharp
public void Stop()
{
    // cancel/dispose...
    _currentUrl = null;
    _currentHost = null;
    _lastSuccessSnapshot = ControlUiLatencySnapshot.Unknown;
    PublishIfChanged(ControlUiLatencySnapshot.Unknown);
}
```

方案 B：保持 service 纯粹，由 `MainViewModel.RefreshResourceScheduling()` 在调用 `Stop()` 后显式重置：

```csharp
_latencyService.Stop();
LatencySummaryText = DefaultLatencySummary;
LatencySummaryBrush = NeutralBrush;
```

更推荐方案 A，因为所有调用方都能获得一致的“停止即 Unknown”语义。

**预期收益**
- 避免隐藏窗口、未初始化、切换环境时残留旧延迟值
- 让 latency badge 与真实探测状态一致
- 降低 UI 误导，特别是 Tunnel 抖动和后台恢复场景

**工作量**：极低

---

## 六、安全加固（中优先级）

### 6.1 配置敏感信息加密

**现状问题**

`settings.json` 明文存储于 `%LOCALAPPDATA%\OpenClaw\settings.json`。

**优化方案**

对敏感字段使用 Windows DPAPI 加密。

**工作量**：低

---

### 6.2 WebView2 脚本执行安全边界

**现状问题**

`ExecuteScriptAsync` 在多处被调用。

**优化建议**

在 `Navigate` 前验证 URL scheme，只允许 https 和 http loopback。

**工作量**：低

---

### 6.3 RunOnUiThread 松耦合 App.MainWindow

**现状问题**

`MainViewModel.Status.cs` 中：

```csharp
private static void RunOnUiThread(Action action)
{
    App.MainWindow?.DispatcherQueue.TryEnqueue(() => action());
}
```

通过 `App.MainWindow` 静态属性获取 DispatcherQueue，耦合紧密。如果未来有第二个窗口或测试环境，会失效。

**优化方案**

```csharp
// 方案 A：在 MainViewModel 构造时捕获 DispatcherQueue
private readonly DispatcherQueue _dispatcher;

public MainViewModel(...)
{
    _dispatcher = DispatcherQueue.GetForCurrentThread();
}

private void RunOnUiThread(Action action)
{
    _dispatcher?.TryEnqueue(() => action());
}

// 方案 B：使用 Microsoft.UI.Dispatching 的静态辅助方法
```

**工作量**：低

---

## 七、细节优化（低优先级）

### 7.1 版本号一致性

`app.manifest` 和 `Package.appxmanifest` 中版本是 `3.0.1.0`，而 `OpenClaw.csproj` 中是 `3.0.3.0`。此外，`AppMetadata.cs` 中仍保留硬编码 fallback：

```csharp
public const string CurrentVersion = "3.0.3";
```

这意味着版本号在 **4 个位置** 独立维护：
- `OpenClaw.csproj` — `<Version>3.0.3.0</Version>`
- `app.manifest` — `<assemblyIdentity version="3.0.1.0">`
- `Package.appxmanifest` — `<Identity Version="3.0.1.0">`
- `AppMetadata.cs` — `CurrentVersion = "3.0.3"`

任何一次版本发布都需要同步修改 4 处，极易遗漏，并可能导致 About 显示、安装包身份、程序集版本和 manifest 声明互相不一致。

**优化方案**：

在 csproj 中统一版本号，通过 MSBuild 自动同步：

```xml
<!-- OpenClaw.csproj -->
<PropertyGroup>
    <Version>3.0.3.0</Version>
    <AssemblyVersion>$(Version)</AssemblyVersion>
    <FileVersion>$(Version)</FileVersion>
</PropertyGroup>

<ItemGroup>
    <ApplicationManifest Include="app.manifest">
        <Version>$(Version)</Version>
    </ApplicationManifest>
</ItemGroup>
```

`AppMetadata.cs` 改为读取程序集版本：

```csharp
public static string GetDisplayVersion()
{
    var version = typeof(App).Assembly.GetName().Version;
    return version is not null
        ? $"{version.Major}.{version.Minor}.{version.Build}"
        : "unknown";
}
```

**工作量**：极低

---

### 7.2 StringResources 线程安全与启动验证

**现状问题**

```csharp
// StringResources.cs:20
private static ResourceLoader? _loader;

private static ResourceLoader? TryGetLoader()
{
    try
    {
        return _loader ??= new ResourceLoader();  // 线程不安全
    }
    catch
    {
        return null;
    }
}
```

`_loader ??= new ResourceLoader()` 在多线程环境下可能创建多个实例（虽然当前只在主线程调用，但如果未来在后台线程调用会有问题）。

此外，`Get()` 在资源未加载时 fallback 到 key name，导致启动失败静默。

**优化方案**

```csharp
private static readonly object _loaderLock = new();

private static ResourceLoader? TryGetLoader()
{
    if (_loader != null) return _loader;

    try
    {
        lock (_loaderLock)
        {
            return _loader ??= new ResourceLoader();
        }
    }
    catch
    {
        return null;
    }
}
```

并在 `App.OnLaunched` 中验证：

```csharp
StringResources.Initialize();
var testKey = StringResources.StatusConnected;
if (testKey == "StatusConnected")
{
    Logger.Warning("String resources not loaded - resource file may be missing");
}
```

**工作量**：极低

---

### 7.3 异常处理规范化

`LoggingService` 中有两处 `catch { }` 静默吞异常。建议至少使用 `Debug.WriteLine` 作为 fallback：

```csharp
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"[LoggingService] {ex.Message}");
}
```

**工作量**：低

---

### 7.4 HeartbeatIntervalSeconds 冗余字段

`AppSettings` 中同时存在 `HeartbeatIntervalSeconds` 和 `Heartbeat.IntervalSeconds`，`NormalizeSettings` 需要处理两者的同步。建议删除 `HeartbeatIntervalSeconds` 字段，仅保留 `Heartbeat.IntervalSeconds`，并在 `NormalizeSettings` 中做好旧版配置迁移。

**工作量**：低

---

### 7.5 ConfigurationService SaveDeferred 改用 PeriodicTimer

**现状问题**

```csharp
Task.Run(async () =>
{
    await Task.Delay(250).ConfigureAwait(false);
    Save();
});
```

使用 `Task.Run` + `Task.Delay` 实现防抖，不如 `PeriodicTimer` 优雅。

**优化方案**

```csharp
private PeriodicTimer? _saveTimer;

public void SaveDeferred()
{
    Interlocked.Increment(ref _deferredSaveRequests);
    if (Interlocked.Exchange(ref _saveQueued, 1) != 0)
    {
        Interlocked.Increment(ref _deferredSaveCoalescedRequests);
        return;
    }

    _saveTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
    _ = RunDeferredSaveAsync();
}

private async Task RunDeferredSaveAsync()
{
    try
    {
        await _saveTimer!.WaitForNextTickAsync();
        Save();
    }
    finally
    {
        Interlocked.Exchange(ref _saveQueued, 0);
        _saveTimer?.Dispose();
        _saveTimer = null;
    }
}
```

**工作量**：低

---

### 7.6 App.xaml.cs 冗余窗口追踪

**现状问题**

```csharp
// App.xaml.cs
private Window? _mainWindow;

public static Window? MainWindow { get; private set; }

// OnLaunched 中：
_mainWindow = new MainWindow();
MainWindow = _mainWindow;
```

`_mainWindow` 和 `MainWindow` 始终引用同一个对象，`_mainWindow` 字段是冗余的。

**优化方案**

```csharp
public static Window? MainWindow { get; private set; }

// OnLaunched 中：
MainWindow = new MainWindow();
```

**工作量**：极低

---

### 7.7 App.xaml.cs UnhandledException 处理过于薄弱

**现状问题**

```csharp
private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
{
    Logger.Error($"Unhandled exception: {e.Exception}");
}
```

仅记录日志，没有：
- 设置 `e.Handled = true` 阻止应用崩溃（即使需要崩溃，也应该先清理状态）
- 向用户展示友好的错误提示
- 包含用户可操作的恢复建议

**优化方案**

```csharp
private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
{
    Logger.Error($"Unhandled exception: {e.Exception}");

    // 根据异常类型决定是否阻止崩溃
    if (IsRecoverable(e.Exception))
    {
        e.Handled = true;
        ShowRecoveryDialog(e.Exception);
    }
}
```

**工作量**：低

---

## 八、实施路线图建议

### 第一阶段：快速优化（1-2 天）

| 编号 | 建议 | 工作量 | 风险 |
|------|------|--------|------|
| 1.3 | 全面使用 CommunityToolkit.Mvvm（RelayCommand + ObservableProperty） | 低 | 低 |
| 1.2 | WebView2 Environment 显式创建 | 低 | 低 |
| 2.2 | JS 字符串转义加固 | 低 | 低 |
| 3.1 | IsTunnelDetected 检测增强 | 低 | 极低 |
| 3.3 | Heartbeat Tunnel 协同 | 低 | 低 |
| 3.4 | 恢复策略 Tunnel 感知 | 低 | 低 |
| 4.1 | async void 改 async Task (全项目 8 处) | 低 | 低 |
| 4.2 | Stop 修复 async void | 低 | 低 |
| 5.2.1 | HeartbeatIndicatorViewModel SolidColorBrush 复用 | 低 | 极低 |
| 5.3 | HttpClient 优化 | 低 | 低 |
| 5.4 | LoggingService 超时 | 低 | 低 |
| 5.5 | 消除魔法数字 (MainWindow 20+ + WindowFrameHelper 12+) | 低 | 极低 |
| 5.7 | Latency Stop 发布 Unknown 状态 | 极低 | 极低 |
| 7.1 | 版本号一致性 (4 处统一) | 极低 | 极低 |
| 7.2 | StringResources 线程安全 + 验证 | 极低 | 极低 |
| 7.3 | 异常处理规范化 | 低 | 极低 |
| 7.6 | App 冗余字段清理 | 极低 | 极低 |
| 7.7 | UnhandledException 处理增强 | 低 | 低 |
| 5.1.6 | MainViewModel.Dispose() 调用 + WebViewService COM 释放 | 极低 | 极低 |

### 第二阶段：Tunnel 专项 + 架构重构（1-2 周）

| 编号 | 建议 | 工作量 | 风险 |
|------|------|--------|------|
| 3.5 | Heartbeat DNS TTL 冲突修复 | 中 | 中 |
| 3.2 | ICMP Ping Tunnel 场景优化 | 中 | 中 |
| 1.1 | 引入依赖注入 | 中 | 中 |
| 1.4 | MainViewModel 服务注入 | 中 | 中 |
| 2.1 | 提取 JS 脚本 | 中 | 中 |
| 5.1 | MainWindow WebView2 COM 释放 + 同步布局优化 + MainViewModel IDisposable | 中 | 中 |
| 5.6 | Navigation 重试逻辑修复 | 低 | 低 |

### 第三阶段：安全加固（3-5 天）

| 编号 | 建议 | 工作量 | 风险 |
|------|------|--------|------|
| 6.1 | 配置加密 | 低 | 低 |
| 6.2 | 脚本执行边界 | 低 | 低 |
| 6.3 | RunOnUiThread 松耦合 | 低 | 低 |
| 2.3 | SendCommand JSON 注入加固 | 低 | 低 |
| 7.4 | HeartbeatIntervalSeconds 冗余清理 | 低 | 低 |
| 7.5 | SaveDeferred 改用 PeriodicTimer | 低 | 低 |

---

## 附录：当前代码质量评分

| 维度 | 评分 | 说明 |
|------|------|------|
| 架构设计 | 8/10 | 分层清晰，partial class 拆分合理；但依赖注入缺失，静态服务定位器阻碍可测试性 |
| 代码规范 | 7/10 | 风格一致；但 async void 达 8 处、魔法数字 32+（MainWindow 20+、WindowFrameHelper 12+）、版本号 4 处独立维护、MainViewModel 手动 INotifyPropertyChanged 样板代码 200+ 行 |
| 错误处理 | 6/10 | 覆盖面广但部分过度吞异常，async void 存在崩溃风险，UnhandledException 仅记录日志无用户反馈 |
| 可测试性 | 6/10 | 静态依赖阻碍单元测试，服务直接 new 无法 mock；MainWindow 直接引用 App.Configuration/Logger |
| 安全性 | 7/10 | 基本到位，缺少加密和边界校验，JS 转义不完整 |
| 性能 | 6/10 | 异步设计良好；但 UpdateLayout 强制同步布局、App.Configuration.Save() 阻塞窗口关闭、HeartbeatIndicatorViewModel 频繁创建 SolidColorBrush（430ms 一次）、Tunnel 场景下 DNS TTL 冲突导致假性断连 |
| 可维护性 | 7/10 | 文件拆分合理；但 JS 内联影响维护，魔法数字分散，CommunityToolkit.Mvvm 已引入但未充分利用，3 处内存泄漏风险（rootElement 事件、SettingsDialog 状态、WebView2 COM 对象） |
| 资源管理 | 5/10 | MainViewModel.Dispose() 正确实现但从未被调用，WebView2 COM 对象释放依赖 GC，Latency Stop 未发布 Unknown 状态，HeartbeatIndicatorViewModel 频繁创建 SolidColorBrush |

**综合评分：6.0/10** — 代码质量处于中等水平。核心优势在于 ShellSessionCoordinator 的适配器模式和恢复状态机设计、分层清晰的架构思路。主要短板：（1）8 个 async void 实例带来的异常安全性风险；（2）CommunityToolkit.Mvvm 已引入但未充分利用，导致 200+ 行手动样板代码；（3）32+ 魔法数字分散在 MainWindow 和 WindowFrameHelper 中；（4）MainViewModel.Dispose() 实现正确但从未被调用，COM 资源和定时器可能泄漏；（5）HeartbeatIndicatorViewModel 频繁创建 SolidColorBrush 带来的 GC 压力；（6）Cloudflare Tunnel 场景下 DNS TTL 与静态 HttpClient 冲突导致的假性断连；（7）App.Configuration 和 App.Logger 在 ViewModel 层 12+ 处直接引用，耦合紧密；（8）版本号 4 处独立维护；（9）Latency Stop 未发布 Unknown 状态导致 UI 可能残留旧探测值；（10）StringResources 线程安全隐患和 UnhandledException 处理薄弱。
