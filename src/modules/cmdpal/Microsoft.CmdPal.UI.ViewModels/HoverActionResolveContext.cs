// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public readonly record struct HoverActionResolveContext(
    bool EnableListHoverActions,
    bool IsHomeSurface,
    HoverActionsMode Mode,
    int MaxHoverActions,
    HoverActionsVisibility Visibility,
    bool IsRowHovered,
    bool IsListSelected,
    IReadOnlyList<CommandContextItemViewModel> Commands);
