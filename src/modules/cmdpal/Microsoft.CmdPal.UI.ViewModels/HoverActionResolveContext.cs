// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public readonly record struct HoverActionResolveContext(
    bool EnableListHoverActions,

    /// <summary>
    /// Home page list surface. Drives per-row <c>ICommandItem2</c> home hover overrides in
    /// <see cref="ListViewModel.GetHoverActionSettings"/>; not used for strip visibility.
    /// </summary>
    bool IsHomeSurface,
    HoverActionsMode Mode,
    int MaxHoverActions,
    HoverActionsVisibility Visibility,
    bool IsRowHovered,
    bool IsListSelected,
    IReadOnlyList<CommandContextItemViewModel> Commands,
    bool SuppressNonSelectedRowHover = false);
