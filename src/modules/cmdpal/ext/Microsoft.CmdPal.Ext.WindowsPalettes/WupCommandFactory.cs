// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.UI.Palettes;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

internal enum WupCommandShape
{
    None,
    NoOp,
    Invoke,
    Children,
    SystemQuery,
    UnsupportedSystemInvoker,
}

internal static class WupCommandFactory
{
    internal static WupCommandShape GetShape(PaletteItemView item) => GetShape(item, out _);

    internal static ICommand Create(
        PaletteItemView item,
        IPaletteItemSourceExtension source,
        PaletteQuery? query,
        string sourceDisplayName,
        WupAnalyticsSession? analytics,
        string? commandId = null)
    {
        var shape = GetShape(item, out var invoker);
        return shape switch
        {
            WupCommandShape.Children => new WupChildrenPage(item, source, sourceDisplayName, commandId),
            WupCommandShape.SystemQuery => WupSystemQueryPage.Create(item, (PaletteSystemInvoker)invoker!, commandId),
            WupCommandShape.UnsupportedSystemInvoker => new WupUnsupportedInvokerCommand(item, ((PaletteSystemInvoker)invoker!).Kind, analytics, commandId),
            WupCommandShape.Invoke => new WupInvokerCommand(item, query, analytics, commandId),
            _ => new NoOpCommand(),
        };
    }

    private static WupCommandShape GetShape(PaletteItemView item, out PaletteInvoker? invoker)
    {
        invoker = null;
        if (item.Kinds.IsOfKind(KnownPaletteKinds.Container))
        {
            return WupCommandShape.Children;
        }

        try
        {
            invoker = item.TryGetInvoker();
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"TryGetInvoker failed for '{item.ExtensionId}:{item.Id}': 0x{ex.HResult:X8} {ex.Message}");
            return WupCommandShape.NoOp;
        }

        if (invoker is PaletteSystemInvoker systemInvoker)
        {
            return systemInvoker.Kind switch
            {
                SystemInvokerKind.Navigate => WupCommandShape.Children,
                SystemInvokerKind.Query => WupCommandShape.SystemQuery,
                _ => WupCommandShape.UnsupportedSystemInvoker,
            };
        }

        return invoker is null ? WupCommandShape.NoOp : WupCommandShape.Invoke;
    }
}
