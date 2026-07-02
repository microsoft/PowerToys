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

        var candidates = FilterHostInjectedCommands(visible);

        return mode switch
        {
            HoverActionsMode.Explicit => ResolveExplicit(candidates, ctx.MaxHoverActions),
            HoverActionsMode.AllMoreCommands => ApplyMax(candidates, ctx.MaxHoverActions),
            _ => TakeFirstN(candidates, ctx.MaxHoverActions),
        };
    }

    private static IReadOnlyList<CommandContextItemViewModel> ResolveExplicit(
        List<CommandContextItemViewModel> commands,
        int maxHoverActions)
    {
        var explicitActions = commands
            .Where(static c => c.ShowInHoverActions)
            .OrderBy(static c => c.HoverOrder)
            .ToList();

        if (explicitActions.Count > 0)
        {
            return ApplyMax(explicitActions, maxHoverActions);
        }

        // Extension chose Explicit but did not flag any commands yet — keep legacy first-N behavior.
        return TakeFirstN(commands, maxHoverActions);
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
            visibility = HoverActionsVisibility.HoverOrSelected;
        }

        var isRowHovered = ctx.IsRowHovered;
        if (ctx.SuppressNonSelectedRowHover && !ctx.IsListSelected)
        {
            isRowHovered = false;
        }

        return visibility switch
        {
            HoverActionsVisibility.OnHoverOnly => isRowHovered,
            HoverActionsVisibility.HoverOrSelected => isRowHovered || ctx.IsListSelected,

            // Safe fallback for any future enum values: treat as HoverOrSelected so the
            // strip is still reachable (vs. always hidden or always visible).
            _ => isRowHovered || ctx.IsListSelected,
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

    private static IReadOnlyList<CommandContextItemViewModel> ApplyMax(
        List<CommandContextItemViewModel> commands,
        int maxHoverActions)
    {
        if (maxHoverActions <= 0)
        {
            return commands;
        }

        return commands.Take(maxHoverActions).ToArray();
    }

    private static List<CommandContextItemViewModel> FilterHostInjectedCommands(
        List<CommandContextItemViewModel> commands) =>
        commands
            .Where(static c => !c.IsHostInjected || c.ShowInHoverActions)
            .ToList();

    private static IReadOnlyList<CommandContextItemViewModel> TakeFirstN(
        List<CommandContextItemViewModel> commands,
        int maxHoverActions)
    {
        var max = maxHoverActions > 0 ? maxHoverActions : DefaultFirstN;
        return commands.Take(max).ToArray();
    }
}
