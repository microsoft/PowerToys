// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Microsoft.PowerToys.Common.UI.Controls;

/// <summary>
/// Property/event-based AI prompt entry field for reuse across PowerToys modules.
/// Hosts own orchestration via <see cref="PromptSubmitted"/> and <see cref="RequestCancelled"/>.
/// </summary>
public sealed partial class PromptBox : UserControl
{
    private const double DefaultLeftInset = 12;
    private const double RightInset = 4;
    private const double VerticalInset = 8;
    private const double EdgeInset = 8;
    private const double ModelSelectorGap = 6;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(PromptBox),
        new PropertyMetadata(string.Empty, (d, e) => ((PromptBox)d).OnTextChanged()));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText),
        typeof(string),
        typeof(PromptBox),
        new PropertyMetadata(string.Empty));

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer),
        typeof(object),
        typeof(PromptBox),
        new PropertyMetadata(null));

    public object Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public static readonly DependencyProperty ModelSelectorProperty = DependencyProperty.Register(
        nameof(ModelSelector),
        typeof(object),
        typeof(PromptBox),
        new PropertyMetadata(null, (d, e) => ((PromptBox)d).UpdateInputGutter()));

    public object ModelSelector
    {
        get => GetValue(ModelSelectorProperty);
        set => SetValue(ModelSelectorProperty, value);
    }

    public static readonly DependencyProperty IsBusyProperty = DependencyProperty.Register(
        nameof(IsBusy),
        typeof(bool),
        typeof(PromptBox),
        new PropertyMetadata(false, (d, e) => ((PromptBox)d).UpdateVisualState()));

    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public static readonly DependencyProperty HasErrorProperty = DependencyProperty.Register(
        nameof(HasError),
        typeof(bool),
        typeof(PromptBox),
        new PropertyMetadata(false, (d, e) => ((PromptBox)d).UpdateVisualState()));

    public bool HasError
    {
        get => (bool)GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    public event EventHandler? PromptSubmitted;

    public event EventHandler? RequestCancelled;

    public PromptBox()
    {
        InitializeComponent();
        UpdateVisualState();
        UpdateInputGutter();
    }

    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public void FocusInput() => InputTxtBox.Focus(FocusState.Programmatic);

    public void SetInputLabel(TextBlock label)
    {
        AutomationProperties.SetLabeledBy(InputTxtBox, label);
        AutomationProperties.SetName(InputTxtBox, label.Text);
    }

    private void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        InputTxtBox.Focus(FocusState.Programmatic);
        UpdateSendButtonState();
        UpdateInputGutter();
    }

    private void ModelSelectorPresenter_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width != e.PreviousSize.Width)
        {
            UpdateInputGutter();
        }
    }

    private void UpdateInputGutter()
    {
        if (InputTxtBox is null || ModelSelectorPresenter is null)
        {
            return;
        }

        var selectorWidth = ModelSelectorPresenter.ActualWidth;
        var left = selectorWidth > 0 ? EdgeInset + selectorWidth + ModelSelectorGap : DefaultLeftInset;
        var padding = new Thickness(left, VerticalInset, RightInset, VerticalInset);

        if (!InputTxtBox.Padding.Equals(padding))
        {
            InputTxtBox.Padding = padding;
        }
    }

    private void OnTextChanged() => UpdateSendButtonState();

    private void UpdateSendButtonState()
    {
        if (SendBtn is not null)
        {
            SendBtn.IsEnabled = !IsBusy && HasText;
        }
    }

    private void UpdateVisualState()
    {
        var state = IsBusy ? "LoadingState" : HasError ? "ErrorState" : "DefaultState";
        VisualStateManager.GoToState(this, state, true);
        UpdateSendButtonState();
    }

    private void InputTxtBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        // Shift+Enter inserts a newline; plain Enter submits.
        var shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        if (((int)shiftState & 1) != 0)
        {
            return;
        }

        e.Handled = true;
        Submit();
    }

    private void SendBtn_Click(object sender, RoutedEventArgs e) => Submit();

    private void CancelBtn_Click(object sender, RoutedEventArgs e) => RequestCancelled?.Invoke(this, EventArgs.Empty);

    private void Submit()
    {
        if (IsBusy || !HasText)
        {
            return;
        }

        PromptSubmitted?.Invoke(this, EventArgs.Empty);
    }
}
