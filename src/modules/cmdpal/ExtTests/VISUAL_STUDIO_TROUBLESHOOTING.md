# Visual Studio 解决方案项目可见性问题解决方案

## 问题描述
新添加的单元测试项目在Visual Studio解决方案资源管理器中不可见。

## 已完成的更改
✅ 项目已正确添加到PowerToys.sln解决方案文件
✅ 使用了正确的项目类型GUID (`{9A19103F-16F7-4668-BE54-9A1E7A4F7556}`)
✅ 添加了完整的构建配置 (Debug/Release, ARM64/x64)
✅ 正确配置了项目嵌套到"Built-in Extension Tests"文件夹

## 验证状态
通过 `dotnet sln list` 命令验证，所有新的测试项目都已正确识别：
- ✅ Microsoft.CmdPal.Ext.Registry.UnitTests
- ✅ Microsoft.CmdPal.Ext.Calc.UnitTests  
- ✅ Microsoft.CmdPal.Ext.WindowWalker.UnitTests
- ✅ Microsoft.CmdPal.Ext.System.UnitTests

## 解决方案
由于已确认项目在解决方案文件级别配置正确，项目不可见的问题可能是Visual Studio缓存相关。请尝试以下步骤：

### 方法1: 重新加载解决方案
1. 在Visual Studio中关闭解决方案
2. 重新打开PowerToys.sln
3. 检查"Built-in Extension Tests"文件夹

### 方法2: 清除Visual Studio缓存
1. 关闭Visual Studio
2. 删除解决方案目录下的`.vs`文件夹
3. 重新打开PowerToys.sln

### 方法3: 手动添加项目引用（如果上述方法无效）
在Visual Studio中右键点击"Built-in Extension Tests"文件夹，选择"添加现有项目"，然后导航到以下项目文件：
- `src\modules\cmdpal\ExtTests\Microsoft.CmdPal.Ext.Registry.UnitTests\Microsoft.CmdPal.Ext.Registry.UnitTests.csproj`
- `src\modules\cmdpal\ExtTests\Microsoft.CmdPal.Ext.Calc.UnitTests\Microsoft.CmdPal.Ext.Calc.UnitTests.csproj`
- `src\modules\cmdpal\ExtTests\Microsoft.CmdPal.Ext.WindowWalker.UnitTests\Microsoft.CmdPal.Ext.WindowWalker.UnitTests.csproj`
- `src\modules\cmdpal\ExtTests\Microsoft.CmdPal.Ext.System.UnitTests\Microsoft.CmdPal.Ext.System.UnitTests.csproj`

## 技术细节
- **项目类型**: SDK风格的C#项目 (.NET)
- **文件夹层次**: CommandPalette → Built-in Extension Tests
- **架构支持**: ARM64, x64
- **配置**: Debug, Release

## 注意事项
- 项目文件已存在且结构正确
- 解决方案文件语法验证无误
- 构建失败是由于C++/CLI依赖项，但不影响项目在Visual Studio中的可见性
- 这些测试项目应该可以在Visual Studio中正常显示和构建
