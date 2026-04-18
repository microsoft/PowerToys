# PowerDisplay — Flyout / Identify 窗口定位调试指南

本文档描述 PowerDisplay 模块中 **flyout 主窗口**（跟鼠标所在屏，贴右下角）和
**IdentifyWindow 识别窗口**（每屏一个，居中显示编号）的定位逻辑如何验证，
尤其是在**混合 DPI 多显示器**环境下如何判断定位是否正确。

## 背景（为什么要这份文档）

历史上这里踩过一个坑：`AppWindow.MoveAndResize(rect, displayArea)` 双参重载的
坐标语义未公开文档化，不同 WinAppSDK 版本表现可能不同，曾导致"主屏定位正常、
副屏定位错位"的非对称 bug（主屏 `OuterBounds.X == 0` 时巧合正确，副屏上
`OuterBounds.X != 0` 时丢失偏移）。

修复后的统一约定：

1. **坐标一律使用绝对虚屏物理像素**（= Win32 `SetWindowPos` 语义）。
2. **只用单参** `AppWindow.MoveAndResize(rect)`，不再用双参重载。
3. **DPI 换算只用目标屏的 DPI**（`GetDpiForMonitor` on target），
   不用当前窗口 DPI 做跨屏换算。
4. `DpiSuppressor` 在每次 MoveAndResize 外层抑制 `WM_DPICHANGED`，
   防止框架自动二次缩放。
5. `WindowEx` 的 `MinWidth` / `MinHeight` 在 XAML 里设 `0`，
   避免 WinUIEx 按当前 DPI 算最小物理尺寸夹住目标 size。

核心逻辑位于 [`WindowHelper.cs`](../../../../src/modules/powerdisplay/PowerDisplay/Helpers/WindowHelper.cs)
的 `MoveWindowBottomRight`、`CenterWindowOnDisplay`、`MoveAndResizeAbsolute`。

## 测试环境要求

- **两块或以上**物理显示器。
- **不同的 DPI 缩放设置**。推荐组合：
  - 笔记本内置屏 150% + 外接屏 100%
  - 或 4K 100% + 1080p 125%
  - 任何"两屏 scale 不相等"的组合都可以
- 主屏任务栏位置任意（底部、顶部、左、右都要覆盖）。
- PowerToys 已启用 PowerDisplay 模块并绑定了激活快捷键。

## 启用 debug 日志

1. PowerToys 设置 → 常规 → **Log level** 改为 **Debug**（或 Trace）。
2. 重启 PowerDisplay（关闭再开启模块开关即可）。
3. 日志位置：
   ```
   %LOCALAPPDATA%\Microsoft\PowerToys\Logs\PowerDisplay\*.log
   ```

## 关键日志行：`[FlyoutPos]`

每次窗口被定位都会写一行日志，格式如下：

```
[FlyoutPos] <caller> dip=(WxH) scale=S outer=(X,Y,W,H) work=(X,Y,W,H) target=(X,Y,W,H) before=(X,Y,W,H) after=(X,Y,W,H)
```

### 字段含义

| 字段 | 含义 | 用途 |
|------|------|------|
| `caller` | `MoveWindowBottomRight` / `CenterWindowOnDisplay` | 区分 flyout 场景 vs Identify 场景 |
| `dip` | 传入的 DIP 宽高 | 确认上层传参正确 |
| `scale` | **目标屏** DPI 倍率（100%=1.00, 125%=1.25, 150%=1.50, …） | 确认识别到了正确的目标屏 DPI |
| `outer` | 目标屏 `OuterBounds`（绝对虚屏物理像素） | 确认识别到了正确的目标屏 |
| `work` | 目标屏 `WorkArea`（已扣除任务栏，绝对虚屏物理像素） | 计算输入；对比 `outer` 可推断任务栏朝向 |
| `target` | 代码算出的目标矩形（绝对虚屏物理像素） | **我们传给 API 的值** |
| `before` | `MoveAndResize` 调用**前**的窗口 `Position + Size` | 可看出是否跨屏移动 |
| `after` | `MoveAndResize` 调用**后**的窗口 `Position + Size` | **API 实际落点**——关键判据 |

### 怎么用 grep 过滤

```powershell
# 所有定位事件
Select-String "\[FlyoutPos\]" "$env:LOCALAPPDATA\Microsoft\PowerToys\Logs\PowerDisplay\*.log"

# 只看 flyout 主窗口
Select-String "\[FlyoutPos\] MoveWindowBottomRight" "$env:LOCALAPPDATA\Microsoft\PowerToys\Logs\PowerDisplay\*.log"

# 只看 Identify 窗口
Select-String "\[FlyoutPos\] CenterWindowOnDisplay" "$env:LOCALAPPDATA\Microsoft\PowerToys\Logs\PowerDisplay\*.log"
```

## 验证矩阵

### 场景 A — Flyout 跟鼠标定位

| 步骤 | 操作 | 预期行为 | 日志核对点 |
|------|------|----------|-----------|
| A1 | 鼠标移到 **100% 屏**，按激活快捷键 | flyout 出现在**当前屏右下角**（距屏右/下 ~12 DIP，贴近系统托盘） | `scale=1.00`，`outer.X/Y` 匹配该屏起点，`after == target` |
| A2 | 鼠标移到 **150% 屏**，按激活快捷键 | flyout 出现在**当前屏右下角** | `scale=1.50`，`after == target` |
| A3 | 在两屏间快速来回唤起（各 3–5 次） | 每次都正确落到鼠标所在屏，无位置残留 | 每行 `outer` 都跟随鼠标切换 |
| A4 | 将主屏任务栏改到**顶部/左侧/右侧** | flyout 位置随任务栏避让（贴工作区右下角） | `work.X/Y/W/H` 与 `outer` 的差值体现任务栏朝向 |
| A5 | 临时交换主副屏设置，重复 A1–A3 | 同样正确 | 对称验证 |

### 场景 B — Identify 多屏编号

| 步骤 | 操作 | 预期行为 | 日志核对点 |
|------|------|----------|-----------|
| B1 | 设置 → "识别显示器" | **每块屏中心**独立显示编号，3 秒后各自消失 | 每屏一条 `CenterWindowOnDisplay`，各 `outer` 对应各屏 |
| B2 | 编号数字在 100% 屏与 150% 屏上视觉大小一致 | 字号按目标屏 DPI 自适应 | 两行的 `dip` 可以不同（自适应），但 `scale` 对应该屏 |

## 判定准则

读每条 `[FlyoutPos]` 日志，按下面的检查清单逐项过：

### ✅ 定位正确时应看到

- `target == after`（位置和尺寸**完全相等**）
- `outer.X ≤ target.X < outer.X + outer.W`
- `outer.Y ≤ target.Y < outer.Y + outer.H`
- `scale` 与目标屏实际 DPI 缩放一致

### ⚠️ 异常模式及含义

| 症状 | 可能原因 | 下一步 |
|------|----------|--------|
| `after != target`，且差值 = `outer.X/Y` | `MoveAndResize` 仍在做额外偏移 | 检查是否误用了双参重载 |
| `after.W != target.W` 或 `after.H != target.H` | MinWidth/MinHeight 夹持 或 `WM_DPICHANGED` 漏抑制 | 确认 XAML `MinWidth=0 MinHeight=0`；确认 `DpiSuppressor` 在作用域内 |
| `scale` 与肉眼缩放不符 | `DisplayArea.GetFromPoint` 拿错了屏 | 打印 cursor 位置，核对 `outer` 是否匹配 |
| `target.X < outer.X` 或 `target.X + target.W > outer.X + outer.W` | 公式错误或边距算错 | 检查 `workArea.X + workArea.Width - w - marginRight` 推导 |
| `work.X != outer.X`（或 Y 不等）但无任务栏在该边 | `WorkArea` 语义与假设不符 | 罕见情况；记录值后进一步调查 |
| 根本没有 `[FlyoutPos]` 日志 | Log level 不是 Debug / Trace；或调用路径没走 `MoveAndResizeAbsolute` | 检查设置；grep 其他关键字 |

## 回归快速自检（开发者本地）

在双屏混合 DPI 机器上：

1. 编译：
   ```
   cd src\modules\powerdisplay\PowerDisplay
   ..\..\..\..\tools\build\build.cmd
   ```
   确认 `build.debug.x64.errors.log` 为空、exit code = 0。
2. 安装并启用 PowerDisplay，Log level 设 Debug。
3. 按场景 A1–A3 各唤起一次 flyout，按场景 B1 触发一次识别。
4. `tail` 日志文件，核对至少 4 行 `[FlyoutPos]`，每行按判定准则过一遍。
5. 把日志里 `target` 的屏幕坐标和肉眼观察到的窗口位置对照（Spy++ 或截屏测距都可以）。

## 什么时候移除这些日志

`[FlyoutPos]` 日志是**临时诊断手段**。当满足全部条件时可以删除：

1. 在 ≥2 台不同硬件、不同 DPI 组合的机器上，场景 A/B 全部通过。
2. 近期没有新增用户反馈定位问题（观察至少 1 个发布周期）。
3. 没有新的 WinAppSDK 版本升级影响 `AppWindow.MoveAndResize` 行为。

移除方法：只删 [`WindowHelper.cs`](../../../../src/modules/powerdisplay/PowerDisplay/Helpers/WindowHelper.cs)
里 `MoveAndResizeAbsolute` 函数末尾的 `ManagedCommon.Logger.LogDebug(...)` 整块即可，
保留助手函数本身（它是单入口，简化调用方）。

## 相关代码与历史

- 当前定位核心：[`WindowHelper.cs`](../../../../src/modules/powerdisplay/PowerDisplay/Helpers/WindowHelper.cs)
- DPI 变更抑制：[`DpiSuppressor.cs`](../../../../src/modules/powerdisplay/PowerDisplay/Helpers/DpiSuppressor.cs)
- Flyout 调用方：[`MainWindow.xaml.cs`](../../../../src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/MainWindow.xaml.cs)
  的 `AdjustWindowSizeToContent` → `PositionWindowBottomRight`
- Identify 调用方：[`IdentifyWindow.xaml.cs`](../../../../src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/IdentifyWindow.xaml.cs)
  的 `PositionOnDisplay` → `CenterWindowOnDisplay`
- 参考模式：CmdPal 的 [`WindowPositionHelper.cs`](../../../../src/modules/cmdpal/Microsoft.CmdPal.UI/Helpers/WindowPositionHelper.cs)
  + [`MainWindow.xaml.cs`](../../../../src/modules/cmdpal/Microsoft.CmdPal.UI/MainWindow.xaml.cs)
  的 `MoveAndResizeDpiAware` — PowerDisplay 参照 CmdPal 的绝对坐标 + 单参 MoveAndResize + DPI 抑制三件套。
