#### [`dllmain.cpp`](/src/modules/powerrename/dll/dllmain.cpp)
# PowerRename Module for PowerToys

The **PowerRename Module** is a component of [PowerToys](https://github.com/microsoft/PowerToys), an open-source utility that provides a set of system utilities for Windows. This module is specifically designed to enhance the file and folder renaming capabilities of Windows Explorer.

## Features
- **Advanced Renaming Options:** PowerRename introduces advanced renaming options, allowing users to efficiently rename multiple files and folders with ease.
- **Context Menu Integration:** Seamlessly integrates with the Windows Explorer context menu for quick and convenient access to renaming functionality.
- **Persistent Settings:** Allows users to customize and save their preferred renaming configurations.

## Usage
1. Right-click on a file or folder in Windows Explorer.
2. Select the "Power Rename" option from the context menu.
3. Configure the desired renaming options in the PowerRename window.
4. Apply the changes to rename the selected files or folders.

## Configuration
The PowerRename Module provides a range of configuration options, including:
- **Persist Input:** Choose whether to restore the search query upon reopening the PowerRename window.
- **MRU (Most Recently Used) Enabled:** Enable or disable the Most Recently Used list for quick access to previous renaming patterns.
- **Maximum MRU Size:** Set the maximum number of items to display in the Most Recently Used list.
- **Show Icon on Menu:** Toggle the display of the PowerRename icon on the context menu.
- **Show Extended Menu:** Determine whether to show the extended menu only in specific contexts.
- **Use Boost Library:** Enable or disable the use of the Boost C++ Libraries for enhanced functionality.




#### [`PowerRenameExt.cpp`](/src/modules/powerrename/dll/PowerRenameExt.cpp)
# PowerRename Shell Extension for PowerToys

The **PowerRename Shell Extension** is a component of [PowerToys](https://github.com/microsoft/PowerToys), an open-source utility that enhances the Windows operating system. This extension integrates into the Windows Explorer context menu, providing advanced file and folder renaming capabilities.

## Features
- **Context Menu Integration:** Adds a "PowerRename" option to the Windows Explorer context menu for efficient file and folder renaming.
- **Streamlined Renaming:** Streamlines the renaming process with a dedicated PowerRename window.
- **Customizable Options:** Configurable options include persisting input, enabling Most Recently Used (MRU) lists, and more.




#### [`Helpers.cpp`](/src/modules/powerrename/lib/Helpers.cpp)
# PowerRename Utility

PowerRename is a C++ utility that provides various functions for file and folder manipulation, string transformation, and registry operations. It is designed to be a flexible tool for handling file and folder names according to user-defined rules.

## Table of Contents

- [File and Folder Manipulation](#file-and-folder-manipulation)
- [String Transformation](#string-transformation)
- [Registry Operations](#registry-operations)
- [Shell Item Operations](#shell-item-operations)
- [Enumeration and Naming](#enumeration-and-naming)
- [Windows GUI Operations](#windows-gui-operations)
- [Miscellaneous](#miscellaneous)
- [Registry Keys and Values](#registry-keys-and-values)
- [Usage](#usage)
- [Localization](#localization)

## File and Folder Manipulation

1. **GetTrimmedFileName**: Trims leading and trailing whitespaces and dots from the given file path.
2. **GetTransformedFileName**: Transforms the file name based on specified flags (uppercase, lowercase, title case, capitalized).
3. **GetDatedFileName**: Formats a file name based on a specified date and time.

## String Transformation

These functions transform strings based on specified flags, such as uppercase, lowercase, title case, and capitalized.

## Registry Operations

1. **GetRegString**: Reads a string value from the Windows Registry.
2. **GetRegNumber**, **SetRegNumber**: Reads and sets integer values in the Registry.
3. **GetRegBoolean**, **SetRegBoolean**: Reads and sets boolean values in the Registry.

## Shell Item Operations

- **GetShellItemArrayFromDataObject**: Retrieves a shell item array from a data object.

## Enumeration and Naming

1. **GetEnumeratedFileName**: Generates a unique file name based on a template, directory, and minimum number.
2. **ShellItemArrayContainsRenamableItem**: Checks if the shell item array contains at least one item that can be renamed.
3. **DataObjectContainsRenamableItem**: Checks if the data object contains at least one item that can be renamed.

## Windows GUI Operations

- **CreateMsgWindow**: Creates a message window with a specified window procedure.

## Miscellaneous

- **isFileTimeUsed**: Checks if a file name template uses date and time patterns.

## Registry Keys and Values

- **c_rootRegPath**: Root path in the Registry where certain settings are stored.

## Usage

Provide information on how to compile and use this utility. Include any dependencies or specific build instructions if necessary.

## Localization

The code sets the global locale to an empty locale, potentially for consistent behavior across different systems.

Feel free to contribute to this project by opening issues or submitting


#### [`PowerRenameItem.cpp`](/src/modules/powerrename/lib/PowerRenameItem.cpp)
# PowerRename Utility

PowerRename is a C++ utility that provides file and folder manipulation capabilities, string transformation, and registry operations. It is designed to be a flexible tool for handling file and folder names according to user-defined rules.

## Features

- **File and Folder Manipulation**: Functions for trimming, transforming, and formatting file names.
- **String Transformation**: String manipulation based on specified flags (uppercase, lowercase, title case, capitalized).
- **Registry Operations**: Read and write operations for Windows Registry.
- **Shell Item Operations**: Operations related to shell items.
- **Enumeration and Naming**: Generate unique file names and check for renamable items.
- **Windows GUI Operations**: GUI-related functions.

## Implementation Details

The `CPowerRenameItem` class provides an implementation for the `IPowerRenameItem` and `IPowerRenameItemFactory` interfaces. Here's a brief overview of its functionality:

- **AddRef / Release**: Manage object references.
- **QueryInterface**: Implementing interfaces for `IPowerRenameItem` and `IPowerRenameItemFactory`.
- **PutPath / GetPath**: Set and retrieve the file path.
- **GetTime**: Retrieve the creation time of the file.
- **GetShellItem**: Retrieve a shell item from the file path.
- **PutOriginalName / GetOriginalName**: Set and retrieve the original file name.
- **PutNewName / GetNewName**: Set and retrieve the new file name.
- **GetIsFolder / GetIsSubFolderContent**: Check if the item is a folder or part of subfolder content.
- **GetSelected / PutSelected**: Check if the item is selected and set its selection status.
- **GetId / GetDepth / PutDepth**: Retrieve the item's ID, depth, and set its depth.
- **GetStatus / PutStatus**: Retrieve and set the rename status.
- **ShouldRenameItem**: Check if the item should be renamed based on specified flags.
- **IsItemVisible**: Check if the item is visible based on specified filters and flags.
- **Reset**: Reset the new name.

#### [`PowerRenameRegEx.cpp`](/src/modules/powerrename/lib/PowerRenameRegEx.cpp)
# PowerRenameRegEx

PowerRenameRegEx is a C++ utility that provides regular expression-based search and replace functionality for file and folder names. It is designed to offer flexible and powerful renaming capabilities using regular expressions.

## Features

- **Search and Replace**: Use regular expressions to find and replace text in file and folder names.
- **Flags and Options**: Customize the renaming process with various flags and options.
- **Event Notification**: Notify subscribers about changes in search terms, replace terms, flags, and file time.

## Usage

```cpp
// Instantiate the PowerRenameRegEx object
IPowerRenameRegEx* renameRegEx;
CPowerRenameRegEx::s_CreateInstance(&renameRegEx);

// Set search term
renameRegEx->PutSearchTerm(L"old_text", false);

// Set replace term
renameRegEx->PutReplaceTerm(L"new_text", false);

// Set flags (use bitwise OR for multiple flags)
renameRegEx->PutFlags(EnumFlags::UseRegularExpressions | EnumFlags::CaseSensitive);

// Perform replace on a file name
PWSTR result;
unsigned long enumIndex = 0;
renameRegEx->Replace(L"old_text_file.txt", &result, enumIndex);
// result now contains the modified file name


#### [`Settings.cpp`](/src/modules/powerrename/lib/Settings.cpp)
# PowerRenameRegEx

PowerRenameRegEx is a C++ utility that provides regular expression-based search and replace functionality for file and folder names. It is designed to offer flexible and powerful renaming capabilities using regular expressions.

## Features

- **Search and Replace**: Use regular expressions to find and replace text in file and folder names.
- **Flags and Options**: Customize the renaming process with various flags and options.
- **Event Notification**: Notify subscribers about changes in search terms, replace terms, flags, and file time.

## Usage

```cpp
// Instantiate the PowerRenameRegEx object
IPowerRenameRegEx* renameRegEx;
CPowerRenameRegEx::s_CreateInstance(&renameRegEx);

// Set search term
renameRegEx->PutSearchTerm(L"old_text", false);

// Set replace term
renameRegEx->PutReplaceTerm(L"new_text", false);

// Set flags (use bitwise OR for multiple flags)
renameRegEx->PutFlags(EnumFlags::UseRegularExpressions | EnumFlags::CaseSensitive);

// Perform replace on a file name
PWSTR result;
unsigned long enumIndex = 0;
renameRegEx->Replace(L"old_text_file.txt", &result, enumIndex);
// result now contains the modified file name


#### [`trace.cpp`](/src/modules/powerrename/lib/trace.cpp)
#include "pch.h"
#include "trace.h"
#include "Settings.h"

TRACELOGGING_DEFINE_PROVIDER(
      g_hProvider,
      "Microsoft.PowerToys",
      // {38e8889b-9731-53f5-e901-e8a7c1753074}
      (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
      TraceLoggingOptionProjectTelemetry());

void Trace::RegisterProvider() noexcept
{
    TraceLoggingRegister(g_hProvider);
}

void Trace::UnregisterProvider() noexcept
{
    TraceLoggingUnregister(g_hProvider);
}

void Trace::Invoked() noexcept
{
  TraceLoggingWrite(
        g_hProvider,
        "PowerRename_Invoked",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::InvokedRet(_In_ HRESULT hr) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerRename_InvokedRet",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingHResult(hr),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::EnablePowerRename(_In_ bool enabled) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerRename_EnablePowerRename",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

void Trace::UIShownRet(_In_ HRESULT hr) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerRename_UIShownRet",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingHResult(hr),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::RenameOperation(_In_ UINT totalItemCount, _In_ UINT selectedItemCount, _In_ UINT renameItemCount, _In_ DWORD flags, _In_ PCWSTR extensionList) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerRename_RenameOperation",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingUInt32(totalItemCount, "TotalItemCount"),
        TraceLoggingUInt32(selectedItemCount, "SelectedItemCount"),
        TraceLoggingUInt32(renameItemCount, "RenameItemCount"),
        TraceLoggingInt32(flags, "Flags"),
        TraceLoggingWideString(extensionList, "ExtensionList"));
}

void Trace::SettingsChanged() noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerRename_SettingsChanged",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(CSettingsInstance().GetEnabled(), "IsEnabled"),
        TraceLoggingBoolean(CSettingsInstance().GetShowIconOnMenu(), "ShowIconOnMenu"),
        TraceLoggingBoolean(CSettingsInstance().GetExtendedContextMenuOnly(), "ExtendedContextMenuOnly"),
        TraceLoggingBoolean(CSettingsInstance().GetPersistState(), "PersistState"),
        TraceLoggingBoolean(CSettingsInstance().GetMRUEnabled(), "IsMRUEnabled"),
        TraceLoggingUInt64(CSettingsInstance().GetMaxMRUSize(), "MaxMRUSize"),
        TraceLoggingBoolean(CSettingsInstance().GetUseBoostLib(), "UseBoostLib"),
        TraceLoggingUInt64(CSettingsInstance().GetFlags(), "Flags"));
}
