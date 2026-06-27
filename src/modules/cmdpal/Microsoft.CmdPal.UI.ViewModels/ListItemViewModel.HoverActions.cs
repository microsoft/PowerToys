// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.UI.ViewModels.Commands;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ListItemViewModel
{
    public const int NoHoverActionSelectionIndex = -1;

    private bool _isRowHovered;
    private bool _isListSelected;
    private int _hoverActionSelectedIndex = NoHoverActionSelectionIndex;
    private IReadOnlyList<CommandContextItemViewModel> _hoverActions = [];

    public IReadOnlyList<CommandContextItemViewModel> HoverActions => _hoverActions;

    public bool HasHoverActions => _hoverActions.Count > 0;

    public bool IsListHoverActionsEnabled =>
        PageContext.TryGetTarget(out var pageContext) &&
        pageContext is ListViewModel listViewModel &&
        listViewModel.EnableListHoverActions;

    public int HoverActionSelectedIndex
    {
        get => _hoverActionSelectedIndex;
        set
        {
            if (_hoverActionSelectedIndex == value)
            {
                return;
            }

            _hoverActionSelectedIndex = value;
            UpdateProperty(nameof(HoverActionSelectedIndex), nameof(HasSelectedHoverAction));
        }
    }

    public bool HasSelectedHoverAction =>
        _hoverActionSelectedIndex >= 0 && _hoverActionSelectedIndex < _hoverActions.Count;

    public bool AreHoverActionsVisible =>
        HoverActionResolver.ShouldShowHoverStrip(BuildHoverActionContext(), HasHoverActions);

    public bool ShowTagsWhenNotHovering => HasTags && !AreHoverActionsVisible;

    internal bool CanResolveHoverActions => IsSelectedInitialized;

    internal bool MayNeedHoverVisibilityRefresh => _isRowHovered || _isListSelected;

    public void SetRowHovered(bool isHovered)
    {
        if (_isRowHovered == isHovered)
        {
            return;
        }

        _isRowHovered = isHovered;
        if (!isHovered && !_isListSelected)
        {
            ClearHoverActionSelection();
        }

        UpdateHoverVisibilityProperties();
    }

    public void SetListSelected(bool isSelected)
    {
        if (_isListSelected == isSelected)
        {
            return;
        }

        _isListSelected = isSelected;
        if (!isSelected)
        {
            ClearHoverActionSelection();
        }

        UpdateHoverVisibilityProperties();
    }

    public void ClearHoverActionSelection()
    {
        HoverActionSelectedIndex = NoHoverActionSelectionIndex;
    }

    /// <returns>False when Tab should leave the hover strip (past last icon or no actions).</returns>
    public bool SelectNextHoverAction()
    {
        var index = _hoverActionSelectedIndex;
        var handled = HoverActionSelection.TrySelectNext(ref index, _hoverActions.Count, AreHoverActionsVisible);
        HoverActionSelectedIndex = index;
        return handled;
    }

    /// <returns>False when Shift+Tab should leave the hover strip (before first icon).</returns>
    public bool SelectPrevHoverAction()
    {
        var index = _hoverActionSelectedIndex;
        var handled = HoverActionSelection.TrySelectPrev(ref index, _hoverActions.Count, AreHoverActionsVisible);
        HoverActionSelectedIndex = index;
        return handled;
    }

    public void SelectLastHoverAction()
    {
        var index = _hoverActionSelectedIndex;
        if (HoverActionSelection.TrySelectLastOnBackwardEntry(ref index, _hoverActions.Count, AreHoverActionsVisible))
        {
            HoverActionSelectedIndex = index;
        }
    }

    public bool TryGetSelectedHoverAction(out CommandContextItemViewModel? action)
    {
        if (HasSelectedHoverAction)
        {
            action = _hoverActions[_hoverActionSelectedIndex];
            return true;
        }

        action = null;
        return false;
    }

    public void RefreshHoverActions() => UpdateHoverActions();

    public void RefreshHoverVisibility() => UpdateHoverVisibilityProperties();

    private void UpdateHoverActions()
    {
        if (!IsSelectedInitialized)
        {
            _hoverActions = [];
            UpdateHoverVisibilityProperties();
            return;
        }

        var context = BuildHoverActionContext();
        _hoverActions = HoverActionResolver.Resolve(context);
        if (_hoverActionSelectedIndex >= _hoverActions.Count)
        {
            ClearHoverActionSelection();
        }

        UpdateHoverVisibilityProperties();
    }

    private HoverActionResolveContext BuildHoverActionContext()
    {
        var commands = MoreCommands.OfType<CommandContextItemViewModel>().ToList();
        var enableHover = true;
        var isHome = false;
        var mode = Microsoft.CommandPalette.Extensions.HoverActionsMode.Default;
        var max = -1;
        var visibility = Microsoft.CommandPalette.Extensions.HoverActionsVisibility.Default;
        var suppressNonSelectedRowHover = false;

        if (PageContext.TryGetTarget(out var pageContext) && pageContext is ListViewModel listViewModel)
        {
            enableHover = listViewModel.EnableListHoverActions;
            isHome = listViewModel.IsMainPage;
            suppressNonSelectedRowHover = listViewModel.SuppressNonSelectedRowHover;
            var settings = listViewModel.GetHoverActionSettings(this);
            mode = settings.Mode;
            max = settings.MaxHoverActions;
            visibility = settings.Visibility;
        }

        return new HoverActionResolveContext(
            enableHover,
            isHome,
            mode,
            max,
            visibility,
            _isRowHovered,
            _isListSelected,
            commands,
            suppressNonSelectedRowHover);
    }

    private void UpdateHoverVisibilityProperties()
    {
        if (!IsListHoverActionsEnabled)
        {
            return;
        }

        UpdateProperty(
            nameof(HoverActions),
            nameof(HasHoverActions),
            nameof(AreHoverActionsVisible),
            nameof(ShowTagsWhenNotHovering),
            nameof(HoverActionSelectedIndex),
            nameof(HasSelectedHoverAction));
    }
}
