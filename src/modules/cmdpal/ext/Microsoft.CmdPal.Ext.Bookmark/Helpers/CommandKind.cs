// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

/// <summary>
/// Classifies a command or bookmark target type.
/// </summary>
public enum CommandKind
{
    /// <summary>
    /// Unknown or unsupported target.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// HTTP/HTTPS URL.
    /// </summary>
    WebUrl,

    /// <summary>
    /// Any non-file URI scheme (e.g., mailto:, ms-settings:, wt:, myapp:).
    /// </summary>
    Protocol,

    /// <summary>
    /// Application User Model ID (e.g., shell:AppsFolder\AUMID or pkgfamily!app).
    /// </summary>
    Aumid,

    /// <summary>
    /// Existing folder path.
    /// </summary>
    Directory,

    /// <summary>
    /// Existing executable file (e.g., .exe, .bat, .cmd).
    /// </summary>
    FileExecutable,

    /// <summary>
    /// Existing document file.
    /// </summary>
    FileDocument,

    /// <summary>
    /// Windows shortcut file (*.lnk).
    /// </summary>
    Shortcut,

    /// <summary>
    /// Internet shortcut file (*.url).
    /// </summary>
    InternetShortcut,

    /// <summary>
    /// Bare command resolved via PATH/PATHEXT (e.g., "wt", "git").
    /// </summary>
    PathCommand,

    /// <summary>
    /// Shell item not matching other types (e.g., Control Panel item, purely virtual directory).
    /// </summary>
    VirtualShellItem,
}
