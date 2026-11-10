// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.UI.Palettes;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

internal sealed record WupSourceDescriptor(string Id, string DisplayName, IconInfo Icon)
{
    internal static WupSourceDescriptor Create(IPaletteExtensionDefinition definition)
    {
        var extensionId = definition.ExtensionId;
        var id = $"wup:{definition.PackageFamilyName}:{definition.ApplicationId}:{extensionId}";
        string displayName;
        try
        {
            displayName = definition.DisplayName;
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Display name unavailable for '{extensionId}': 0x{ex.HResult:X8}.");
            displayName = extensionId;
        }

        PaletteIconData? icon;
        try
        {
            icon = definition.Icon;
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Icon unavailable for '{extensionId}': 0x{ex.HResult:X8}.");
            icon = null;
        }

        return new(id, string.IsNullOrWhiteSpace(displayName) ? extensionId : displayName, WupIconAdapter.Create(icon));
    }
}
