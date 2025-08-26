// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Microsoft.PowerToys.Settings.UI.Controls;

[TemplateVisualState(Name = BackButtonVisibleState, GroupName = BackButtonStates)]
[TemplateVisualState(Name = BackButtonCollapsedState, GroupName = BackButtonStates)]
[TemplateVisualState(Name = PaneButtonVisibleState, GroupName = PaneButtonStates)]
[TemplateVisualState(Name = PaneButtonCollapsedState, GroupName = PaneButtonStates)]
[TemplateVisualState(Name = WindowActivatedState, GroupName = ActivationStates)]
[TemplateVisualState(Name = WindowDeactivatedState, GroupName = ActivationStates)]
[TemplateVisualState(Name = StandardState, GroupName = DisplayModeStates)]
[TemplateVisualState(Name = TallState, GroupName = DisplayModeStates)]
[TemplateVisualState(Name = IconVisibleState, GroupName = IconStates)]
[TemplateVisualState(Name = IconCollapsedState, GroupName = IconStates)]
[TemplateVisualState(Name = ContentVisibleState, GroupName = ContentStates)]
[TemplateVisualState(Name = ContentCollapsedState, GroupName = ContentStates)]
[TemplateVisualState(Name = FooterVisibleState, GroupName = FooterStates)]
[TemplateVisualState(Name = FooterCollapsedState, GroupName = FooterStates)]
[TemplateVisualState(Name = WideState, GroupName = ReflowStates)]
[TemplateVisualState(Name = NarrowState, GroupName = ReflowStates)]
[TemplatePart(Name = PartBackButton, Type = typeof(Button))]
[TemplatePart(Name = PartPaneButton, Type = typeof(Button))]
[TemplatePart(Name = nameof(PART_LeftPaddingColumn), Type = typeof(ColumnDefinition))]
[TemplatePart(Name = nameof(PART_RightPaddingColumn), Type = typeof(ColumnDefinition))]
[TemplatePart(Name = nameof(PART_ButtonHolder), Type = typeof(StackPanel))]

public partial class TitleBar : Control
{
    private const string PartBackButton = "PART_BackButton";
    private const string PartPaneButton = "PART_PaneButton";

    private const string BackButtonVisibleState = "BackButtonVisible";
    private const string BackButtonCollapsedState = "BackButtonCollapsed";
    private const string BackButtonStates = "BackButtonStates";

    private const string PaneButtonVisibleState = "PaneButtonVisible";
    private const string PaneButtonCollapsedState = "PaneButtonCollapsed";
    private const string PaneButtonStates = "PaneButtonStates";

    private const string WindowActivatedState = "Activated";
    private const string WindowDeactivatedState = "Deactivated";
    private const string ActivationStates = "WindowActivationStates";

    private const string IconVisibleState = "IconVisible";
    private const string IconCollapsedState = "IconCollapsed";
    private const string IconStates = "IconStates";

    private const string StandardState = "Standard";
    private const string TallState = "Tall";
    private const string DisplayModeStates = "DisplayModeStates";

    private const string ContentVisibleState = "ContentVisible";
    private const string ContentCollapsedState = "ContentCollapsed";
    private const string ContentStates = "ContentStates";

    private const string FooterVisibleState = "FooterVisible";
    private const string FooterCollapsedState = "FooterCollapsed";
    private const string FooterStates = "FooterStates";

    private const string WideState = "Wide";
    private const string NarrowState = "Narrow";
    private const string ReflowStates = "ReflowStates";

#pragma warning disable SA1306 // Field names should begin with lower-case letter
#pragma warning disable SA1310 // Field names should not contain underscore
#pragma warning disable SA1400 // Access modifier should be declared
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    ColumnDefinition? PART_RightPaddingColumn;
    ColumnDefinition? PART_LeftPaddingColumn;
    StackPanel? PART_ButtonHolder;
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
#pragma warning restore SA1400 // Access modifier should be declared
#pragma warning restore SA1306 // Field names should begin with lower-case letter
#pragma warning restore SA1310 // Field names should not contain underscore

    // Internal tracking (if AutoConfigureCustomTitleBar is on) if we've actually setup the TitleBar yet or not
    // We only want to reset TitleBar configuration in app, if we're the TitleBar instance that's managing that state.
    private bool _isAutoConfigCompleted;

    public TitleBar()
    {
        this.DefaultStyleKey = typeof(TitleBar);
    }

    protected override void OnApplyTemplate()
    {
        PART_LeftPaddingColumn = GetTemplateChild(nameof(PART_LeftPaddingColumn)) as ColumnDefinition;
        PART_RightPaddingColumn = GetTemplateChild(nameof(PART_RightPaddingColumn)) as ColumnDefinition;
        ConfigureButtonHolder();
        Configure();
        if (GetTemplateChild(PartBackButton) is Button backButton)
        {
            backButton.Click -= BackButton_Click;
            backButton.Click += BackButton_Click;
        }

        if (GetTemplateChild(PartPaneButton) is Button paneButton)
        {
            paneButton.Click -= PaneButton_Click;
            paneButton.Click += PaneButton_Click;
        }

        SizeChanged -= this.TitleBar_SizeChanged;
        SizeChanged += this.TitleBar_SizeChanged;

        Update();
        base.OnApplyTemplate();
    }

    private void TitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateVisualStateAndDragRegion(e.NewSize);
    }

    private void UpdateVisualStateAndDragRegion(Size size)
    {
        if (size.Width <= CompactStateBreakpoint)
        {
            if (Content != null || Footer != null)
            {
                VisualStateManager.GoToState(this, NarrowState, true);
            }
        }
        else
        {
            VisualStateManager.GoToState(this, WideState, true);
        }

        SetDragRegionForCustomTitleBar();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        BackButtonClick?.Invoke(this, new RoutedEventArgs());
    }

    private void PaneButton_Click(object sender, RoutedEventArgs e)
    {
        PaneButtonClick?.Invoke(this, new RoutedEventArgs());
    }

    private void ConfigureButtonHolder()
    {
        if (PART_ButtonHolder != null)
        {
            PART_ButtonHolder.SizeChanged -= PART_ButtonHolder_SizeChanged;
        }

        PART_ButtonHolder = GetTemplateChild(nameof(PART_ButtonHolder)) as StackPanel;

        if (PART_ButtonHolder != null)
        {
            PART_ButtonHolder.SizeChanged += PART_ButtonHolder_SizeChanged;
        }
    }

    private void PART_ButtonHolder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        SetDragRegionForCustomTitleBar();
    }

    private void Configure()
    {
        SetWASDKTitleBar();
    }

    public void Reset()
    {
        ResetWASDKTitleBar();
    }

    private void Update()
    {
        if (Icon != null)
        {
            VisualStateManager.GoToState(this, IconVisibleState, true);
        }
        else
        {
            VisualStateManager.GoToState(this, IconCollapsedState, true);
        }

        VisualStateManager.GoToState(this, IsBackButtonVisible ? BackButtonVisibleState : BackButtonCollapsedState, true);
        VisualStateManager.GoToState(this, IsPaneButtonVisible ? PaneButtonVisibleState : PaneButtonCollapsedState, true);

        if (DisplayMode == DisplayMode.Tall)
        {
            VisualStateManager.GoToState(this, TallState, true);
        }
        else
        {
            VisualStateManager.GoToState(this, StandardState, true);
        }

        if (Content != null)
        {
            VisualStateManager.GoToState(this, ContentVisibleState, true);
        }
        else
        {
            VisualStateManager.GoToState(this, ContentCollapsedState, true);
        }

        if (Footer != null)
        {
            VisualStateManager.GoToState(this, FooterVisibleState, true);
        }
        else
        {
            VisualStateManager.GoToState(this, FooterCollapsedState, true);
        }

        SetDragRegionForCustomTitleBar();
    }
}
