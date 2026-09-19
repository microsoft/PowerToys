// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace RobocopyUI.Controls
{
    /// <summary>
    /// The AI prompt entry field: a rounded input with an inline send button that turns into a
    /// cancel button, and an animated gradient border while a request is running.
    /// </summary>
    /// <remarks>
    /// Adapted from Advanced Paste's <c>PromptBox</c> to give both AI entry points the same look.
    /// Advanced Paste's version binds directly to its <c>OptionsViewModel</c>, so it cannot be
    /// referenced across modules; this one exposes plain properties and events instead and leaves
    /// request orchestration to the host.
    /// </remarks>
    public sealed partial class PromptBox : UserControl
    {
        private const double DefaultLeftInset = 12;
        private const double RightInset = 4;
        private const double VerticalInset = 8;

        /// <summary>
        /// Inset of the model selector and the send button from their respective edges. Both controls
        /// use it, which is what puts them on a shared baseline clear of the rounded corners.
        /// </summary>
        private const double EdgeInset = 8;

        /// <summary>
        /// Gap between the model selector and the first character of the prompt.
        /// </summary>
        private const double ModelSelectorGap = 6;

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(PromptBox),
            new PropertyMetadata(defaultValue: string.Empty, (d, e) => ((PromptBox)d).OnTextChanged()));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
            nameof(PlaceholderText),
            typeof(string),
            typeof(PromptBox),
            new PropertyMetadata(defaultValue: string.Empty));

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
            nameof(Footer),
            typeof(object),
            typeof(PromptBox),
            new PropertyMetadata(defaultValue: null));

        public object Footer
        {
            get => GetValue(FooterProperty);
            set => SetValue(FooterProperty, value);
        }

        public static readonly DependencyProperty ModelSelectorProperty = DependencyProperty.Register(
            nameof(ModelSelector),
            typeof(object),
            typeof(PromptBox),
            new PropertyMetadata(defaultValue: null, (d, e) => ((PromptBox)d).UpdateInputGutter()));

        /// <summary>
        /// Gets or sets the content shown in the gutter at the left edge of the input, used for the
        /// AI provider picker. The input's left padding tracks its measured width, so hosts that do
        /// not supply one (or that hide it) get no empty gutter.
        /// </summary>
        public object ModelSelector
        {
            get => GetValue(ModelSelectorProperty);
            set => SetValue(ModelSelectorProperty, value);
        }

        public static readonly DependencyProperty IsBusyProperty = DependencyProperty.Register(
            nameof(IsBusy),
            typeof(bool),
            typeof(PromptBox),
            new PropertyMetadata(defaultValue: false, (d, e) => ((PromptBox)d).UpdateVisualState()));

        /// <summary>
        /// Gets or sets a value indicating whether a request is in flight. Drives the animated
        /// border, disables input, and swaps the send button for a cancel button.
        /// </summary>
        public bool IsBusy
        {
            get => (bool)GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);
        }

        public static readonly DependencyProperty HasErrorProperty = DependencyProperty.Register(
            nameof(HasError),
            typeof(bool),
            typeof(PromptBox),
            new PropertyMetadata(defaultValue: false, (d, e) => ((PromptBox)d).UpdateVisualState()));

        public bool HasError
        {
            get => (bool)GetValue(HasErrorProperty);
            set => SetValue(HasErrorProperty, value);
        }

        /// <summary>
        /// Raised when the user submits the prompt, by pressing Enter or clicking send.
        /// </summary>
        public event EventHandler? PromptSubmitted;

        /// <summary>
        /// Raised when the user cancels an in-flight request.
        /// </summary>
        public event EventHandler? RequestCancelled;

        public PromptBox()
        {
            InitializeComponent();
            UpdateVisualState();
            UpdateInputGutter();
        }

        /// <summary>
        /// Gets a value indicating whether the prompt currently holds text worth submitting.
        /// </summary>
        public bool HasText => !string.IsNullOrWhiteSpace(Text);

        /// <summary>
        /// Moves keyboard focus into the prompt input.
        /// </summary>
        public void FocusInput() => InputTxtBox.Focus(FocusState.Programmatic);

        /// <summary>
        /// Points assistive technology at the host's visible label for the prompt. The input lives
        /// inside this control, so the host cannot reach it to set the association itself.
        /// </summary>
        /// <remarks>
        /// The label text is mirrored into the automation name as well: a name set in XAML wins over
        /// <c>LabeledBy</c>, so the relationship alone would leave the generic fallback name in place.
        /// </remarks>
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

        /// <summary>
        /// Insets the text so it clears the model selector overlaid on the left edge. The selector is
        /// measured rather than assumed, so a host that hides it does not leave a blank gutter.
        /// </summary>
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

            // Shift+Enter inserts a newline; plain Enter submits. The TextBox accepts returns so the
            // user can write a multi-line description.
            var shiftDown = Microsoft.UI.Input.InputKeyboardSource
                .GetKeyStateForCurrentThread(VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (shiftDown)
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
}
