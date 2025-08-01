# PowerToys CmdPal AOT 分析工具

这个工具包用于分析 PowerToys CmdPal 在启用 AOT (Ahead-of-Time) 编译时被移除的类型。

## 核心文件

### 分析工具 (`tools/TrimmingAnalyzer/`)
- `TrimmingAnalyzer.csproj` - 项目文件
- `Program.cs` - 主程序入口
- `TypeAnalyzer.cs` - 程序集类型分析引擎
- `ReportGenerator.cs` - 报告生成器 (Markdown/XML/JSON)

### 脚本文件 (`tools/build/`)
- `Generate-CmdPalTrimmingReport.ps1` - 主要分析脚本（包含完整说明和自动化流程）

## 使用方法

### 前提条件
- Visual Studio 2022 with C++ workload
- Windows SDK
- 使用 Developer Command Prompt for VS 2022

### 运行分析
```powershell
cd C:\Users\yuleng\PowerToys
.\tools\build\Generate-CmdPalTrimmingReport.ps1
```

### 输出报告
- `TrimmedTypes.md` - 人类可读的 Markdown 报告
- `TrimmedTypes.rd.xml` - 运行时指令以保留类型
- JSON 格式的分析数据

## 分析原理

1. **Debug 构建** - 不启用 AOT，保留所有类型
2. **Release 构建** - 启用 AOT，移除未使用的类型
3. **程序集比较** - 识别被 AOT 优化移除的类型
4. **报告生成** - 生成详细的优化效果报告

## 价值

- 显示 AOT 优化的有效性
- 识别被消除的未使用代码
- 帮助理解二进制大小减少
- 协助排查运行时反射问题
- 为性能优化决策提供数据

---

*工具状态: 已完成，等待 Visual Studio C++ 构建环境配置后即可使用*
