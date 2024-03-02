# Telemetry

Following telemetry is sent by File Actions menu:

|Name|Thrown when...|Properties|
|----|-----------|----------|
| `FileActionsMenuInvokedEvent` | The menu is invoked. | **LoadedPluginsCount**: How many plugins were loaded. |
| `FileActionsMenuProgressConflictEvent` | A progress window is open and a conflit was solved. | **ReplaceChoosen**: `true` when the replace option got choosen, otherwise `false`
|||
| `FileActionsMenuItemInvokedEvent` | Never, but is the base type for all the following events. | **ItemCount**: How many items are selected.<br/> **HasExecutableFilesSelected**: If any files ending in `.exe` or `.dll` are selected.<br/>**HasFilesSelected**: If the selection contains any files.<br/>**HasFolderSelected**: If the selection contains any folders.<br/>**HasImageSelected**: If the selection contains any image files.|
| `FileActionsMenuUninstallActionInvoked` | When the uninstall action is called. | **IsCalledFromDesktop**: If the selected item was on the desktop. |
| `FileActionsMenuPowerRenameAction` | When PowerRename is launched via File Actions Menu | |
| `FileActionsMenuImageResizerAction` | When Image Resizer is launched via File Actions Menu | |
| `FileActionsMenuFileLocksmithAction` | When File Locksmith is launched via File Actions Menu | |
| `FileActionsMenuCopyContentAsCEscapedStringActionInvokedEvent` | When the "Copy file content → As C escaped string" action is invoked | |
| `FileActionsMenuCopyContentAsDataUrlActionInvokedEvent` | When the "Copy file content → As data url" action is invoked | |
| `FileActionsMenuCopyContentAsPlaintextActionInvokedEvent` | When the "Copy file content → As plaintext" action is invoked | |
| `FileActionsMenuCopyContentAsUriEncodedActionInvokedEvent` | When the "Copy file content → As URI encoded string" action is invoked | |
| `FileActionsMenuCopyContentAsXmlEncodedActionInvokedEvent` | When the "Copy file content → As XML encoded string" action is invoked | |
| `FileActionsMenuCollapseFolderActionInvokedEvent` | When the collapse folder action is invoked | **CollapsedFilesCount**: The number of files in the folder that was collapsed. |
| `FileActionsMenuCopyFolderTreeActionInvokedEvent` | When the copy folder tree action is invoked | **IsDriveRoot**: Whetever the selected folder is a drive or not. |
| `FileActionsMenuMergeContentActionInvokedEvent ` | When the Merge content action is invoked | **HasDifferentExtensions**: Whetever the merged files had different file extensions or not.