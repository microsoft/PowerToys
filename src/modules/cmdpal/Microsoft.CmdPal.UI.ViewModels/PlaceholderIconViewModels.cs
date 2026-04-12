// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public static class PlaceholderIconViewModels
{
    private const string ExtensionPlaceholderIconPath = "Assets\\Icons\\ExtensionIconPlaceholder.png";
    private const string GenericPlaceholderIconPath = "Assets\\Icons\\GenericIconPlaceholder.png";
    private const string CommandPlaceholderIconPath = GenericPlaceholderIconPath;
    private const string FallbackCommandPlaceholderIconPath = GenericPlaceholderIconPath;

    public static IconInfoViewModel ExtensionIcon { get; } = CreatePlaceholderIcon(ExtensionPlaceholderIconPath);

    public static IconInfoViewModel GenericPlaceholderIcon { get; } = CreatePlaceholderIcon(GenericPlaceholderIconPath);

    public static IconInfoViewModel CommandIcon { get; } = CreatePlaceholderIcon(CommandPlaceholderIconPath);

    public static IconInfoViewModel FallbackCommandIcon { get; } = CreatePlaceholderIcon(FallbackCommandPlaceholderIconPath);

    private static IconInfoViewModel CreatePlaceholderIcon(string relativePath)
    {
        var icon = new IconInfoViewModel(IconHelpers.FromRelativePath(relativePath));
        icon.InitializeProperties();
        return icon;
    }
}
