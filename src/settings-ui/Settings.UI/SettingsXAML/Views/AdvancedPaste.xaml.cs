// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class AdvancedPastePage : Page, IRefreshablePage
    {
        private AdvancedPasteViewModel ViewModel { get; set; }

        public ICommand SaveOpenAIKeyCommand => new RelayCommand(SaveOpenAIKey);

        public AdvancedPastePage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new AdvancedPasteViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<AdvancedPasteSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();

            Loaded += (s, e) => ViewModel.OnPageLoaded();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private void SaveOpenAIKey()
        {
            if (!string.IsNullOrEmpty(AdvancedPaste_EnableAIDialogOpenAIApiKey.Text))
            {
                ViewModel.EnableAI(AdvancedPaste_EnableAIDialogOpenAIApiKey.Text);
            }
        }

        private async void AdvancedPaste_EnableAIButton_Click(object sender, RoutedEventArgs e)
        {
            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
            EnableAIDialog.PrimaryButtonText = resourceLoader.GetString("EnableAIDialog_SaveBtnText");
            EnableAIDialog.SecondaryButtonText = resourceLoader.GetString("EnableAIDialog_CancelBtnText");
            EnableAIDialog.PrimaryButtonCommand = SaveOpenAIKeyCommand;

            AdvancedPaste_EnableAIDialogOpenAIApiKey.Text = string.Empty;

            await ShowEnableDialogAsync();
        }

        private async Task ShowEnableDialogAsync()
        {
            await EnableAIDialog.ShowAsync();
        }

        private void AdvancedPaste_DisableAIButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.DisableAI();
        }

        private void AdvancedPaste_EnableAIDialogOpenAIApiKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableAIDialog.IsPrimaryButtonEnabled = AdvancedPaste_EnableAIDialogOpenAIApiKey.Text.Length > 0;
        }

        public async void DeleteCustomActionButton_Click(object sender, RoutedEventArgs e)
        {
            var customAction = GetBoundCustomAction(sender);
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            ContentDialog dialog = new()
            {
                XamlRoot = RootPage.XamlRoot,
                Title = customAction.Name,
                PrimaryButtonText = resourceLoader.GetString("Yes"),
                CloseButtonText = resourceLoader.GetString("No"),
                DefaultButton = ContentDialogButton.Primary,
                Content = new TextBlock() { Text = resourceLoader.GetString("Delete_Dialog_Description") },
            };

            dialog.PrimaryButtonClick += (_, _) => ViewModel.DeleteCustomAction(customAction);

            await dialog.ShowAsync();
        }

        private async void AddCustomActionButton_Click(object sender, RoutedEventArgs e)
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            CustomActionDialog.Title = resourceLoader.GetString("AddCustomAction");
            CustomActionDialog.DataContext = ViewModel.GetNewCustomAction(resourceLoader.GetString("AdvancedPasteUI_NewCustomActionPrefix"));
            CustomActionDialog.PrimaryButtonText = resourceLoader.GetString("CustomActionSave");
            await CustomActionDialog.ShowAsync();
        }

        private async void EditCustomActionButton_Click(object sender, RoutedEventArgs e)
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            CustomActionDialog.Title = resourceLoader.GetString("EditCustomAction");
            CustomActionDialog.DataContext = GetBoundCustomAction(sender).Clone();
            CustomActionDialog.PrimaryButtonText = resourceLoader.GetString("CustomActionUpdate");
            await CustomActionDialog.ShowAsync();
        }

        private void ReorderButtonDown_Click(object sender, RoutedEventArgs e)
        {
            var index = ViewModel.CustomActions.IndexOf(GetBoundCustomAction(sender));
            ViewModel.CustomActions.Move(index, index + 1);
        }

        private void ReorderButtonUp_Click(object sender, RoutedEventArgs e)
        {
            var index = ViewModel.CustomActions.IndexOf(GetBoundCustomAction(sender));
            ViewModel.CustomActions.Move(index, index - 1);
        }

        private void CustomActionDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (args.Result != ContentDialogResult.Primary)
            {
                return;
            }

            var dialogCustomAction = GetBoundCustomAction(sender);
            var existingCustomAction = ViewModel.CustomActions.FirstOrDefault(candidate => candidate.Id == dialogCustomAction.Id);

            if (existingCustomAction == null)
            {
                ViewModel.AddCustomAction(dialogCustomAction);

                var element = (ContentPresenter)CustomActions.ContainerFromIndex(CustomActions.Items.Count - 1);
                element.StartBringIntoView(new BringIntoViewOptions { VerticalOffset = -60, AnimationDesired = true });
                element.Focus(FocusState.Programmatic);
            }
            else
            {
                existingCustomAction.Update(dialogCustomAction);
            }
        }

        private static AdvancedPasteCustomAction GetBoundCustomAction(object sender) => (AdvancedPasteCustomAction)((FrameworkElement)sender).DataContext;
    }
}
