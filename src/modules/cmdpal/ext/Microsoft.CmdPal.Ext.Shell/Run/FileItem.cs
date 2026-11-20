// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Shell;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Run;

/// <summary>
/// Helper class for both directory paths and executables.
/// Sets this list item up so that its icon will come from the file path.
/// </summary>
internal abstract partial class FileItem : ListItem, IFileItem
{
    private readonly Lazy<IconInfo?> _icon;

    public override IIconInfo? Icon { get => _icon.Value; set => throw new NotImplementedException("Hey you shouldn't be setting the Icon on a FileItem"); }

    public string FullPath { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileItem"/> class.
    /// Create a new instance of a FileItem, which can lazy-load the icon for
    /// the file at the given `fullPath`
    /// </summary>
    /// <param name="fullPath">The path of the file this item represents. We'll pull the icon from it.</param>
    /// <param name="showFileIcon">false to show the Run icon instead, such as for a path that doesn't exist</param>
    internal FileItem(string fullPath, bool showFileIcon = true)
    {
        FullPath = fullPath;
        _icon = new Lazy<IconInfo?>(() => showFileIcon && ResolveIconPath(fullPath) is { } iconPath ? new IconInfo(ShellItemIconProtocol.Create(iconPath)) : Icons.RunGeneric);
    }

    private static string? ResolveIconPath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return null;
        }

        // Normalizing a full path would break \\?\ paths and look up short names on disk.
        var expanded = Environment.ExpandEnvironmentVariables(fullPath);
        if (Path.IsPathFullyQualified(expanded))
        {
            return expanded;
        }

        // Resolving Z:tool.exe checks that drive's saved directory, which can stall on a mapped drive.
        if (expanded is [_, ':', ..])
        {
            return null;
        }

        return Path.GetFullPath(expanded);
    }
}

internal interface IFileItem
{
    string FullPath { get; }
}
