# Sparse Package + WinUI 3 调查报告

## 背景

PowerToys 希望使用 Windows App SDK 的 Semantic Search API (`AppContentIndexer.GetOrCreateIndex`) 来实现语义搜索功能。该 API 要求应用具有 **Package Identity**。

## 问题现象

### 1. Semantic Search API 调用失败

在 [SemanticSearchIndex.cs](../../src/common/Common.Search/SemanticSearch/SemanticSearchIndex.cs) 中调用 `AppContentIndexer.GetOrCreateIndex(_indexName)` 时，抛出 COM 异常：

```
System.Runtime.InteropServices.COMException (0x80004005): Error HRESULT E_FAIL has been returned from a call to a COM component.
   at WinRT.ExceptionHelpers.<ThrowExceptionForHR>g__Throw|39_0(Int32 hr)
   at Microsoft.Windows.AI.Search.AppContentIndexer.GetOrCreateIndex(String indexName)
```

### 2. API 要求

根据 [Windows App SDK 文档](https://learn.microsoft.com/en-us/windows/ai/apis/content-search)，Semantic Search API 需要：
- Windows 11 24H2 或更高版本
- NPU 硬件支持
- **Package Identity**（应用需要有 MSIX 包标识）

## Sparse Package 方案

### 什么是 Sparse Package

Sparse Package（稀疏包）是一种为非打包（unpackaged）Win32 应用提供 Package Identity 的技术，无需完整的 MSIX 打包。

参考：[Grant package identity by packaging with external location](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps)

### 实现架构

```
PowerToysSparse.msix (仅包含 manifest 和图标)
    │
    ├── AppxManifest.xml  (声明应用和依赖)
    ├── Square44x44Logo.png
    ├── Square150x150Logo.png
    └── StoreLogo.png

ExternalLocation (指向实际应用目录)
    │
    └── ARM64\Debug\
        ├── PowerToys.Settings.exe
        ├── PowerToys.Settings.pri
        └── ... (其他应用文件)
```

### 关键组件

| 文件 | 位置 | 作用 |
|------|------|------|
| AppxManifest.xml | src/PackageIdentity/ | 定义 sparse package 的应用、依赖和能力 |
| app.manifest | src/settings-ui/Settings.UI/ | 嵌入 exe 中，声明与 sparse package 的关联 |
| BuildSparsePackage.ps1 | src/PackageIdentity/ | 构建和签名脚本 |

### Publisher 配置

**重要**：app.manifest 和 AppxManifest.xml 中的 Publisher 必须匹配。

| 环境 | Publisher |
|------|-----------|
| 开发环境 | `CN=PowerToys Dev, O=PowerToys, L=Redmond, S=Washington, C=US` |
| 生产环境 | `CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US` |

BuildSparsePackage.ps1 会在本地构建时**自动**将 AppxManifest.xml 的 Publisher 替换为开发环境值，无需手动修改源码。

## 当前问题：WinUI 3 + Sparse Package 崩溃

### 现象

当 Settings.exe（WinUI 3 应用）通过 sparse package 启动时，立即崩溃：

```
Microsoft.UI.Xaml.Markup.XamlParseException (-2144665590):
Cannot locate resource from 'ms-appx:///Microsoft.UI.Xaml/Themes/themeresources.xaml'. [Line: 11 Position: 40]
```

### 新观察（2026-01-25）

对齐 WinAppSDK 版本并恢复 app-local 运行时后，仍可复现**更早期**的崩溃（未写入 Settings 日志）：

- Application Error / WER（AUMID 启动）：
  - Faulting module: `CoreMessagingXP.dll`
  - Exception code: `0xc0000602`
  - Faulting module path: `C:\PowerToys\ARM64\Debug\WinUI3Apps\CoreMessagingXP.dll`
- 暂时移除 `CoreMessagingXP.dll` 后，出现 .NET Runtime 1026：
  - `COMException (0x80040111): ClassFactory cannot supply requested class`
  - 发生在 `Microsoft.UI.Xaml.Application.Start(...)`

这说明 **“themeresources.xaml 无法解析”并不是唯一/必现的失败模式**，app-local 运行时在 sparse identity 下可能存在更底层的初始化问题。

### 新观察（2026-01-25 晚间）

framework-dependent + bootstrap 方向有实质进展：

- 设置 `WindowsAppSDKSelfContained=false`（仅在 `UseSparseIdentity=true` 时生效）
- 添加 `WindowsAppSDKBootstrapAutoInitializeOptions_OnPackageIdentity_NoOp=true`
- **从 ExternalLocation 根目录与 `WinUI3Apps` 目录移除 app-local WinAppSDK 运行时文件**
  - 尤其是 `CoreMessagingXP.dll`，否则会优先加载并导致 `0xc0000602`
- **保留/放回 bootstrap DLL**
  - 必需：`Microsoft.WindowsAppRuntime.Bootstrap.Net.dll`
  - 建议同时保留：`Microsoft.WindowsAppRuntime.Bootstrap.dll`

按以上处理后，Settings 通过 AUMID 启动不再崩溃，日志写入恢复。

### 根本原因分析

1. **ms-appx:/// URI 机制**
   - WinUI 3 使用 `ms-appx:///` URI 加载 XAML 资源
   - 这个 URI scheme 依赖于 MSIX 包的资源索引系统

2. **框架资源位置**
   - `themeresources.xaml` 等主题资源在 Windows App Runtime 框架包中
   - 框架包位置：`C:\Program Files\WindowsApps\Microsoft.WindowsAppRuntime.2.0-experimental4_*\`（应与 WinAppSDK 版本匹配）
   - 资源编译在框架包的 `resources.pri` 中

3. **WinAppSDK 版本/依赖不一致（更可能的原因）**
   - 仓库当前引用 `Microsoft.WindowsAppSDK` **2.0.0-experimental4**（见 `Directory.Packages.props`）
   - Sparse manifest 仍依赖 **Microsoft.WindowsAppRuntime.2.0-experimental3**（`MinVersion=0.676.658.0`）
   - 通过包标识启动时会走框架包资源图，如果依赖版本不匹配，WinUI 资源解析可能失败，从而触发上述 `XamlParseException`
   - 需要先对齐依赖版本，再判断是否是 sparse 本身限制

4. **app-local 运行时在 sparse identity 下崩溃（已观测）**
   - 即使对齐 WinAppSDK 版本，也可能在 `CoreMessagingXP.dll` 处崩溃（`0xc0000602`）
   - 此时 Settings 日志不一定写入，需查看 Application Event Log

4. **Sparse Package 的限制（待验证）**
   - 之前推断 `ms-appx:///` 在 sparse package 下无法解析框架依赖资源
   - 但在修正依赖版本之前无法下结论

### 对比：WPF 应用可以工作

WPF 应用（如 ImageResizer）使用 sparse package 时**可以正常工作**，因为：
- WPF 不依赖 `ms-appx:///` URI
- WPF 资源加载使用不同的机制

## 已尝试的解决方案

| 方案 | 结果 | 原因 |
|------|------|------|
| 复制 PRI 文件到根目录 | ❌ 失败 | `ms-appx:///` 不查找本地 PRI |
| 复制 themeresources 到本地 | ❌ 失败 | 资源在 PRI 中，不是独立文件 |
| 修改 Settings OutputPath 到根目录 | ❌ 失败 | 问题不在于应用资源位置 |
| 复制框架 resources.pri | ❌ 失败 | `ms-appx:///` 机制问题 |
| 对齐 WindowsAppRuntime 依赖版本 | ⏳ 待验证 | 先排除依赖不一致导致的资源解析失败 |
| app-local 运行时（self-contained）+ sparse identity | ❌ 失败 | Application Error: `CoreMessagingXP.dll` / `0xc0000602` |
| 移除 `CoreMessagingXP.dll` | ❌ 失败 | .NET Runtime 1026: `ClassFactory cannot supply requested class` |
| framework-dependent + bootstrap + 清理 ExternalLocation 中 app-local 运行时 | ✅ 成功 | 需保留 `Microsoft.WindowsAppRuntime.Bootstrap*.dll` |
| 将 resources.pri 打进 sparse MSIX | ✅ 成功 | MRT 可从包内 resources.pri 正常解析字符串 |

## 当前代码状态

### 已修正（建议保留）

1. **Settings.UI 输出路径**
   - 文件：`src/settings-ui/Settings.UI/PowerToys.Settings.csproj`
   - 修改：恢复为 `WinUI3Apps`（避免破坏 runner/installer/脚本路径假设）

2. **AppxManifest.xml 的 Executable 路径**
   - 文件：`src/PackageIdentity/AppxManifest.xml`
   - 修改：恢复为 `WinUI3Apps\PowerToys.Settings.exe`

3. **AppxManifest.xml 的 WindowsAppRuntime 依赖**
   - 文件：`src/PackageIdentity/AppxManifest.xml`
   - 修改：更新为 `Microsoft.WindowsAppRuntime.2.0-experimental4`，`MinVersion=0.738.2207.0`（与 `Microsoft.WindowsAppSDK.Runtime` 2.0.0-experimental4 对齐）

### 未修改（源码中保持生产配置）

- AppxManifest.xml 的 Publisher 保持 Microsoft Corporation（脚本会自动替换）

### 验证步骤（建议）

1. **确认 WindowsAppRuntime 版本已安装**
   - `Get-AppxPackage -Name Microsoft.WindowsAppRuntime.2.0-experimental4`
   - 如缺失，可从 NuGet 缓存安装：  
     `Add-AppxPackage -Path "$env:USERPROFILE\.nuget\packages\microsoft.windowsappsdk.runtime\2.0.0-experimental4\tools\MSIX\win10-x64\Microsoft.WindowsAppRuntime.2.0-experimental4.msix"`

2. **构建并注册 sparse package**
   - `.\src\PackageIdentity\BuildSparsePackage.ps1 -Platform x64 -Configuration Debug`
   - `Add-AppxPackage -Path ".\x64\Debug\PowerToysSparse.msix" -ExternalLocation ".\x64\Debug"`

3. **用包标识启动 Settings**
   - AUMID：`Microsoft.PowerToys.SparseApp!PowerToys.SettingsUI`
   - 预期：不再触发 `themeresources.xaml` 解析错误

## 可能的解决方向

### 方向 1：等待 Windows App SDK 修复

- 可能是 Windows App SDK 的已知限制或 bug
- 需要在 GitHub issues 中搜索或提交新 issue

### 方向 2：使用完整 MSIX 打包

- 不使用 sparse package，而是完整打包
- 影响：改变部署模型，增加复杂性

### 方向 3：创建非 WinUI 3 的 Helper 进程

- 创建一个 Console App 或 WPF App 作为 helper
- 该 helper 具有 package identity，专门调用 Semantic Search API
- Settings 通过 IPC 与 helper 通信
- 优点：不影响现有 Settings 架构

### 方向 4：进一步调查 ms-appx:/// 解析

- 研究是否有配置选项让 sparse package 正确解析框架资源
- 可能需要深入 Windows App SDK 源码或联系微软

### 方向 5：切换为 framework-dependent + Bootstrap（待验证）

- 设置 `WindowsAppSDKSelfContained=false` 并**重新构建** Settings
- 确保外部目录不再携带 app-local WinAppSDK 运行时
- 让 `Bootstrap.TryInitialize(...)` 生效，走框架包动态依赖

## 可复现的工作流（已验证 2026-01-25）

目标：Settings 使用 sparse identity 启动，WinUI 资源/字符串正常加载。

### 1) 构建 Settings（framework-dependent + bootstrap no-op）

在 `PowerToys.Settings.csproj` 中添加（仅在 `UseSparseIdentity=true` 时生效）：

```
<PropertyGroup Condition="'$(UseSparseIdentity)'=='true'">
  <WindowsAppSDKSelfContained>false</WindowsAppSDKSelfContained>
  <WindowsAppSDKBootstrapAutoInitializeOptions_OnPackageIdentity_NoOp>true</WindowsAppSDKBootstrapAutoInitializeOptions_OnPackageIdentity_NoOp>
</PropertyGroup>
```

构建：

```
MSBuild.exe src\settings-ui\Settings.UI\PowerToys.Settings.csproj /p:Platform=ARM64 /p:Configuration=Debug /p:UseSparseIdentity=true /m:1 /p:CL_MPCount=1 /nodeReuse:false
```

### 2) 清理 ExternalLocation 的 app-local WinAppSDK 运行时

**必须移除** app-local WinAppSDK 运行时文件，否则会优先加载并崩溃（`CoreMessagingXP.dll` / `0xc0000602`）。

需清理的目录：
- `ARM64\Debug`（ExternalLocation 根）
- `ARM64\Debug\WinUI3Apps`

建议只移除 app-local WinAppSDK 相关文件（保留业务 DLL）。

**保留/放回 bootstrap DLL（必要）：**
- `Microsoft.WindowsAppRuntime.Bootstrap.dll`
- `Microsoft.WindowsAppRuntime.Bootstrap.Net.dll`

### 3) 生成与包名一致的 resources.pri

关键点：resources.pri 的 **ResourceMap name 必须与包名一致**。

使用 `makepri.exe new` 生成，确保 `/mn` 指向 sparse 包的 `AppxManifest.xml`：

```
makepri.exe new ^
  /pr C:\PowerToys\src\settings-ui\Settings.UI ^
  /cf C:\PowerToys\src\settings-ui\Settings.UI\obj\ARM64\Debug\priconfig.xml ^
  /mn C:\PowerToys\src\PackageIdentity\AppxManifest.xml ^
  /of C:\PowerToys\ARM64\Debug\resources.pri ^
  /o
```

### 4) 将 resources.pri 打进 sparse MSIX

在 `BuildSparsePackage.ps1` 中把 `resources.pri` 放入 staging（脚本已更新）：
- 优先取 `ARM64\Debug\resources.pri`
- 如果不存在则回退 `ARM64\Debug\WinUI3Apps\PowerToys.Settings.pri`

重新打包：

```
.\src\PackageIdentity\BuildSparsePackage.ps1 -Platform ARM64 -Configuration Debug
```

### 5) 重新注册 sparse 包（如需先卸载）

如果因为内容变更被阻止，先卸载再安装：

```
Get-AppxPackage -Name Microsoft.PowerToys.SparseApp | Remove-AppxPackage
Add-AppxPackage -Path .\ARM64\Debug\PowerToysSparse.msix -ExternalLocation .\ARM64\Debug -ForceApplicationShutdown
```

### 6) 启动 Settings（验证）

```
Start-Process "shell:AppsFolder\Microsoft.PowerToys.SparseApp_djwsxzxb4ksa8!PowerToys.SettingsUI"
```

验证要点：
- Settings 正常启动，UI 文本显示
- 日志正常写入：`%LOCALAPPDATA%\Microsoft\PowerToys\Settings\Logs\0.0.1.0\`

### 备注（可选）

如果出现 `ms-appx:///CommunityToolkit...` 资源缺失，可将对应的 `.pri`（从 NuGet 缓存）复制到 `ARM64\Debug\WinUI3Apps`，但在 **resources.pri 已正确打包** 后通常不再需要。

## 待确认事项

1. [ ] WinUI 3 + Sparse Package 的兼容性问题是否有官方文档说明？
2. [ ] 是否有其他项目成功实现 WinUI 3 + Sparse Package？
3. [ ] Windows App SDK GitHub 上是否有相关 issue？
4. [ ] 修正依赖版本后，Settings 是否能在 sparse identity 下正常启动？
5. [ ] framework-dependent（Bootstrap）方式是否能在 sparse identity 下启动？

## 相关文件

- [SemanticSearchIndex.cs](../../src/common/Common.Search/SemanticSearch/SemanticSearchIndex.cs) - Semantic Search 实现
- [AppxManifest.xml](../../src/PackageIdentity/AppxManifest.xml) - Sparse package manifest
- [BuildSparsePackage.ps1](../../src/PackageIdentity/BuildSparsePackage.ps1) - 构建脚本
- [app.manifest](../../src/settings-ui/Settings.UI/app.manifest) - Settings 应用 manifest
- [PowerToys.Settings.csproj](../../src/settings-ui/Settings.UI/PowerToys.Settings.csproj) - Settings 项目文件

## 参考链接

- [Grant package identity by packaging with external location](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps)
- [Windows App SDK - Content Search API](https://learn.microsoft.com/en-us/windows/ai/apis/content-search)
- [Windows App SDK GitHub Issues](https://github.com/microsoft/WindowsAppSDK/issues)
