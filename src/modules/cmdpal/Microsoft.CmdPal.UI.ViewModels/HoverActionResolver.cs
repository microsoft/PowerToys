// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public static class HoverActionResolver
{
    public const int DefaultFirstN = 3;

    public static IReadOnlyList<CommandContextItemViewModel> Resolve(HoverActionResolveContext ctx)
    {
        if (!ctx.EnableListHoverActions)
        {
            return [];
        }

        var mode = ResolveEffectiveMode(ctx);
        if (mode == HoverActionsMode.None)
        {
            return [];
        }

        var visible = ctx.Commands.Where(static c => c.ShouldBeVisible).ToList();
        if (visible.Count == 0)
        {
            return [];
        }

        var candidates = FilterHomeHostCommands(ctx.IsHomeSurface, visible);

        return mode switch
        {
            HoverActionsMode.Explicit => candidates
                .Where(static c => c.ShowInHoverActions)
                .OrderBy(static c => c.HoverOrder)
                .ToArray(),
            HoverActionsMode.AllMoreCommands => candidates,
            _ => TakeFirstN(candidates, ctx.MaxHoverActions),
        };
    }

    public static bool ShouldShowHoverStrip(HoverActionResolveContext ctx, bool hasHoverActions)
    {
        if (!ctx.EnableListHoverActions || !hasHoverActions)
        {
            return false;
        }

        var visibility = ctx.Visibility;
        if (visibility == HoverActionsVisibility.Default)
        {
            visibility = ctx.IsHomeSurface
                ? HoverActionsVisibility.OnHoverOnly
                : HoverActionsVisibility.HoverOrSelected;
        }

        return visibility switch
        {
            HoverActionsVisibility.OnHoverOnly => ctx.IsRowHovered,
            HoverActionsVisibility.HoverOrSelected => ctx.IsRowHovered || ctx.IsListSelected,
            _ => ctx.IsRowHovered || ctx.IsListSelected,
        };
    }

    private static HoverActionsMode ResolveEffectiveMode(HoverActionResolveContext ctx)
    {
        var mode = ctx.Mode;
        if (mode == HoverActionsMode.Default)
        {
            mode = HoverActionsMode.FirstN;
        }

        if (mode is HoverActionsMode.FirstN &&
            ctx.Commands.Any(static c => c.ShouldBeVisible && c.ShowInHoverActions))
        {
            return HoverActionsMode.Explicit;
        }

        return mode;
    }

    private static List<CommandContextItemViewModel> FilterHomeHostCommands(
        bool isHomeSurface,
        List<CommandContextItemViewModel> commands)
    {
        if (!isHomeSurface)
        {
            return commands;
        }

        return commands
            .Where(static c => !c.IsHostInjected || c.ShowInHoverActions)
            .ToList();
    }

    private static IReadOnlyList<CommandContextItemViewModel> TakeFirstN(
        List<CommandContextItemViewModel> commands,
        int maxHoverActions)
    {
        var max = maxHoverActions > 0 ? maxHoverActions : DefaultFirstN;
        return commands.Take(max).ToArray();
    }
}
