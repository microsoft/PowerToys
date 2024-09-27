// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.ViewModels;
using CommunityToolkit.Mvvm.Input;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AdvancedPaste.Controls
{
    public sealed partial class PromptBox : Microsoft.UI.Xaml.Controls.UserControl
    {
        public OptionsViewModel ViewModel { get; private set; }

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
            get => (object)GetValue(FooterProperty);
            set => SetValue(FooterProperty, value);
        }

        public PromptBox()
        {
            InitializeComponent();

            ViewModel = App.GetService<OptionsViewModel>();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.CustomActionActivated += ViewModel_CustomActionActivated;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Busy) || e.PropertyName == nameof(ViewModel.PasteOperationErrorText))
            {
                var state = ViewModel.Busy ? "LoadingState" : string.IsNullOrEmpty(ViewModel.PasteOperationErrorText) ? "DefaultState" : "ErrorState";
                VisualStateManager.GoToState(this, state, true);
            }
        }

        private void ViewModel_CustomActionActivated(object sender, CustomActionActivatedEventArgs e)
        {
            Logger.LogTrace();

            if (!e.PasteResult)
            {
                PreviewGrid.Width = InputTxtBox.ActualWidth;
                PreviewFlyout.ShowAt(InputTxtBox);
            }
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            InputTxtBox.Focus(FocusState.Programmatic);
        }

        [RelayCommand]
        private async Task GenerateCustomAsync() => await ViewModel.GenerateCustomFunctionAsync(PasteActionSource.PromptBox);

        [RelayCommand]
        private void Recall()
        {
            Logger.LogTrace();

            InputTxtBox.IsEnabled = true;

            var lastQuery = ViewModel.RecallPreviousCustomQuery();
            if (lastQuery != null)
            {
                InputTxtBox.Text = lastQuery.Query;
            }

            ClipboardHelper.SetClipboardTextContent(lastQuery.ClipboardData);
        }

        private async void InputTxtBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && InputTxtBox.Text.Length > 0 && ViewModel.IsCustomAIEnabled)
            {
                await GenerateCustomAsync();
            }
        }

        private void PreviewPasteBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.PasteCustom();
        }

        private void ThumbUpDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && bool.TryParse(btn.CommandParameter as string, out bool result))
            {
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteCustomFormatOutputThumbUpDownEvent(result));
            }
        }

        private void PreviewFlyout_Opened(object sender, object e)
        {
            PreviewPasteBtn.Focus(FocusState.Programmatic);
        }

        internal void IsLoading(bool loading)
        {
            Loader.IsLoading = loading;
        }
    }
}
