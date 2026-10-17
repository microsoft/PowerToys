// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class SectionCommandButton : Button
{
    private bool _isPointerOver;

    internal bool IsPointerInvocation { get; private set; }

    public static readonly DependencyProperty IsSectionSelectedProperty =
        DependencyProperty.Register(nameof(IsSectionSelected), typeof(bool), typeof(SectionCommandButton), new PropertyMetadata(false, OnIsSectionSelectedChanged));

    public static readonly DependencyProperty ActionLabelOpacityProperty =
        DependencyProperty.Register(nameof(ActionLabelOpacity), typeof(double), typeof(SectionCommandButton), new PropertyMetadata(0.0));

    public static readonly DependencyProperty ActionIconOpacityProperty =
        DependencyProperty.Register(nameof(ActionIconOpacity), typeof(double), typeof(SectionCommandButton), new PropertyMetadata(0.6));

    public bool IsSectionSelected
    {
        get => (bool)GetValue(IsSectionSelectedProperty);
        set => SetValue(IsSectionSelectedProperty, value);
    }

    public double ActionLabelOpacity
    {
        get => (double)GetValue(ActionLabelOpacityProperty);
        private set => SetValue(ActionLabelOpacityProperty, value);
    }

    public double ActionIconOpacity
    {
        get => (double)GetValue(ActionIconOpacityProperty);
        private set => SetValue(ActionIconOpacityProperty, value);
    }

    public SectionCommandButton()
    {
        PointerEntered += SectionCommandButton_PointerEntered;
        PointerExited += ResetPointerState;
        PointerCanceled += ResetPointerState;
        Unloaded += ResetPointerState;
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        IsPointerInvocation = true;
        try
        {
            base.OnPointerReleased(e);
        }
        finally
        {
            IsPointerInvocation = false;
        }
    }

    private static void OnIsSectionSelectedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((SectionCommandButton)sender).UpdateActionVisibility();

    private void SectionCommandButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOver = true;
        UpdateActionVisibility();
    }

    private void ResetPointerState(object sender, RoutedEventArgs e)
    {
        _isPointerOver = false;
        UpdateActionVisibility();
    }

    private void UpdateActionVisibility()
    {
        var showAction = _isPointerOver || IsSectionSelected;
        ActionLabelOpacity = showAction ? 1.0 : 0.0;
        ActionIconOpacity = showAction ? 1.0 : 0.6;
    }
}
