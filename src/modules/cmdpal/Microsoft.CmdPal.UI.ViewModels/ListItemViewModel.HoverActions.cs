// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ListItemViewModel
{
    private bool _isRowHovered;
    private bool _isListSelected;
    private IReadOnlyList<CommandContextItemViewModel> _hoverActions = [];

    public IReadOnlyList<CommandContextItemViewModel> HoverActions => _hoverActions;

    public bool HasHoverActions => _hoverActions.Count > 0;

    public bool AreHoverActionsVisible =>
        HoverActionResolver.ShouldShowHoverStrip(BuildHoverActionContext(), HasHoverActions);

    public bool ShowTagsWhenNotHovering => HasTags && !AreHoverActionsVisible;

    public void EnsureHoverActionsLoaded()
    {
        if (!IsSelectedInitialized)
        {
            SafeSlowInit();
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

    internal void RefreshHoverActions() => UpdateHoverActions();

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

        if (PageContext.TryGetTarget(out var pageContext) && pageContext is ListViewModel listViewModel)
        {
            enableHover = listViewModel.EnableListHoverActions;
            isHome = listViewModel.IsMainPage;
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
            commands);
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
