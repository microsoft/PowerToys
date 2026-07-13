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
    private bool _isRowTabFocused;
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

            // Icon focus and row-outline focus must never show at the same time.
            if (_hoverActionSelectedIndex >= 0 && _isRowTabFocused)
            {
                _isRowTabFocused = false;
                UpdateProperty(
                    nameof(HoverActionSelectedIndex),
                    nameof(HasSelectedHoverAction),
                    nameof(IsRowTabFocused));
                SyncHoverActionKeyboardSelection();
                return;
            }

            UpdateProperty(nameof(HoverActionSelectedIndex), nameof(HasSelectedHoverAction));
            SyncHoverActionKeyboardSelection();
        }
    }

    public bool HasSelectedHoverAction =>
        _hoverActionSelectedIndex >= 0 && _hoverActionSelectedIndex < _hoverActions.Count;

    /// <summary>
    /// Visual-only Tab step: the list row is highlighted before cycling hover icons.
    /// </summary>
    public bool IsRowTabFocused => _isRowTabFocused;

    public bool AreHoverActionsVisible =>
        HoverActionResolver.ShouldShowHoverStrip(BuildHoverActionContext(), HasHoverActions);

    public bool ShowTags => HasTags;

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
        SetRowTabFocused(false);
    }

    public void SetRowTabFocused(bool focused)
    {
        if (_isRowTabFocused == focused)
        {
            return;
        }

        _isRowTabFocused = focused;

        // Row outline is a dedicated Tab step before icons; clear any icon selection.
        if (focused && _hoverActionSelectedIndex != NoHoverActionSelectionIndex)
        {
            _hoverActionSelectedIndex = NoHoverActionSelectionIndex;
            UpdateProperty(
                nameof(IsRowTabFocused),
                nameof(HoverActionSelectedIndex),
                nameof(HasSelectedHoverAction));
            SyncHoverActionKeyboardSelection();
            return;
        }

        UpdateProperty(nameof(IsRowTabFocused));
        if (focused)
        {
            SyncHoverActionKeyboardSelection();
        }
    }

    private void SyncHoverActionKeyboardSelection()
    {
        for (var i = 0; i < _hoverActions.Count; i++)
        {
            _hoverActions[i].IsHoverKeyboardSelected = i == _hoverActionSelectedIndex;
        }
    }

    /// <returns>False when Tab should leave the hover strip (past last icon or no actions).</returns>
    public bool HandleForwardHoverTab()
    {
        var index = _hoverActionSelectedIndex;
        var rowTabFocused = _isRowTabFocused;
        var handled = HoverActionTabNavigation.TryHandleForward(
            ref index,
            ref rowTabFocused,
            _hoverActions.Count,
            AreHoverActionsVisible);
        HoverActionSelectedIndex = index;
        SetRowTabFocused(rowTabFocused);
        return handled;
    }

    /// <returns>False when Shift+Tab should leave the hover strip.</returns>
    public bool HandleBackwardHoverTab()
    {
        var index = _hoverActionSelectedIndex;
        var rowTabFocused = _isRowTabFocused;
        var handled = HoverActionTabNavigation.TryHandleBackward(
            ref index,
            ref rowTabFocused,
            _hoverActions.Count,
            AreHoverActionsVisible);
        HoverActionSelectedIndex = index;
        SetRowTabFocused(rowTabFocused);
        return handled;
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
        else
        {
            SyncHoverActionKeyboardSelection();
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
            UpdateProperty(nameof(AreHoverActionsVisible));
            return;
        }

        UpdateProperty(
            nameof(HoverActions),
            nameof(HasHoverActions),
            nameof(AreHoverActionsVisible),
            nameof(HoverActionSelectedIndex),
            nameof(HasSelectedHoverAction),
            nameof(IsRowTabFocused));
    }
}
