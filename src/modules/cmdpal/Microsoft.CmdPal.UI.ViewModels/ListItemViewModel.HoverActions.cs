// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ListItemViewModel
{
    private const int MaxHoverActions = 3;

    private bool _isRowHovered;
    private bool _isListSelected;
    private IReadOnlyList<CommandContextItemViewModel> _hoverActions = [];

    public IReadOnlyList<CommandContextItemViewModel> HoverActions => _hoverActions;

    public bool HasHoverActions => _hoverActions.Count > 0;

    public bool AreHoverActionsVisible => (_isRowHovered || _isListSelected) && HasHoverActions;

    public bool ShowTagsWhenNotHovering => HasTags && !AreHoverActionsVisible;

    public void EnsureHoverActionsLoaded()
    {
        if (!IsSelectedInitialized)
        {
            SafeSlowInitSynchronous();
        }
    }

    public void SetRowHovered(bool isHovered)
    {
        if (_isRowHovered == isHovered)
        {
            return;
        }

        _isRowHovered = isHovered;
        UpdateHoverVisibilityProperties();
    }

    public void SetListSelected(bool isSelected)
    {
        if (_isListSelected == isSelected)
        {
            return;
        }

        _isListSelected = isSelected;
        UpdateHoverVisibilityProperties();
    }

    private void UpdateHoverActions()
    {
        if (!IsSelectedInitialized)
        {
            _hoverActions = [];
            UpdateHoverVisibilityProperties();
            return;
        }

        _hoverActions = MoreCommands
            .OfType<CommandContextItemViewModel>()
            .Where(action => action.ShouldBeVisible)
            .Take(MaxHoverActions)
            .ToArray();

        UpdateHoverVisibilityProperties();
    }

    private void UpdateHoverVisibilityProperties()
    {
        UpdateProperty(
            nameof(HoverActions),
            nameof(HasHoverActions),
            nameof(AreHoverActionsVisible),
            nameof(ShowTagsWhenNotHovering));
    }
}
