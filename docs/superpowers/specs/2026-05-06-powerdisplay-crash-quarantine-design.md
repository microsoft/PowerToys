# PowerDisplay 崩溃隔离机制 —— 设计文档

**状态：** 草稿待评审
**分支：** `yuleng/pd/f/crash/1`
**作者：** yuleng@microsoft.com
**日期：** 2026-05-06
**关联 issue：** [microsoft/PowerToys#47556](https://github.com/microsoft/PowerToys/issues/47556)

## 1. 背景

GitHub issue #47556 报告了一个 `KERNEL_SECURITY_CHECK_FAILURE (0x139, 子码 0x2)` 蓝屏，崩溃栈源头在 `win32kfull!CPhysicalMonitorHandle::DdcciGetCapabilitiesStringFromMonitor`。在 Windows 11 build 26200.8328、PowerToys 0.99.1.0、外接特定 LG 显示器（LG 27MR400，EDID 已损坏）的环境下可稳定复现。第二位用户报告了类似的次生症状——没有蓝屏，但 LG 固件的显示渲染流水线在睡眠/恢复后被楔死，发生在另一型 LG 显示器上。

两份报告都指向同一条内核侧路径：PowerDisplay 调用 `Dxva2.dll` 的 `GetCapabilitiesStringLength` / `CapabilitiesRequestAndCapabilitiesReply`，作用于一台返回非规范 DDC/CI capability 字符串的显示器，内核函数在解析过程中破坏了自己的栈。

**根因在 Windows 内核（`win32kfull`），必须由 Windows 团队修复。** PowerToys 没法 patch 内核。这份设计是 PowerToys 一侧的**缓解措施**：**检测到 PowerDisplay 上次运行造成了崩溃，就拒绝再次执行同样的危险操作，直到用户明确表示愿意承担风险**。

缓解目标：**任何遭遇过一次蓝屏的用户，不应该被同一颗雷自动炸第二次**。第一次崩溃后，PowerDisplay 自我禁用、Settings UI 顶部显示红色 InfoBar 警告、整个 PowerDisplay 设置页面被锁定。用户必须主动点击 "Ignore" 才能解除锁定，然后手动翻开关重新启用。如果触发崩溃的显示器仍然连着，重启用会再次蓝屏——但那是用户在知情下做出的选择。

## 2. 范围

### 包含（方案 A，brainstorming 阶段已确认）

* **仅 capability 获取路径**受本机制保护：
  * `Dxva2.dll!GetCapabilitiesStringLength`（[PInvoke.cs:107-109](src/modules/powerdisplay/PowerDisplay.Lib/Drivers/PInvoke.cs#L107-L109)）
  * `Dxva2.dll!CapabilitiesRequestAndCapabilitiesReply`（[PInvoke.cs:111-116](src/modules/powerdisplay/PowerDisplay.Lib/Drivers/PInvoke.cs#L111-L116)）
* 这两个 API 仅在 `DdcCiNative.FetchCapabilities`（[DdcCiNative.cs:27-82](src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiNative.cs#L27-L82)）和 `DdcCiController.GetCapabilitiesStringAsync`（[DdcCiController.cs:179-235](src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs#L179-L235)）中调用。受保护的窗口是 `DdcCiController.FetchCapabilitiesInParallelAsync` 的并行 fetch 阶段（[DdcCiController.cs:400-412](src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs#L400-L412)）。

### 不包含

* **VCP get/set**（`SetVCPFeature`、`GetVCPFeatureAndVCPFeatureReply`）——这些是热路径调用（亮度滑条每秒触发数十次 set）。在这里加崩溃检测意味着每次 VCP 调用都要同步落盘 + flush，性能不可接受。已报告的蓝屏路径也不在这里。决策依据：brainstorming 第一轮——分析了 L3 flush 在滑条热路径上的成本后，舍方案 B 选方案 A。
* **按显示器粒度的隔离**。我们不尝试识别"是哪一台显示器导致的崩溃"。Phase 2 是并行 fetch，多个 BEGIN 标记同时在飞，蓝屏发生时无法可靠归因到单台显示器。所以我们禁用整个 PowerDisplay 模块。决策依据：brainstorming 第二轮。
* **崩溃事件的遥测**。v1 不加 telemetry。
* **InfoBar 列出可疑显示器**。InfoBar 文案是通用的（"检测到崩溃，已自动禁用"），不展示具体设备。决策依据：用户在 brainstorming 中明确要求。
* **内核侧修复**——那需要 Windows 内核团队出 update。

### 非目标

* 防住所有 PowerDisplay 相关的崩溃。我们只覆盖文档明确的蓝屏路径。PowerDisplay.exe 还可能因为别的原因崩（例如 C# 代码自身 bug），那些不在本设计范围内。
* 自动恢复。用户点 Ignore + 重新启用后，如果坏显示器还连着，系统会再蓝。我们不打算在这里做任何"聪明"的事——用户已经被警告过了。

## 3. 架构

### 3.1 磁盘文件

两个文件都放在 `%LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\`（即 `PathConstants.PowerDisplayFolderPath`）。

| 文件 | 生命周期 | 用途 |
|---|---|---|
| `discovery.lock` | Phase 2 进入前写入；Phase 2 完成或非崩溃式退出时删除。**只有进程被外力强杀（蓝屏、TerminateProcess、FailFast）时才会残留**。 | "我们正在执行危险代码"的标志。下次启动时若发现它，意味着上次崩溃了。**纯内部机制，用户看不到**。 |
| `crash_detected.flag` | PowerDisplay.exe Phase 0 检测到孤儿 `discovery.lock` 时写入；用户在 Settings UI 点 Ignore 时删除。 | UI 信号。Settings UI 读这个文件来决定是否显示红色 InfoBar 并锁定页面。 |

两个文件都是带 `version` 字段的 JSON，便于将来格式演进。当前 `version=1`。Phase 0 读取时如果遇到不认识的 version，**保守处理为"格式异常 ⇒ 当作孤儿"**。

```jsonc
// discovery.lock
{
  "version": 1,
  "pid": 12345,
  "startedAt": "2026-05-06T10:00:00Z"
}

// crash_detected.flag
{
  "version": 1,
  "detectedAt": "2026-05-06T10:01:23Z"
}
```

`pid` 和时间戳字段**仅用于诊断**（写到 log 里方便用户提 issue 时贴出），**不参与决策逻辑**。检测规则就是"lock 文件是否存在"这一个 bool。

### 3.2 组件总览

```
┌──────────────────────────────────────────────────────────────┐
│  runner.exe 进程                                             │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  PowerDisplayModuleInterface.dll（被 runner 加载）   │   │
│  │                                                       │   │
│  │  • [新增] AutoDisable listener 线程，wait              │   │
│  │    POWER_DISPLAY_AUTO_DISABLE_EVENT                   │   │
│  │  • 现有的 toggle/refresh listener 不动                │   │
│  └──────┬───────────────────────────────────────────────┘   │
│         │ 启动子进程（已有逻辑）                              │
└─────────┼───────────────────────────────────────────────────┘
          │
          ▼
┌──────────────────────────────────────────────────────────────┐
│  PowerDisplay.exe（C# WinUI 应用）                           │
│                                                              │
│  启动时:                                                      │
│    [新增] CrashRecovery.DetectOrphanAndDisable()             │
│      → 若孤儿 lock：写 flag、写 settings.json、              │
│        SignalEvent、删 lock、Exit(0)                          │
│                                                              │
│  发现监视器时:                                                │
│    DdcCiController.DiscoverMonitorsAsync()                   │
│      Phase 1（GDI 枚举）       — 安全                         │
│      [新增] using (CrashDetectionScope.Begin())               │
│        Phase 2（FetchCapabilitiesInParallelAsync）— 危险      │
│      Phase 3（CreateValidMonitors）— 安全                     │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│  PowerToys Settings UI 进程（独立进程，按需启动）             │
│                                                              │
│  PowerDisplayViewModel:                                       │
│    [新增] 构造时读 crash_detected.flag                        │
│    [新增] IsCrashLockActive 属性驱动:                         │
│      - 顶部 Error InfoBar 是否显示                            │
│      - 整页面的 IsEnabled 绑定（页面锁定）                    │
│    [新增] DismissCrashWarning 命令删除 flag                   │
└──────────────────────────────────────────────────────────────┘
```

### 3.3 五个组件清单

1. **DdcCiController（C#）** —— 用 `using (CrashDetectionScope.Begin())` 包住 Phase 2 调用。Phase 3 在 scope 外。
2. **PowerDisplay.exe Phase 0（C#）** —— 应用启动时、任何 DDC/CI 活动之前调用 `CrashRecovery.DetectOrphanAndDisable()`。返回 true 则立即退出。
3. **CommonSharedConstants（C++）** —— 新增 `POWER_DISPLAY_AUTO_DISABLE_EVENT` 常量。
4. **PowerDisplayModuleInterface DLL（C++）** —— 新增一条 listener 线程 wait 在新事件上；收到信号调 `this->disable()`，把 runner 内存状态校准到磁盘上"已禁用"的事实。
5. **PowerDisplayViewModel + PowerDisplayPage XAML（C#/XAML）** —— 新增 `IsCrashLockActive` 属性、顶部 Error InfoBar、整页 IsEnabled 锁定、Ignore 按钮。

## 4. 详细设计

### 4.1 `CrashDetectionScope`（新建类）

**位置：** `src/modules/powerdisplay/PowerDisplay.Lib/Services/CrashDetectionScope.cs`

**职责：** 管理 `discovery.lock` 在 Phase 2 期间的生命周期。**单一职责**——不做检测，不写其他文件。

**API：**

```csharp
public sealed class CrashDetectionScope : IDisposable
{
    public static CrashDetectionScope Begin();
    public void Dispose();
}
```

**实现契约：**

* `Begin()` 用 `FileMode.CreateNew`（如果文件已存在直接抛——防御性）、`FileOptions.WriteThrough`、且写完显式 `Flush(flushToDisk: true)` 写入 `discovery.lock`。文件系统失败或 lock 已存在时抛 `IOException`。
* `Begin()` 写入的 JSON 内容包含 `version`、`pid`、`startedAt`。**内容仅用于诊断**；逻辑只关心文件是否存在。
* `Dispose()` 删除 `discovery.lock`。删除失败时 log warning 但**不重抛**——失败的最坏后果是下次启动一次性误判为崩溃，用户点 Ignore 即可恢复。
* `Dispose()` 是幂等的（用 `_disposed` 字段守卫）。

**调用方模式：**

```csharp
// DdcCiController.DiscoverMonitorsAsync 中
(CandidateMonitor, DdcCiValidationResult)[] fetchResults;
using (CrashDetectionScope.Begin())
{
    fetchResults = await FetchCapabilitiesInParallelAsync(candidates, cancellationToken);
}
return CreateValidMonitors(fetchResults);
```

用 `using (...)` 块（**而不是** `using var`）确保 lock 在 Phase 2 完成的**那一刻**释放，而不是在方法 return 时。Phase 3（`CreateValidMonitors`）在 scope 外执行——它内部的 DDC/CI 调用（VCP get 初始化输入源、色温等）走的是不同的内核函数，不在我们这次防御范围内，符合方案 A 的语义。

**为什么用 `FileMode.CreateNew` 而不是 `Create`：** 如果 Phase 2 入口处发现 `discovery.lock` 已存在，意味着要么 (a) Phase 0 检测没清理干净，要么 (b) 两个 PowerDisplay.exe 实例并发跑了。两种都是 bug。失败比静默覆盖好。

### 4.2 `CrashRecovery`（新建静态类）

**位置：** `src/modules/powerdisplay/PowerDisplay.Lib/Services/CrashRecovery.cs`

**职责：** 一次性的 Phase 0 检测。和 `CrashDetectionScope` 互相独立。

**API：**

```csharp
public static class CrashRecovery
{
    /// <summary>
    /// 在 PowerDisplay.exe 启动时、任何 DDC/CI 活动之前调用。
    /// 若检测到孤儿 discovery.lock，执行严格的 auto-disable 序列
    ///（写 flag、写 settings.json、SignalEvent、删 lock）。
    /// </summary>
    /// <returns>true 表示检测到孤儿、调用方应立即退出进程。</returns>
    /// <exception>序列任何一步失败即抛出（严格 fail-fast）。</exception>
    public static bool DetectOrphanAndDisable();
}
```

**严格 fail-fast 序列（必须严格按此顺序执行）：**

1. **写 `crash_detected.flag`**（UI 信号）
2. **写全局 `settings.json`**，把 `enabled.PowerDisplay = false`（持久化禁用）
3. **Signal `POWER_DISPLAY_AUTO_DISABLE_EVENT`**（runner 内存状态校准）
4. **删除 `discovery.lock`**（commit point）

如果第 1～3 步任何一步抛异常，**lock 不会被删除**。异常向上传播；`Program.Main` 应该 `Environment.Exit(1)`。下次启动时检测会再次发现 lock 并重新跑完整序列——通过"lock 即 commit point"模式实现自愈。

**步骤顺序的理由：**

| 步骤 | 失败后果（如果后续步骤未执行） | 重试时的自愈结果 |
|---|---|---|
| 1 失败 | 当前会话没 UI banner；lock 仍在 | 重试；完整恢复 |
| 2 失败 | settings.json 还是 enabled；下次开机 runner 仍会启动 PowerDisplay；lock 仍在 | 重试；最终成功 |
| 3 失败 | 当前 runner 会话未同步；用户看到 toggle OFF + InfoBar；翻 toggle 需要 OFF→ON 一次（因为 runner m_enabled 还是 true）。但 settings.json 已写，下次 runner 重启状态一致 | 下次启动重试；状态会更干净 |
| 4 必须最后 | 如果 4 在 1～3 之前跑了，会丢失"上次崩了"的证据，下次启动 Phase 1/2 正常进入，可能再蓝一次 | N/A —— 这步是最终 commit |

**删除 `discovery.lock` 是 commit point**：在 lock 被删除之前，整个序列处于"未提交"状态；任何失败都让系统处于可恢复状态。

**不做部分容错。** 按设计决策，单步失败**不**被吞掉。如果我们没法可靠地写 flag 或 settings.json，就没法可靠地声称"PowerDisplay 已禁用"——留下证据等待下次重试，比假装成功好。

### 4.3 `CommonSharedConstants` 新增（C++）

**位置：** `src/common/interop/shared_constants.h`

新增一个 `constexpr` 字符串常量：

```cpp
// 命名遵循已有约定（见 TOGGLE_POWER_DISPLAY_EVENT、REFRESH_POWER_DISPLAY_MONITORS_EVENT）。
inline constexpr wchar_t POWER_DISPLAY_AUTO_DISABLE_EVENT[] =
    L"Local\\PowerToysPowerDisplay-AutoDisable-Event-{在落地阶段填入新生成的 UUID}";
```

UUID 后缀在落地阶段由实现者生成（参考 [PathConstants.cs:84](src/modules/powerdisplay/PowerDisplay.Lib/PathConstants.cs#L84) 中 `LIGHT_SWITCH_LIGHT_THEME_EVENT` 等的 UUID 用法）。

同一常量需要在 C# 侧暴露——加到 `PathConstants.cs`（或新建一个 `EventConstants.cs`），让 `CrashRecovery` 能在 PowerDisplay.exe 中 SignalEvent。

### 4.4 PowerDisplayModuleInterface DLL 改动（C++）

**位置：** `src/modules/powerdisplay/PowerDisplayModuleInterface/dllmain.cpp`

仿照已有的 `m_hToggleEvent` / `m_toggleEventThread` 模式（[dllmain.cpp:75-80, 127-170](src/modules/powerdisplay/PowerDisplayModuleInterface/dllmain.cpp#L75-L80)），新增事件 handle 和 listener 线程：

```cpp
// PowerDisplayModule 类内：
HANDLE m_hAutoDisableEvent = nullptr;
std::thread m_autoDisableEventThread;

// 构造函数中（和现有 event create 并列）:
m_hAutoDisableEvent = CreateDefaultEvent(CommonSharedConstants::POWER_DISPLAY_AUTO_DISABLE_EVENT);

// 新增 listener 线程，由 enable() 启动、disable() 停止:
void StartAutoDisableEventListener();
void StopAutoDisableEventListener();
```

listener 线程主体：

```cpp
m_autoDisableEventThread = std::thread([this]() {
    HANDLE handles[] = { m_hAutoDisableEvent, m_hStopEvent };
    while (true) {
        DWORD result = WaitForMultipleObjects(2, handles, FALSE, INFINITE);
        if (result == WAIT_OBJECT_0) {
            Logger::warn(L"PowerDisplay AutoDisable event received — disabling module");
            // 调本模块自己的 disable() —— 这会把 m_enabled 设为 false
            //（runner 通过 is_enabled() 查这个值），并 stop process manager
            //（PowerDisplay.exe 已自杀，stop 是 no-op）
            this->disable();
            // 注意: 这里不写 settings.json。PowerDisplay.exe Phase 0 已经写过了。
            break;  // 一次性 —— listener 退出；模块如被重新启用，listener 会重新创建
        }
        else {
            break;  // stop event 信号
        }
    }
});
```

**为什么是一次性的：** listener 收到事件并调用 `disable()` 之后，任务已完成。如果用户后续重新启用 PowerDisplay（调 `enable()`），可以重新启动一条新的 listener。这避免了"模块已经 disabled 时再收到 AutoDisable 事件该怎么办"的歧义。

**生命周期：**

* `enable()` —— 事件 handle 已在构造函数创建；这里启动 listener 线程。
* `disable()` —— 已有 `StopToggleEventListener` 调用，我们并列加 `StopAutoDisableEventListener`。
* 构造函数 / 析构函数管理事件 handle 的生命周期。

### 4.5 PowerDisplay.exe Phase 0 接入（C#）

**位置：** `PowerDisplay.exe` 启动入口。具体可能在 `App.xaml.cs::OnLaunched`，也可能是一个在任何窗口构造之前调用的 bootstrap 方法。具体位置在 plan 阶段确认。

```csharp
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    // Phase 0: 崩溃恢复检测。必须在任何 DDC/CI 初始化之前。
    try
    {
        if (CrashRecovery.DetectOrphanAndDisable())
        {
            Logger.LogWarning("Phase 0: orphan discovery.lock detected; auto-disable sequence executed; exiting.");
            Environment.Exit(0);
        }
    }
    catch (Exception ex)
    {
        Logger.LogError($"Phase 0: auto-disable sequence failed: {ex}");
        // lock 没有被删除；下次启动会重试整个序列。
        Environment.Exit(1);
    }

    // ... 现有 OnLaunched 逻辑 ...
}
```

### 4.6 PowerDisplay.Lib 改动 —— DdcCiController 集成

**位置：** [DdcCiController.cs:264-298](src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs#L264-L298)

修改 `DiscoverMonitorsAsync`，把 Phase 2 包进 scope：

```csharp
public async Task<IEnumerable<Monitor>> DiscoverMonitorsAsync(CancellationToken cancellationToken = default)
{
    try
    {
        var allMonitorDisplayInfo = DdcCiNative.GetAllMonitorDisplayInfo();

        var monitorHandles = EnumerateMonitorHandles();
        if (monitorHandles.Count == 0) return Enumerable.Empty<Monitor>();

        var candidates = await CollectCandidateMonitorsAsync(
            monitorHandles, allMonitorDisplayInfo, cancellationToken);
        if (candidates.Count == 0) return Enumerable.Empty<Monitor>();

        // Phase 2: 严格被崩溃检测 scope 包住
        (CandidateMonitor Candidate, DdcCiValidationResult Result)[] fetchResults;
        using (CrashDetectionScope.Begin())
        {
            fetchResults = await FetchCapabilitiesInParallelAsync(candidates, cancellationToken);
        }

        // Phase 3: 在 scope 外
        return CreateValidMonitors(fetchResults);
    }
    catch (Exception ex)
    {
        Logger.LogError($"DDC: DiscoverMonitorsAsync exception: {ex.Message}\nStack: {ex.StackTrace}");
        return Enumerable.Empty<Monitor>();
    }
}
```

`using` 块的边界是精确的——Phase 1 在 `Begin()` 之前、Phase 3 在 `Dispose()` 之后。这正符合方案 A。

### 4.7 PathConstants 新增

**位置：** [PathConstants.cs](src/modules/powerdisplay/PowerDisplay.Lib/PathConstants.cs)

新增三个路径/事件访问器：

```csharp
public const string DiscoveryLockFileName = "discovery.lock";
public const string CrashDetectedFlagFileName = "crash_detected.flag";

public static string DiscoveryLockPath
    => Path.Combine(PowerDisplayFolderPath, DiscoveryLockFileName);

public static string CrashDetectedFlagPath
    => Path.Combine(PowerDisplayFolderPath, CrashDetectedFlagFileName);

public const string AutoDisableEventName =
    "Local\\PowerToysPowerDisplay-AutoDisable-Event-{对齐 shared_constants.h 中的 UUID}";
```

`AutoDisableEventName` 的字符串值必须**完全等于** `shared_constants.h` 里的 `POWER_DISPLAY_AUTO_DISABLE_EVENT`。我们沿用现有约定（C++ 和 C# 双方各自维护字符串常量，靠人工对齐）——和 `LightSwitchLightThemeEventName` 等的做法一致。

### 4.8 Settings UI —— PowerDisplayViewModel（C#）

**位置：** [PowerDisplayViewModel.cs](src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs)

新增：

```csharp
private bool _isCrashLockActive;
public bool IsCrashLockActive
{
    get => _isCrashLockActive;
    private set
    {
        if (_isCrashLockActive != value)
        {
            _isCrashLockActive = value;
            OnPropertyChanged(nameof(IsCrashLockActive));
        }
    }
}

public ButtonClickCommand DismissCrashWarningCommand => new ButtonClickCommand(DismissCrashWarning);

private void DismissCrashWarning()
{
    try
    {
        var path = /* CrashDetectedFlagPath —— 见 4.10 节 */;
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
    catch (Exception ex)
    {
        Logger.LogError($"DismissCrashWarning: failed to delete flag: {ex.Message}");
    }
    IsCrashLockActive = false;
}
```

构造函数中，在现有初始化之后：

```csharp
IsCrashLockActive = File.Exists(/* CrashDetectedFlagPath */);
```

`IsEnabled` 的 setter **不动**——当 `IsCrashLockActive` 为 true 时，整个页面（包括 toggle）由 XAML 绑定层强制 disabled，用户必须先点 Ignore。

### 4.9 Settings UI —— PowerDisplayPage XAML

**位置：** [PowerDisplayPage.xaml](src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml)

两处修改：

**修改 1：** 在 `ModuleContent` 根 StackPanel 顶部加 InfoBar（在现有 GPOInfoControl 上方）：

```xml
<StackPanel ChildrenTransitions="{StaticResource SettingsCardsAnimations}">
    <!-- 新增：崩溃恢复 banner -->
    <InfoBar
        x:Uid="PowerDisplay_CrashDetectedInfoBar"
        IsClosable="False"
        IsOpen="{x:Bind ViewModel.IsCrashLockActive, Mode=OneWay}"
        Severity="Error">
        <InfoBar.ActionButton>
            <Button
                x:Uid="PowerDisplay_CrashDetected_IgnoreButton"
                Command="{x:Bind ViewModel.DismissCrashWarningCommand}" />
        </InfoBar.ActionButton>
    </InfoBar>

    <!-- 现有内容，但包一层 wrapper 让其在锁定时变 disabled -->
    <StackPanel
        IsEnabled="{x:Bind ViewModel.IsCrashLockActive, Mode=OneWay, Converter={StaticResource BoolNegationConverter}}">
        <controls:GPOInfoControl ...>
            ...
        </controls:GPOInfoControl>
        <!-- 所有现有的 SettingsGroup 都放在这个内层 StackPanel 内 -->
    </StackPanel>
</StackPanel>
```

**修改 2：** 在 `Settings.UI/Strings/en-us/Resources.resw` 加本地化资源：

| 资源 key | 英文文案 |
|---|---|
| `PowerDisplay_CrashDetectedInfoBar.Title` | `PowerDisplay was automatically disabled` |
| `PowerDisplay_CrashDetectedInfoBar.Message` | `A system crash was detected during the previous PowerDisplay session. PowerDisplay has been disabled to prevent another crash. If you understand the risk, click Ignore to dismiss this warning, then re-enable PowerDisplay manually.` |
| `PowerDisplay_CrashDetected_IgnoreButton.Content` | `Ignore` |

**为什么 InfoBar 在内层 disabled StackPanel 之外：** InfoBar 本身（特别是 Ignore 按钮）必须在页面其他部分被锁定时仍可交互。把 InfoBar 放在被 IsEnabled 绑定锁住的容器之外，自然达成这个效果。

### 4.10 PowerDisplay.Lib 和 Settings.UI 之间的路径共享

**问题：** Settings UI 的 `PowerDisplayViewModel` 在 `src/settings-ui/Settings.UI/`，默认**不**引用 `PowerDisplay.Lib`。`crash_detected.flag` 的路径定义在 `PowerDisplay.Lib/PathConstants.cs`。

**方案：** 在 `PowerDisplay.Models`（Settings UI 已经引用，见 [PowerDisplayPage.xaml:10](src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml#L10)）里加一个轻量的 `PowerDisplayPaths` 静态类，**只暴露 UI 需要的那部分路径推导逻辑**：

```csharp
// PowerDisplay.Models/PowerDisplayPaths.cs
public static class PowerDisplayPaths
{
    public static string CrashDetectedFlagPath
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "PowerToys", "PowerDisplay", "crash_detected.flag");
}
```

`PowerDisplay.Lib` 中的 `PathConstants` 继续作为运行时的 single source of truth，可以引用 `PowerDisplayPaths`、也可以重复字面量。**plan 阶段确认采用其中一种、保持单一来源**——绝不能让两边各自维护、漂移。

## 5. 端到端流程

### 5.1 Happy path（无崩溃）

1. PowerDisplay.exe 启动。
2. Phase 0：`CrashRecovery.DetectOrphanAndDisable()` 检查 `discovery.lock` → 不存在 → 返回 false。
3. 正常启动流程继续。
4. `DdcCiController.DiscoverMonitorsAsync` 运行：
   * Phase 1：GDI 枚举。
   * `CrashDetectionScope.Begin()` 写入 `discovery.lock`。
   * Phase 2：`FetchCapabilitiesInParallelAsync` 运行。
   * 正常完成或抛异常时，`Dispose()` 删除 `discovery.lock`。
   * Phase 3：`CreateValidMonitors`（lock 已不存在）。
5. PowerDisplay 正常运行。

### 5.2 蓝屏路径

1. PowerDisplay.exe 启动；Phase 0 没找到 lock；正常启动。
2. `DdcCiController.DiscoverMonitorsAsync`：Phase 1 → `CrashDetectionScope.Begin()` 用 `WriteThrough+Flush(true)` 写 lock → Phase 2 开始 → 在 `win32kfull!DdcciGetCapabilitiesStringFromMonitor` 内部蓝屏。
3. 系统硬重启。
4. Windows 启动；runner 读全局 `settings.json`（仍是 `enabled.PowerDisplay=true`）→ 启用 PowerDisplay 模块 → 启动 PowerDisplay.exe。
5. PowerDisplay.exe 启动；Phase 0 发现步骤 2 留下的**孤儿** `discovery.lock`。
6. `CrashRecovery.DetectOrphanAndDisable()` 跑严格序列：
   * 写 `crash_detected.flag`。
   * 写全局 `settings.json`，`enabled.PowerDisplay=false`。
   * Signal `POWER_DISPLAY_AUTO_DISABLE_EVENT` → runner 内的 listener（在 module DLL 里的线程）醒来 → 调 `this->disable()` → `m_enabled=false`、`m_processManager.stop()`（PowerDisplay.exe 反正马上要退出）。
   * 删除 `discovery.lock`（commit point）。
   * 返回 true。
7. PowerDisplay.exe `Environment.Exit(0)`。
8. 此时：磁盘 `enabled.PowerDisplay=false`、runner `m_enabled=false`、`crash_detected.flag` 存在。状态自洽。

### 5.3 用户打开 Settings UI

9. 用户打开 Settings UI（独立进程；重新读 `settings.json`）。
10. 导航到 PowerDisplayPage → 构造 PowerDisplayViewModel。
11. ViewModel 读 `crash_detected.flag` → `IsCrashLockActive = true`。
12. ViewModel 读 `_isEnabled = GeneralSettingsConfig.Enabled.PowerDisplay` → false。
13. UI 渲染：
    * 顶部 InfoBar（Severity=Error）显示，带 "Ignore" 按钮。
    * **InfoBar 下方整个页面 disabled**（绑定到 `!IsCrashLockActive`）。toggle、监视器列表、profiles、自定义 VCP 映射——全部灰着、不可点。
    * **唯一可交互的就是 Ignore 按钮**。

### 5.4 用户 dismiss 警告并重新启用

14. 用户点 Ignore → `DismissCrashWarningCommand` → flag 被删、`IsCrashLockActive=false`。
15. 页面变可交互。toggle 仍 OFF（settings.json 是 false；runner 也认为是 false——步骤 6 已同步）。
16. 用户翻 toggle 到 ON → 走现有 setter 逻辑 → IPC 给 runner `enabled.PowerDisplay=true` → runner 看 target=true、`module->is_enabled()=false`（步骤 6 已设）→ 不相等 → 调 `enable()` → PowerDisplay.exe 启动。
17. PowerDisplay.exe Phase 0（现在 lock 不在、flag 也不在）→ 正常启动 → discovery → 如果坏显示器仍连着，再次蓝屏 → 回到步骤 2 循环。

### 5.5 边角场景：PowerDisplay.exe 自身崩（非蓝屏）

如果 PowerDisplay.exe 进程在 Phase 2 期间因为别的原因（C# 未捕获异常等）崩溃：

* lock 文件留在磁盘上（没机会跑 Dispose）。
* module DLL 的 `m_processManager` **不会**自动重启子进程；runner 认为 PowerDisplay 还 enabled，但进程已经没了。
* 下次有人调 `m_processManager.send_message`（比如 Quick Access toggle）时，会检测到 `!is_process_running()` → 调 `refresh()` → 重新启动 PowerDisplay.exe → Phase 0 检测到孤儿 → 跑 auto-disable 序列。
* 用户视角：和蓝屏路径一样，只是触发时机晚一些（直到有人想和 PowerDisplay 交互的那一刻）。

## 6. 失败模式与恢复矩阵

| 场景 | 磁盘上残留 | 下次启动行为 | 用户需要做什么 |
|---|---|---|---|
| Phase 2 蓝屏 | 仅 `discovery.lock` | Phase 0 检测 → 完整序列 → `crash_detected.flag` + `settings.json=false` | 打开 Settings、点 Ignore、手动翻 toggle 重新启用 |
| Phase 2 进程崩（非蓝屏） | 仅 `discovery.lock` | 同蓝屏场景，只是触发晚（下次 spawn 时） | 同上 |
| Phase 0 步骤 1 失败（无法写 flag） | 仅 `discovery.lock` | 重试完整序列 | 起初无；如果是持久性磁盘问题，用户需自己解决 |
| Phase 0 步骤 2 失败（无法写 settings.json） | `discovery.lock` + `crash_detected.flag` | 重试完整序列；flag 写入是幂等的 | 同上 |
| Phase 0 步骤 3 失败（无法 Signal event） | `discovery.lock` + `crash_detected.flag` + `settings.json=false` | 重试：1、2 步幂等通过，3 步重试，依此类推 | 同上 |
| Phase 0 步骤 4 失败（无法删 lock） | 四个文件状态都对，但 lock 仍在 | 重试完整序列——重复写入是幂等的 | 同上；lock 最终会被删除 |
| 用户点 Ignore 但没重启用 | `crash_detected.flag` 已删、`settings.json=false` | 模块跨重启保持禁用 | 无 |
| 用户 Ignore 后翻 toggle ON，坏显示器仍连着 | 正常启动 → 再次蓝屏 | 回到蓝屏路径 | 用户必须断开坏显示器 |

## 7. 测试策略

### 7.1 单元测试

**项目：** `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/`

* `CrashDetectionScopeTests`：
  * `Begin_WritesLockFile`：验证文件创建在预期路径，含正确的 version/pid/timestamp。
  * `Begin_FailsIfLockAlreadyExists`：确保 `FileMode.CreateNew` 正确强制唯一性。
  * `Dispose_DeletesLockFile`：标准 happy path。
  * `Dispose_IsIdempotent`：调用 Dispose 两次不抛。
  * `Dispose_DoesNotThrowOnDeleteFailure`：模拟 lock 被另一进程持有 → Dispose log 但不抛。
* `CrashRecoveryTests`：
  * `DetectOrphanAndDisable_ReturnsFalseWhenNoLock`。
  * `DetectOrphanAndDisable_RunsFullSequenceWhenLockPresent`：验证 flag 写、settings 写、event signal、删 lock 按正确顺序执行。
  * `DetectOrphanAndDisable_LeavesLockIntactIfFlagWriteFails`：模拟步骤 1 IO 失败 → 确保 lock 仍在、异常向上传播。
  * 步骤 2、3 失败的同类测试。
  * `DetectOrphanAndDisable_HandlesUnknownVersionAsOrphan`：向前兼容行为。

测试通过抽象的文件系统 / 事件 seam（如 `IFileSystem`、`IEventSignaler`）注入失败。

### 7.2 集成 / 手工 QA

**Debug-only 崩溃注入**，加在 `DdcCiController.FetchCapabilitiesInParallelAsync`：

```csharp
#if DEBUG
if (Environment.GetEnvironmentVariable("POWERDISPLAY_SIMULATE_CRASH") == "1")
{
    Environment.FailFast("Simulated crash for quarantine testing");
}
#endif
```

QA 流程：

1. `set POWERDISPLAY_SIMULATE_CRASH=1`，启动 PowerDisplay.exe。
2. PowerDisplay 进 Phase 2 → FailFast → 进程硬死。
3. 验证磁盘上 `discovery.lock` 存在。
4. `set POWERDISPLAY_SIMULATE_CRASH=`（清掉），重新启动 PowerDisplay.exe。
5. 验证 Phase 0 检测到孤儿：`crash_detected.flag` 出现、`settings.json` 显示 `enabled.PowerDisplay=false`、`discovery.lock` 已删、PowerDisplay.exe 以 0 退出。
6. 打开 Settings UI → PowerDisplayPage → 验证 Error InfoBar 显示、页面 disabled、Ignore 按钮可点。
7. 点 Ignore → 页面变可交互、toggle 仍 OFF。
8. 翻 toggle 到 ON → PowerDisplay.exe 正常启动并跑（这次不会崩，env var 已清）。

### 7.3 跨组件 IPC 测试

验证 `POWER_DISPLAY_AUTO_DISABLE_EVENT` 在两个进程同时运行时的回环：

* 启动 PowerToys（runner + PowerDisplay 模块加载）。
* 用一个小测试工具 SignalEvent（用 well-known event 名）。
* 验证 module 的 `m_enabled` 变 false（例如刷新 Settings UI 看 toggle 已 OFF）。

参考已有 `TOGGLE_POWER_DISPLAY_EVENT` 的测试方式。

## 8. 日志

遵循项目"热路径静默、决策点详细"的约定：

| 位置 | 级别 | 文案 |
|---|---|---|
| `CrashDetectionScope.Begin()` 成功 | Info | `CrashDetectionScope: lock written at <path>` |
| `CrashDetectionScope.Begin()` 失败 | Error | `CrashDetectionScope: failed to write lock at <path>: <ex>` |
| `CrashDetectionScope.Dispose()` 成功 | Info | `CrashDetectionScope: lock deleted at <path>` |
| `CrashDetectionScope.Dispose()` 失败 | Warning | `CrashDetectionScope: failed to delete lock at <path>: <ex>` |
| `CrashRecovery.DetectOrphanAndDisable()` 没孤儿 | Trace | `Phase 0: no orphan lock; normal startup` |
| `CrashRecovery.DetectOrphanAndDisable()` 找到孤儿 | Warning | `Phase 0: found orphan lock at <path> with content <json>; entering auto-disable sequence` |
| 严格序列每个步骤 | Info | `Phase 0: step <N> (<name>) ok` |
| 严格序列步骤失败 | Error | `Phase 0: step <N> (<name>) failed: <ex>; sequence aborted, lock retained for retry` |
| Module DLL listener 触发 | Warning | `PowerDisplay AutoDisable event received — disabling module` |
| Settings UI 加载时 flag 存在 | Info | `PowerDisplayViewModel: crash flag present, locking page` |
| Settings UI 用户点 Ignore | Info | `PowerDisplayViewModel: user dismissed crash warning, flag deleted` |

热路径（DDC/CI 的 VCP get/set、刷新循环）保持 log 静默。

## 9. 边界考虑

* **多用户 / RDP：** 文件在 `%LOCALAPPDATA%`，按用户隔离。每个用户的 PowerDisplay 状态独立。用户 A 崩溃只影响用户 A 的 PowerDisplay。用户 B 不受影响。
* **漫游配置文件：** 同样按用户属性。无需特殊处理。
* **备份/恢复（PowerToys 设置备份功能）：** `crash_detected.flag` 和 `discovery.lock` **不应**被包含在 PowerToys 备份中——它们是瞬时运行时状态。如果 `SettingsBackupAndRestoreUtils` 用 glob 模式包含了，需要显式排除。**plan 阶段验证。**
* **卸载：** PowerToys 卸载时整个 `%LOCALAPPDATA%\Microsoft\PowerToys\` 文件夹会被现有卸载逻辑删除，无需特殊清理。
* **GPO 禁用：** 如果 PowerDisplay 被 GPO 禁用，模块根本不启动 PowerDisplay.exe → 没有 Phase 2 → 没有 lock → 崩溃恢复机制无关。`IsCrashLockActive` UI 状态仍受尊重（如果 GPO 生效之前留下了 flag，用户仍能看到警告）。

## 10. 这个设计明确不做什么

* 不识别是哪台显示器导致崩溃。
* 不在重新启用前再次警告。点 Ignore 后用户可能再蓝一次——这是用户在知情下的选择。
* 不去聪明地区分"真蓝屏"和"被 TerminateProcess 强杀"。两者一视同仁。手动强杀 Phase 2 期间的 PowerDisplay.exe 会被识别为崩溃。用户可通过 Ignore 恢复。
* 不在 runner 主体（main.cpp / general_settings.cpp）添加任何 PowerDisplay 特定的代码。所有新 C++ 代码都放在 `PowerDisplayModuleInterface.dll`。
* 不修改任何 DDC/CI 逻辑或内核 API 调用。修复实际蓝屏是内核团队的责任。
* 不实现基于 EDID 的预筛选或已知坏显示器黑名单。这些是 brainstorming 中提到的相关改动，**有意延后**以保持本 PR 的原子性。

## 11. 留给实现 plan 的细节

* `App.xaml.cs` 中 Phase 0 hook 的精确位置。
* 是否在文件系统 / 事件 SignalEvent 后面抽象 interface 以便测试——推荐但非强制。
* `POWER_DISPLAY_AUTO_DISABLE_EVENT` 的新 GUID。
* `PowerDisplay.Models` 是否引用 `PowerDisplay.Lib`（让 `PathConstants` 用 `PowerDisplayPaths` 而非重复字面量）。
* 确认 `PTSettingsHelper::save_general_settings`（C++）和对应的 C# 写入路径不冲突；为 Phase 0 写 settings.json 选定其中一个。**初步倾向：** PowerDisplay.exe 是 C#，用现有的 C# `SettingsUtils.SaveSettings` 走全局 settings 文件路径。
* 本地化 key 必须加到所有 `Resources.resw` 文件（en-US 是规范，翻译流水线会拾取）。

这些点在实现 plan 阶段解决。
