// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.FileLocksmith.UITests;

/// <summary>
/// Names File Locksmith exposes to the outside world: its module key, its UI process/window, and the
/// caption both shell extensions register into the Explorer context menu.
/// </summary>
internal static class FileLocksmithConstants
{
    public const string ModuleName = "File Locksmith";
    public const string PowerRenameModuleName = "PowerRename";
    public const string UiProcessName = "PowerToys.FileLocksmithUI";
    public const string UiExecutableName = "PowerToys.FileLocksmithUI.exe";
    public const string ContextMenuCaption = "Unlock with File Locksmith";

    /// <summary>Sibling PowerToys command used to prove the menu itself still renders.</summary>
    public const string PowerRenameContextMenuCaption = "Rename with PowerRename";

    public const string WindowTitle = "File Locksmith";
    public const string ElevatedWindowTitle = "Administrator: File Locksmith";

    public const string ProcessListAutomationId = "ProcessesListView";
    public const string ReloadAutomationId = "ReloadBtn";
    public const string RestartAsAdminAutomationId = "RestartAsAdminBtn";
    public const string EndTaskCaption = "End task";
    public const string EmptyListCaption = "No results";
}
