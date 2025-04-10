# Telemetry

Following telemetry is sent by File Actions menu:

|Name|Thrown when...|Properties|
|----|-----------|----------|
| `FileActionsMenuInvokedEvent` | The menu is invoked. | **LoadedPluginsCount**: How many plugins were loaded. |
| `FileActionsMenuProgressConflictEvent` | A progress window is open and a conflict was solved. | **ReplaceChosen**: `true` when the replace option got Chosen, otherwise `false`
|||
| `IFileActionsMenuItemInvokedEvent` | Never, but is the base type for all the following events. | **ItemCount**: How many items are selected.<br/> **HasExecutableFilesSelected**: If any files ending in `.exe` or `.dll` are selected.<br/>**HasFilesSelected**: If the selection contains any files.<br/>**HasFolderSelected**: If the selection contains any folders.<br/>**HasImageSelected**: If the selection contains any image files.|
| `FileActionsMenuUninstallActionInvoked` | When the uninstall action is called. | **IsCalledFromDesktop**: If the selected item was on the desktop.<br/> **IsCalledOnShortcut**: If the selected item is a shortcut (`.ink` file). |
| `FileActionsMenuPowerRenameAction` | When PowerRename is launched via File Actions menu | |
| `FileActionsMenuImageResizerAction` | When Image Resizer is launched via File Actions menu | |
| `FileActionsMenuFileLocksmithAction` | When File Locksmith is launched via File Actions menu | |
| `FileActionsMenuCopyContentAsCEscapedStringActionInvokedEvent` | When the "Copy file content → As C escaped string" action is invoked | |
| `FileActionsMenuCopyContentAsDataUrlActionInvokedEvent` | When the "Copy file content → As data url" action is invoked | |
| `FileActionsMenuCopyContentAsPlaintextActionInvokedEvent` | When the "Copy file content → As plaintext" action is invoked | |
| `FileActionsMenuCopyContentAsUriEncodedActionInvokedEvent` | When the "Copy file content → As URI encoded string" action is invoked | |
| `FileActionsMenuCopyContentAsXmlEncodedActionInvokedEvent` | When the "Copy file content → As XML encoded string" action is invoked | |
| `FileActionsMenuCollapseFolderActionInvokedEvent` | When the "Collapse folder" action is invoked | **CollapsedFilesCount**: The number of files in the folder that was collapsed. |
| `FileActionsMenuCopyFolderTreeActionInvokedEvent` | When the "Copy folder tree" action is invoked | **IsDriveRoot**: If the selected folder is a drive or not. |
| `FileActionsMenuMergeContentActionInvokedEvent ` | When the "Merge content" action is invoked | **HasDifferentExtensions**: If the merged files had different file extensions or not.
| `FileActionsMenuUnblockFilesActionInvokedEvent` | When the "Unblock files" action is called. | |
| `FileActionsMenuVerifyHashesActionInvokedEvent` | When a Verify Checksum action is called. | **HashType**: The hash algorithm used.<br />**VerifyMode**: The mode used to get the comparing hash. |
| `FileActionsMenuVerifyHashesActionInvokedEvent` | When a Generate Checksum action is called. | **HashType**: The hash algorithm used.<br />**GenerateMode**: The mode used to save the generated hash. |
| `FileActionsMenuCopyImageToClipboardActionInvokedEvent` | When the "Copy image to clipboard" action is invoked | |
| `FileActionsMenuCopyImageFromClipboardActionInvokedEvent` | When the "Copy image from clipboard to folder" action is invoked | |
| `FileActionsMenuCopyDirectoryPathActionInvokedEvent` | When the "Copy path of the containing folder" or "Copy WSL path of the containing folder" action is invoked | **IsWSLMode**: If the WSL path was copied.<br/>**ResolveShortcut**: If the resolve shortcut option is activated. |
| `FileActionsMenuCopyFilePathActionInvokedEvent` | When the "Copy full path" or "Copy full WSL path" action is invoked | **IsWSLMode**: If the WSL path was copied.<br/>**ResolveShortcut**: If the resolve shortcut option is activated.<br/>**Delimiter**: Which path delimiter was used. |
| `FileActionsMenuCopyFullPathActionInvokedEvent` | When the "Copy full path" action is invoked | |

## Hash generating/verifying types

### `HashType`

| Number | Hash algorithm |
|--------|---------------|
|    0   | MD5           |
|    1   | SHA1          |
|    2   | SHA256        |
|    3   | SHA384        |
|    4   | SHA512        |
|    5   | SHA3-256      |
|    6   | SHA3-384      |
|    7   | SHA3-512      |
|    8   | CRC32 Hex     |
|    9   | CRC32 Decimal |
|   10   | CRC64 Hex     |
|   11   | CRC64 Decimal |

### `VerifyMode` and `GenerateMode`

| Number | Mode           |
|--------|----------------|
|    0   | Single file    |
|    1   | Multiple files |
|    2   | Filename       |
|    3   | Clipboard      |

## Sending telemetry events

Telemetry events that implement `FileActionsMenuProgressConflictEvent` are sent with the `TelemetryHelper.LogEvent` method. The `selectedItems` parameter is an array of the selected items. For more information, see the [Helpers docs](Helpers.md#void-logeventtt-e-string-selecteditems-where-t--eventbase-ifileactionsmenuiteminvokedevent).
