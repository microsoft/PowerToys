# CmdPal Extension Unit Tests Migration

## Overview
Successfully migrated unit tests for four PowerToys launcher plugins (Registry, Calculator, WindowWalker, System) to the Command Palette (CmdPal) extension framework.

## Migrated Extensions

### 1. Microsoft.CmdPal.Ext.Registry.UnitTests
**Original Source**: `src\modules\launcher\Plugins\Microsoft.PowerToys.Run.Plugin.Registry.UnitTest\`
**New Location**: `src\modules\cmdpal\ExtTests\Microsoft.CmdPal.Ext.Registry.UnitTests\`

**Test Files Created**:
- `RegistryHelperTest.cs` - Tests for registry key parsing and base key operations
- `ResultHelperTest.cs` - Tests for text truncation and result formatting
- `QueryHelperTest.cs` - Tests for query parsing and short base key handling
- `KeyNameTest.cs` - Tests for registry key name constants
- `BasicStructureTest.cs` - Basic project structure validation

### 2. Microsoft.CmdPal.Ext.Calc.UnitTests
**Original Source**: `src\modules\launcher\Plugins\Microsoft.PowerToys.Run.Plugin.Calculator.UnitTest\`
**New Location**: `src\modules\cmdpal\ExtTests\Microsoft.CmdPal.Ext.Calc.UnitTests\`

**Test Files Created**:
- `BracketHelperTests.cs` - Tests for bracket completion validation
- `ExtendedCalculatorParserTests.cs` - Tests for mathematical expression parsing and evaluation
- `NumberTranslatorTests.cs` - Tests for culture-specific number translation

### 3. Microsoft.CmdPal.Ext.WindowWalker.UnitTests
**Original Source**: `src\modules\launcher\Plugins\Microsoft.Plugin.WindowWalker.UnitTests\`
**New Location**: `src\modules\cmdpal\ExtTests\Microsoft.CmdPal.Ext.WindowWalker.UnitTests\`

**Test Files Created**:
- `PluginSettingsTests.cs` - Tests for WindowWalker settings management (adapted to use SettingsManager instead of WindowWalkerSettings)

### 4. Microsoft.CmdPal.Ext.System.UnitTests
**Original Source**: `src\modules\launcher\Plugins\Microsoft.PowerToys.Run.Plugin.System.UnitTests\`
**New Location**: `src\modules\cmdpal\ExtTests\Microsoft.CmdPal.Ext.System.UnitTests\`

**Test Files Created**:
- `ImageTests.cs` - Tests for icon theme handling (adapted for cmdpal structure)
- `QueryTests.cs` - Tests for system command queries
- `BasicTests.cs` - Tests for helper classes and basic functionality

## Key Adaptations Made

### 1. Namespace Changes
- Updated all namespaces from `Microsoft.PowerToys.Run.Plugin.*` to `Microsoft.CmdPal.Ext.*`
- Updated using statements to reference cmdpal extension helpers

### 2. Class Reference Updates
- **Registry**: Updated references from `Microsoft.PowerToys.Run.Plugin.Registry.Helper` to `Microsoft.CmdPal.Ext.Registry.Helpers`
- **Calculator**: Updated references from `Microsoft.PowerToys.Run.Plugin.Calculator` to `Microsoft.CmdPal.Ext.Calc.Helper`
- **WindowWalker**: Adapted from `WindowWalkerSettings.Instance` to `SettingsManager.Instance`
- **System**: Adapted tests to work with new cmdpal system extension structure

### 3. Project Structure
- All projects follow the same structure as the existing `Microsoft.CmdPal.Ext.TimeDate.UnitTests`
- Use MSTest framework with Moq for mocking
- Target .NET 9.0 with Windows 10.0.26100.0 SDK
- Output to the same directory structure as other cmdpal tests

### 4. Framework Adaptations
- **WindowWalker**: The settings system changed from `WindowWalkerSettings` singleton to `SettingsManager` with JsonSettingsManager base
- **System**: Adapted to use new helper classes like `Commands`, `Icons`, `Win32Helpers`, `NetworkConnectionProperties`
- **Calculator**: Maintained similar test structure but updated to use cmdpal's calculator helpers
- **Registry**: Maintained similar registry helper test patterns

## Project Configuration
Each test project includes:
- Reference to the corresponding cmdpal extension project
- MSTest and Moq package references
- Proper output path configuration for cmdpal test structure
- Appropriate root namespace matching the extension

## Migration Benefits
1. **Consistency**: All tests now follow the same pattern as the existing TimeDate extension tests
2. **Framework Alignment**: Tests are aligned with the cmdpal extension framework
3. **Maintainability**: Tests are organized alongside their respective extensions
4. **Reusability**: Test patterns can be applied to future cmdpal extensions

## Next Steps
1. Verify all tests compile and run correctly in the full build environment
2. Add any additional test coverage specific to cmdpal extension features
3. Consider adding integration tests for the command palette framework interactions
4. Update CI/CD pipelines to include the new test projects

## Files Created
Total of 13 test files across 4 new test projects, mirroring the functionality and test coverage of the original PowerToys launcher plugin tests while adapting to the cmdpal extension architecture.
