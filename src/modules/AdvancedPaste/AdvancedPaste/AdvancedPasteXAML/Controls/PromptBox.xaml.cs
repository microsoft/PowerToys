// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using AdvancedPaste.Models;
using AdvancedPaste.ViewModels;
using CommunityToolkit.Mvvm.Input;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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

            // Hook up the AIProviderButton after the template is applied
            InputTxtBox.Loaded += InputTxtBox_Loaded;
        }

        private void InputTxtBox_Loaded(object sender, RoutedEventArgs e)
        {
            // Find the AIProviderButton in the template
            var button = FindChildByName<Button>(InputTxtBox, "AIProviderButton");
            if (button != null)
            {
                button.Click -= AIProviderButton_Click; // Remove in case already added
                button.Click += AIProviderButton_Click;

                // Set up the button content with actual provider info
                UpdateAIProviderButtonContent(button);
            }
        }

        private void UpdateAIProviderButtonContent(Button button)
        {
            var activeProvider = ViewModel?.ActiveAIProvider;
            if (activeProvider == null || button == null)
            {
                return;
            }

            // Find the Image and TextBlock in the button
            var image = FindChildByName<Image>(button, "AIProviderIcon");
            var tooltip = FindChildByName<TextBlock>(button, "AIProviderTooltip");

            if (image != null)
            {
                var iconPath = Microsoft.PowerToys.Settings.UI.Library.AIServiceTypeRegistry.GetIconPath(activeProvider.ServiceType);
                image.Source = new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri(iconPath));
            }

            if (tooltip != null)
            {
                tooltip.Text = $"{activeProvider.ModelName} ({activeProvider.ServiceType}) - Click to change AI provider in Settings";
            }
        }

        private T FindChildByName<T>(DependencyObject parent, string childName)
            where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement element && element.Name == childName && child is T typedChild)
                {
                    return typedChild;
                }

                var result = FindChildByName<T>(child, childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ViewModel.IsBusy) or nameof(ViewModel.PasteActionError))
            {
                var state = ViewModel.IsBusy ? "LoadingState" : ViewModel.PasteActionError.HasText ? "ErrorState" : "DefaultState";
                VisualStateManager.GoToState(this, state, true);
            }
            else if (e.PropertyName == nameof(ViewModel.ActiveAIProvider))
            {
                // Update the button when the active provider changes
                var button = FindChildByName<Button>(InputTxtBox, "AIProviderButton");
                if (button != null)
                {
                    UpdateAIProviderButtonContent(button);
                }
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

        private void AIProviderButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.OpenSettings();
        }

        internal void IsLoading(bool loading)
        {
            Loader.IsLoading = loading;
        }
    }
}
