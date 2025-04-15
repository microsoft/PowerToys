// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Threading.Tasks;

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
            get => GetValue(FooterProperty);
            set => SetValue(FooterProperty, value);
        }

        public PromptBox()
        {
            InitializeComponent();

            ViewModel = App.GetService<OptionsViewModel>();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.PreviewRequested += ViewModel_PreviewRequested;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ViewModel.IsBusy) or nameof(ViewModel.PasteActionError))
            {
                var state = ViewModel.IsBusy ? "LoadingState" : ViewModel.PasteActionError.HasText ? "ErrorState" : "DefaultState";
                VisualStateManager.GoToState(this, state, true);
            }
        }

        private void ViewModel_PreviewRequested(object sender, EventArgs e)
        {
            Logger.LogTrace();

            PreviewGrid.Width = InputTxtBox.ActualWidth;
            PreviewFlyout.ShowAt(InputTxtBox);
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            InputTxtBox.Focus(FocusState.Programmatic);
        }

        [RelayCommand]
        private async Task GenerateCustomAIAsync() => await ViewModel.ExecuteCustomAIFormatFromCurrentQueryAsync(PasteActionSource.PromptBox);

        [RelayCommand]
        private async Task CancelPasteActionAsync() => await ViewModel.CancelPasteActionAsync();

        private async void InputTxtBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && InputTxtBox.Text.Length > 0 && ViewModel.IsCustomAIAvailable)
            {
                await GenerateCustomAIAsync();
            }
        }

        private async void PreviewPasteBtn_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.PasteCustomAsync();
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
