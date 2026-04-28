# PowerToys Run 拆分为独立仓库 — 设计文档

**日期**：2026-04-28
**状态**：Draft（pending user review）
**作者**：yuleng@microsoft.com
**议题**：将 PowerToys Run 模块从 PowerToys 主仓拆分到独立仓库 `microsoft/PowerToysRun`，使其能够独立 build、安装、运行，覆盖现有完整功能

---

## 1. 目标与非目标

### 目标

- 创建一个全新的独立仓库 `microsoft/PowerToysRun`
- 该仓库可以独立 build 出可工作的 MSI 安装包
- 用户安装独立 MSI 后能完整使用 PowerToys Run 现有功能（搜索、19 个保留内置插件、热键、主题、本地化、设置）
- 现有社区 Run 插件 DLL 可 drop-in 使用，无需修改源码（仅可能需 rebuild 一次）

### 非目标（本阶段明确不做）

- ❌ 不修改 PowerToys 主仓代码（不删除 `src/modules/launcher/`、不改 installer、不动 Settings UI、不改 runner）
- ❌ 不决定 PowerToys 主仓何时切除 Run（后续阶段处理）
- ❌ 不做内置自动更新机制
- ❌ 不做 Microsoft Store 提交
- ❌ 不做 telemetry pipeline
- ❌ 不重新设计插件 API

---

## 2. 关键决策摘要

| # | 决策点 | 选择 | 备注 |
|---|---|---|---|
| Q1 | 发行模型 | **完全独立**：从 PT 中分离，独立分发 | 不再走 PT installer |
| Q2 | 共享依赖处理 | **裁剪 + Vendor**：只 fork 真正必需的子集 | 删 GPO、删 PT-Interop、删 Telemetry |
| Q3 | 进程模型 | **真·单进程**：所有功能（后台、热键、托盘、主 UI、Settings UI）在一个 `PowerToys.PowerLauncher.exe` 中 | 关键升级：Settings 不再是单独 exe |
| Q4 | 插件兼容性 | **完全二进制兼容** | 现有 Plugin DLL drop-in 可用 |
| Q5 | Settings UI 实现 | **WPF 重写 + 集成单进程，极简自定义控件**（用 WPF 内置控件替代 WinUI3 控件） | 视觉接受经典 WPF 风格 |
| Q6 | 用户数据迁移 | **新数据目录** `%LOCALAPPDATA%\PowerToys Run\` + Settings UI 内置 "Import from PowerToys" 按钮 | 自动检测、自动显示 InfoBar、不含插件 cache |
| Q7 | 分发方式 | **MSI（per-user 默认 + per-machine 双模式）+ winget**；.NET self-contained | 不做 MSIX |
| Q8 | 品牌命名 | **保留 "PowerToys Run"**；repo 名 `microsoft/PowerToysRun` | 显示名、命名空间、exe 名都不变 |
| Q9 | Telemetry | **完全去掉** | 删 PowerLauncher.Telemetry 全部 |
| 路径 | 执行节奏 | **一次性大爆炸**：新 repo 一次性建成 | 与 PT 主仓不耦合时序 |
| Git | 历史保留 | **Clean slate（不保留历史）** | 单个 initial commit；丢弃 PT 历史与作者归属 |

---

## 3. 设计章节

### §1 目标 repo 布局与代码复制映射

新仓 `microsoft/PowerToysRun` 顶层结构：

```
PowerToysRun/
├── src/
│   ├── PowerLauncher/                     ← 复制自 PT/src/modules/launcher/PowerLauncher/
│   │   ├── (现有代码)
│   │   ├── Views/Settings/                ← 新增：所有 WPF 设置 UI
│   │   │   ├── SettingsWindow.xaml(.cs)
│   │   │   ├── PowerLauncherSettingsPage.xaml(.cs)  ← 800→500 行简化的 WPF 版本
│   │   │   └── DataTemplates/PluginOptionTemplates.xaml  ← 9 种 plugin AdditionalOption 模板
│   │   ├── Controls/                      ← 极少量自定义 WPF 控件
│   │   │   ├── InfoBar.xaml(.cs)         ← 唯一新 UserControl
│   │   │   └── NumberBox.cs              ← 30-50 行
│   │   ├── Helpers/
│   │   │   └── NumberValidationRule.cs
│   │   └── Services/
│   │       └── SettingsImportService.cs   ← 从旧 PT 路径导入
│   ├── Wox.Plugin/                        ← 复制（保留命名空间，关键的 Q4 兼容点）
│   ├── Wox.Infrastructure/                ← 复制
│   ├── Plugins/                           ← 复制（19 个内置插件，删 Microsoft.PowerToys.Run.Plugin.PowerToys）
│   ├── Common/                            ← 从 PT 主仓 vendor 进来的最小子集
│   │   ├── ManagedCommon/                 ← 裁剪后的子集（删 RunnerHelper，改 PowerToysPathResolver）
│   │   ├── Common.UI/                     ← 子集（保留 ThemeManager/ThemeListener/NativeEventWaiter；删 SettingsDeepLink）
│   │   └── Settings.Library/              ← 仅 PowerLauncher* 相关类（命名空间保留 Microsoft.PowerToys.Settings.UI.Library）
│   └── Tests/
│       ├── Wox.Test/                      ← 复制
│       └── Plugins/*.UnitTests/           ← 复制
├── installer/
│   └── PowerToysRunSetup/                 ← 从 installer/PowerToysSetupVNext/Run.wxs 抽取并独立化
├── winget/
│   └── manifest/                          ← winget-pkgs 提交模板
├── doc/                                   ← 用户文档、插件开发指南、迁移指南
├── tools/
│   └── extract-localization.ps1           ← 一次性 resw 抽取脚本
├── .github/workflows/
│   ├── build.yml                          ← PR CI
│   └── release.yml                        ← tag 触发的 release 流水线
├── PowerToysRun.sln                       ← 新建独立 sln
├── Directory.Build.props                  ← 从 PT 裁剪
├── README.md
└── LICENSE                                ← MIT（同 PT）
```

**复制策略**：
- **Clean slate**：直接 `cp -r` 把 `src/modules/launcher/{PowerLauncher,Wox.Plugin,Wox.Infrastructure,Plugins,Wox.Test}` 这 5 个子目录拷贝到新 repo，**不保留 PT 历史**
- 新 repo 从一个 "Initial commit from PowerToys main repo (snapshot 2026-04-28)" 开始；commit message 注明源 commit SHA 以便追溯
- 不保留作者归属（git blame 全部归到这次的初始 commit）
- **不复制**：`Microsoft.Launcher/`（C++ 模块 DLL）、`PowerLauncher.Telemetry/`、`Plugins/Microsoft.PowerToys.Run.Plugin.PowerToys/`

**复制后修改**（仅在新 repo 内）：
- `PowerLauncher.csproj` 中所有 `..\..\..\common\*` ProjectReference 改为指向新仓的 `src/Common/*`
- 移除对 `GPOWrapperProjection`、`PowerToys.Interop`、`PowerLauncher.Telemetry` 的引用
- 删除调用 `Telemetry`/`GPO`/runner-IPC 的代码点（编译错误驱动，逐个清理）

**双跑期间冲突缓解**：用户同时安装 PT (含 Run) + 独立 PowerToys Run 时，两个进程都会注册全局热键 → 后启动者失败。独立 Run 启动时检测 PT 集成版进程（路径区分），冲突时弹提示。

### §2 依赖审计与 Vendor 策略

**实际使用情况（依据 grep 结果）**：

| 共享库 | 处置 | 落地位置 |
|---|---|---|
| `ManagedCommon` | **裁剪 vendor**（删 `RunnerHelper`，改写 `PowerToysPathResolver` 为新 Run 自己的路径解析） | `src/Common/ManagedCommon/` |
| `Common.UI` | **裁剪 vendor**（保留 `NativeEventWaiter`、`ThemeManager`、`ThemeListener`、`CustomLibraryThemeProvider`；**删除 `SettingsDeepLink`**——因 PT 工具插件被删后无消费者） | `src/Common/Common.UI/` |
| `Settings.UI.Library` | **小子集 vendor**（仅 `PowerLauncher*` 类 + 它们传递依赖到的 `SettingsUtils`、`HotkeySettings`、`PluginAdditionalOption` 等基础类）；命名空间保留 `Microsoft.PowerToys.Settings.UI.Library` | `src/Common/Settings.Library/` |
| `GPOWrapperProjection` / `GPOWrapper` | **删除**（启动检查、PluginManager、SettingsReader、PT 工具插件全部去 GPO 化） | — |
| `PowerToys.Interop` | **删除**（hotkey 改 Win32 RegisterHotKey；centralized hook 路径删；RunExitEvent 删） | — |
| `ManagedTelemetry` / `PowerLauncher.Telemetry` | **删除** | — |
| C++ `logger` / `SettingsAPI` | **删除**（仅被已弃用的 `Microsoft.Launcher.dll` 用） | — |
| `Microsoft.PowerToys.Run.Plugin.PowerToys` 插件 | **整个删除** | — |

**关键兼容性约束（确保 Q4）**：
- `Microsoft.PowerToys.Settings.UI.Library` 命名空间必须保留（Plugins 各处用了）
- `Wox.Plugin` 命名空间必须保留
- vendor 来的类放到 `src/Common/` 下时，namespace 不要改

### §3 进程与激活模型（接管 runner 的职责）

**真·单进程**：

```
PowerToys.PowerLauncher.exe（唯一 exe）
├── TrayIconService                      ← P/Invoke Shell_NotifyIconW + 隐藏窗口接 WM_TRAYICON
├── HotkeyService                        ← Win32 RegisterHotKey + WM_HOTKEY
├── AutostartService                     ← HKCU\Software\Microsoft\Windows\CurrentVersion\Run
├── SingleInstanceGuard                  ← 命名 Mutex（mutex 名带 "Standalone" 区分 PT 集成版）
├── SearchWindow（MainWindow）           ← 按 hotkey 弹出/隐藏
└── SettingsWindow                       ← 托盘菜单"Settings"打开（同进程的 WPF Window）
```

**关键删除点**：
- `App.xaml.cs::GetPowerToysPId()` 检测父 runner pid → 删
- `RunnerHelper.WaitForPowerToysRunner` → 删
- `Constants.RunExitEvent()`、`PowerLauncherCentralizedHookSharedEvent` 监听 → 删
- 所有 GPO 检查（启动 + per-plugin） → 删
- `PowerToys.Interop` 所有调用点 → 删

**热键冲突兜底**：注册失败时显示 InfoBar/Toast 提示用户在设置中换一个热键（不再有 centralized hook 兜底）。

**托盘**：P/Invoke `Shell_NotifyIconW`，左键唤起搜索框，右键菜单：Settings / Restart / Exit。

**单实例**：保留现有 `SingleInstance<App>` 命名 Mutex 机制，mutex 名改为新名（如 `Local\PowerToys-Run-Standalone-XXX`）。

**自启**：写 `HKCU\...\Run\PowerToysRun`，Settings UI 提供开关（默认开）。

### §4 设置 UI 重新设计（WPF 极简版）

**总策略**：用 WPF 内置控件 + 极少量自定义控件替代 WinUI3，**功能一致 + 布局类似**，接受经典 WPF 视觉风格。

**控件替换映射**：

| WinUI3 控件 | 用什么替代 |
|---|---|
| `tkcontrols:SettingsCard` | `Border` + `Grid` inline |
| `tkcontrols:SettingsExpander` | WPF 自带 `Expander` |
| `controls:SettingsGroup` | WPF 自带 `GroupBox` 或 `StackPanel` + 标题 |
| `ToggleSwitch` | WPF 自带 `CheckBox` |
| `InfoBar` | 50 行 UserControl（Border + Icon + TextBlock + 可选 Button），4 种 severity |
| `NumberBox` | `TextBox` + `NumberValidationRule` |
| `AutoSuggestBox` | 普通 `TextBox` + TextChanged 触发 ViewModel 过滤命令 |
| `ItemsRepeater` | WPF 自带 `ItemsControl` |
| `IsEnabledTextBlock` | `TextBlock` + Trigger 绑 IsEnabled 改 Opacity |
| `CheckBoxWithDescriptionControl` | `CheckBox`（Content = StackPanel 含主标题 + 说明） |
| `ShortcutControl` | **复用 PowerLauncher 已有的热键 UI** |

**真正要新写的（仅 3 项）**：
- `InfoBar.xaml(.cs)` — 0.5 天
- `NumberBox.cs` + `NumberValidationRule` — 0.5 天
- `ShortcutControl` 微调 — 0.5 天

**复用部分（约 90%）**：
- `PowerLauncherViewModel.cs` (715 行) — 删除 GPO 引用、删除 IPC 调用 (`ShellPage.SendDefaultIPCMessage`)，改为直接调 `SettingsRepository.Save()`
- `PowerLauncherPluginViewModel.cs` (221 行) — 同上
- 设置数据模型（`PowerLauncherSettings`、`PluginAdditionalOption` 等）— 完全原样 vendor

**主题**：复用 PowerLauncher 现有的 `ThemeManager`（监听 `WM_SETTINGCHANGE`），WPF `DynamicResource` + 两套 ResourceDictionary（Light.xaml / Dark.xaml）。

**IPC 简化（关键）**：
- 删除整条 Named Pipe IPC 路径（`ShellPage.SendDefaultIPCMessage`）
- ViewModel 直接调 `SettingsRepository.Save()` → 写 `%LOCALAPPDATA%\PowerToys Run\settings.json`
- 同进程下：直接调 `SettingsReader.Reload()`（即时）
- 同进程外（外部修改 settings.json）：现有 `FileSystemWatcher` 兜底

**预估工作量**：10-12 工作日（含调试）。

### §5 Installer（WiX dual-mode）+ winget

**工程结构**：
```
installer/PowerToysRunSetup/
├── PowerToysRunSetup.wixproj        ← 独立 WiX 4 工程
├── Product.wxs                       ← 顶层产品定义
├── Run.wxs                           ← 从 PT Run.wxs 剥离/裁剪
├── Common.wxi                        ← 从 PT Common.wxi 裁剪
├── DualMode.wxi                      ← per-machine / per-user 切换
├── ui/                               ← 安装界面资源
└── Strings/<lang>/
```

**输出布局（安装目录）**：
```
<InstallDir>/
├── PowerToys.PowerLauncher.exe       ← 唯一主进程
├── *.dll                             ← Wox.Plugin、Wox.Infrastructure、ManagedCommon (vendor) 等
├── Assets/PowerLauncher/
└── RunPlugins/
    ├── Calculator/
    ├── Folder/
    └── ...（19 个保留插件，已删 PowerToys 工具启动器）
```

**安装路径**：
- per-user 默认：`%LOCALAPPDATA%\Programs\PowerToys Run\`
- per-machine：`C:\Program Files\PowerToys Run\`（命令行 `msiexec /i ... ALLUSERS=1`）

**关键决策**：
- per-user 为默认（无 UAC 友好）
- .NET runtime 走 self-contained（每个 exe 自带，无外部依赖）
- 所有插件必装（不引入插件可选安装）
- 卸载保留用户数据目录 `%LOCALAPPDATA%\PowerToys Run\`
- 升级走 MSI Major Upgrade
- ARP 显示名 "PowerToys Run"，新 `UpgradeCode` GUID（与 PT 主 MSI 完全隔离）
- 不做 Bootstrapper EXE、不做 MSIX、不做 Store 提交

**winget**：
```
winget/manifest/
├── Microsoft.PowerToysRun.installer.yaml
├── Microsoft.PowerToysRun.locale.en-US.yaml
└── Microsoft.PowerToysRun.yaml
```
PR 提交到 `microsoft/winget-pkgs`。`InstallerType: msi` + 两份 Installer 节点（machine / user scope）。

### §6 从 PowerToys 导入设置（in-Settings 按钮）

**入口位置**：Settings 窗口顶部 InfoBar，**自动显示**于以下条件满足时：
- 旧路径存在：`%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\settings.json`
- 新路径为默认值或不存在：`%LOCALAPPDATA%\PowerToys Run\settings.json`

```
┌────────────────────────────────────────────────────────────┐
│ ⓘ Existing PowerToys Run settings detected on this PC.      │
│                                              [ Import ] [×] │
└────────────────────────────────────────────────────────────┘
```

也可在 About / Advanced 区块手动找到同一按钮。Dismissed 后写入 user-pref `ImportInfoBarDismissed=true`，下次不再显示。

**`SettingsImportService.cs`**（在 `src/PowerLauncher/Services/`）：
- `IsLegacyDataAvailable()`、`IsCurrentDataEmpty()`
- `Import(IProgress<string> progress = null)` → `ImportResult`
- 内部：`BackupCurrent` / `CopyDirectory` / `Rollback` / `NotifyReload`

**导入范围**：
- ✅ `settings.json`（核心配置）
- ✅ `Plugins/<Name>/settings.json`（每个插件配置）
- ❌ `Plugins/<Name>/cache/*` 缓存（不复制；用户首次启动重新生成）
- ❌ `Microsoft.PowerToys.Run.Plugin.PowerToys/` 配置（该插件已删）

**流程**：
1. 确认 Dialog："This will overwrite your current PowerToys Run settings. A backup will be saved. Continue?"
2. 备份当前设置到 `%LOCALAPPDATA%\PowerToys Run\backups\settings-<timestamp>.json` + `Plugins.zip`
3. 复制（不是移动）旧路径 → 新路径
4. 失败时回滚（恢复 backup）
5. 同进程下直接调 `SettingsReader.Reload()` 立即生效
6. 显示成功 Toast "Settings imported. Restart may be needed for some plugins."（提供 "Restart now" 按钮）
7. **不删除**旧 PT 数据

**失败模式**：
- 旧 settings.json 损坏 → InfoBar 错误，引导用户在 PT 中重置
- 部分插件 schema 漂移 → 跳过 + dialog 列出跳过项
- 复制中断 → backup + atomic rename 写入策略保证不破坏目标

### §7 Build / CI、本地化、更新机制

**Solution & Build**：
- 单一 `PowerToysRun.sln`，包含所有 csproj + wixproj
- `Directory.Build.props` 沿用 PT 的 `Common.Dotnet.CsWinRT.props` 中相关项，删 PT-specific RepoRoot 引用
- `TargetFramework=net8.0-windows10.0.20348.0`（与 PT 现状一致）
- 输出路径：`$(SolutionDir)bin\$(Platform)\$(Configuration)\`
- 目标平台：x64 + arm64

**GitHub Actions CI**：

`.github/workflows/build.yml`（PR 触发）：
- runs-on: windows-2022
- matrix: [x64, arm64]
- steps: checkout → setup-dotnet 8.x → nuget restore → msbuild sln → dotnet test → msbuild wixproj → upload-artifact MSI

`.github/workflows/release.yml`（tag `v*` 触发）：
- 同 build
- 加签名步骤（ESRP/SignTool；详见 §8 R2）
- 创建 GitHub Release，上传 x64/arm64 两份 MSI

**本地化**：
- PowerLauncher / Plugins 的 `*.resx` 整体复制原样（已经独立于 PT）
- 设置 UI 字符串：写 `tools/extract-localization.ps1` 从 PT `Settings.UI/Strings/<lang>/Resources.resw` 抽取以 `PowerLauncher_*` / `Run_*` / `Activation_*` / `Shortcut*` / `Radio_Theme_*` / `ColorModeHeader` / `ShowPluginsOverview_*` 等开头的 keys
- 抽取后的 resw 转 `Resources.<lang>.resx`（schema 几乎一致）
- 18 种语言全部抽取，人工 spot-check 三种（en/zh/de）
- WPF 通过 `ResourceManager` + satellite assembly 加载；XAML 中用 `{x:Static prop:Resources.X}`
- 启动时 `LanguageHelper.LoadLanguage()` 设 `CurrentUICulture`，运行中不切（保留现有行为）

**更新机制**：
- 完全推迟。**不做应用内"检查更新"**、**不做启动检查**、**不做新版本提示 InfoBar**
- 用户手动从 GitHub Releases 下载新 MSI 重装；或 `winget upgrade Microsoft.PowerToysRun`
- WiX `MajorUpgrade` 自动卸旧装新

### §8 风险、开放项、推迟工作

**已识别的风险**：

- **R1：Q4 "完全二进制兼容" 实际可达性** — 插件依赖的程序集版本/strong name 变化可能破坏加载。缓解：vendor 出来的程序集保持原 namespace + 原 assembly name；发布前用 GitHub top 10 社区插件做兼容性测试。
- **R2：签名（Code Signing）** — 独立 repo 接入 Microsoft ESRP 签名 pipeline 需要内部审批。本设计假设可拿到签名通道，但实操是 ops 工作。
- **R3：本地化抽取的完整性** — 18 语言 resw 抽取漏抽 = label 显示空。缓解：脚本抽取 + 人工 spot-check。
- **R4：双跑期间的进程冲突** — 用户同时装 PT (含 Run) + 独立 Run 时全局热键互踩。缓解：启动检测 + 提示。下一阶段 PT 切除 Run 后自动消失。
- **R5：Microsoft.Plugin.Indexer 插件的 COM 依赖** — 用 Windows Search COM Interop。缓解：独立 build 后端到端验证。
- **R6：测试覆盖率** — PT 主仓的集成测试可能依赖 runner.exe；本设计仅承诺单元测试全部通过，UI 自动化测试可能短期不可用。
- **R7：CI 签名密钥** — GitHub Actions runner 接入签名证书的安全方式未定。

**开放项**：

| # | 项 | 解决责任 |
|---|---|---|
| O1 | ESRP 签名 pipeline 接入 | ops |
| O2 | `git filter-repo` 历史保留细节（issue/PR 不可迁） | repo migration ops |
| O3 | 独立 repo 名最终敲定（`PowerToysRun` vs `PowerToys-Run` vs `powertoys-run`） | 用户决策 |
| O4 | OneNote 插件的 `Microsoft.Office.Interop.OneNote` 在 self-contained build 下打包 | 实现期验证 |
| O5 | x64 vs arm64 在 winget 同一 manifest 内 `Architecture` 节点表达 | 实现期验证 |
| O6 | 仓库治理基础设施（issue templates、CONTRIBUTING、CODE_OF_CONDUCT） | 设置阶段补 |

**推迟到下一阶段（本次明确不做）**：

| # | 内容 |
|---|---|
| D1 | 删除 PT 主仓中的 `src/modules/launcher/`、`Run.wxs`、`PowerLauncherPage.xaml`、`Settings.UI.Library/PowerLauncher*`、`runner` 中 `Microsoft.Launcher.dll` 加载逻辑 |
| D2 | PT 中加 deprecation 提示引导用户去新 repo |
| D3 | 内置自动更新 / 更新通知 |
| D4 | Microsoft Store 上架 |
| D5 | 集中键盘钩子（Centralized Keyboard Hook）兜底 |
| D6 | telemetry（如未来想做，单独设计 opt-in pipeline） |
| D7 | 独立 plugin marketplace |

---

## 4. 验收标准

新 repo "完整运行" 的判定：

1. ✅ `PowerToysRun.sln` 在 Windows + Visual Studio 可打开、x64 全 build green
2. ✅ Wox.Test + 各 Plugins.UnitTests 全部 pass
3. ✅ 安装 MSI 后能从 winget/installer 启动 `PowerToys.PowerLauncher.exe`
4. ✅ 全局热键 `Alt+Space` 工作；搜索框正常弹出
5. ✅ 至少 5 个内置插件功能正常（Calculator / Program / Folder / Shell / WindowWalker）
6. ✅ Settings 窗口能从托盘打开；改 hotkey、改主题、enable/disable 插件能立即生效
7. ✅ Import from PowerToys 按钮检测旧数据并成功导入
8. ✅ 18 语言下 Settings UI 无空字符串
9. ✅ 至少 3 个 top 社区插件 DLL drop-in 可用（验证 Q4 兼容性）
10. ✅ MSI 可被卸载，卸载后保留用户数据目录

---

## 5. 工作量与时间估算

| 任务块 | 估时（工作日） |
|---|---|
| §1 repo 复制 + 项目引用修复 + 编译 green | 3-4 |
| §2 共享依赖 vendor + 裁剪 | 2-3 |
| §3 进程接管（hotkey、tray、autostart、单实例） | 3-4 |
| §4 Settings UI（自定义控件 1.5 天 + Page 重写 3-4 天 + ViewModel 适配 1 天 + 主题 0.5 天 + 模板 2 天 + 联调 1.5-2 天） | 10-12 |
| §5 Installer + winget manifest | 3-4 |
| §6 Settings Import 服务 | 1-2 |
| §7 CI + 本地化抽取脚本 | 2-3 |
| §8 兼容性测试 + 端到端验证 | 3-4 |
| **合计** | **27-36 个工作日（约 1.5-2 个开发月）** |

---

## 6. 后续步骤

设计文档 review 通过后，进入 `superpowers:writing-plans` skill 产出可执行的 step-by-step 实施计划。
