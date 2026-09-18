// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Windows.Input;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class AdvancedPastePage : NavigablePage, IRefreshablePage, IDisposable
    {
        private bool _disposed;

        private AdvancedPasteViewModel ViewModel { get; set; }

        public ICommand EnableAdvancedPasteAICommand => new RelayCommand(EnableAdvancedPasteAI);

        public AdvancedPastePage()
        {
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new AdvancedPasteViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<AdvancedPasteSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();

            Loaded += (s, e) =>
            {
                ViewModel.OnPageLoaded();
                ViewModel.ReloadGlobalAIConfiguration();
            };
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
            ViewModel.ReloadGlobalAIConfiguration();
        }

        private void EnableAdvancedPasteAI() => ViewModel.EnableAI();

        private void ManageAIProvidersCard_Click(object sender, RoutedEventArgs e)
        {
            ShellPage.Navigate(typeof(AIProvidersPage));
        }

        private void AdvancedPaste_EnableAIToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            var toggle = (ToggleSwitch)sender;

            if (toggle.IsOn)
            {
                ViewModel.EnableAI();
            }
            else
            {
                ViewModel.DisableAI();
                FixSpellingAndGrammar.IsExpanded = false;
                AdvancedPasteUIActions.IsExpanded = false;
            }
        }

        public async void DeleteCustomActionButton_Click(object sender, RoutedEventArgs e)
        {
            var customAction = GetBoundCustomAction(sender, e);
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
            CustomActionDialog.DataContext = GetBoundCustomAction(sender, e).Clone();
            CustomActionDialog.PrimaryButtonText = resourceLoader.GetString("CustomActionUpdate");
            await CustomActionDialog.ShowAsync();
        }

        private void ReorderButtonDown_Click(object sender, RoutedEventArgs e)
        {
            var index = ViewModel.CustomActions.IndexOf(GetBoundCustomAction(sender, e));
            ViewModel.CustomActions.Move(index, index + 1);
        }

        private void ReorderButtonUp_Click(object sender, RoutedEventArgs e)
        {
            var index = ViewModel.CustomActions.IndexOf(GetBoundCustomAction(sender, e));
            ViewModel.CustomActions.Move(index, index - 1);
        }

        private void CustomActionDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (args.Result != ContentDialogResult.Primary)
            {
                return;
            }

            var dialogCustomAction = GetBoundCustomAction(sender, args);
            var existingCustomAction = ViewModel.CustomActions.FirstOrDefault(candidate => candidate.Id == dialogCustomAction.Id);

            if (existingCustomAction == null)
            {
                ViewModel.AddCustomAction(dialogCustomAction);
            }
            else
            {
                existingCustomAction.Update(dialogCustomAction);
            }
        }

        private AdvancedPasteCustomAction GetBoundCustomAction(object sender, object eventArgs = null)
        {
            if (TryResolveCustomAction(sender, out var action))
            {
                return action;
            }

            if (eventArgs is RoutedEventArgs routedEventArgs && TryResolveCustomAction(routedEventArgs.OriginalSource, out action))
            {
                return action;
            }

            if (CustomActionDialog?.DataContext is AdvancedPasteCustomAction dialogAction)
            {
                return dialogAction;
            }

            throw new InvalidOperationException("Unable to determine Advanced Paste custom action from sender.");
        }

        private static bool TryResolveCustomAction(object source, out AdvancedPasteCustomAction action)
        {
            action = ResolveCustomAction(source);
            return action is not null;
        }

        private static AdvancedPasteCustomAction ResolveCustomAction(object source)
        {
            if (source is null)
            {
                return null;
            }

            if (source is AdvancedPasteCustomAction directAction)
            {
                return directAction;
            }

            if (source is MenuFlyoutItemBase menuItem && menuItem.Tag is AdvancedPasteCustomAction taggedAction)
            {
                return taggedAction;
            }

            if (source is FrameworkElement element)
            {
                return ResolveFromElement(element);
            }

            return null;
        }

        private static AdvancedPasteCustomAction ResolveFromElement(FrameworkElement element)
        {
            for (FrameworkElement current = element; current is not null; current = VisualTreeHelper.GetParent(current) as FrameworkElement)
            {
                if (current.Tag is AdvancedPasteCustomAction tagged)
                {
                    return tagged;
                }

                if (current.DataContext is AdvancedPasteCustomAction contextual)
                {
                    return contextual;
                }
            }

            return null;
        }

        private void ClearProviderSelection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ComboBox comboBox)
            {
                comboBox.SelectedIndex = -1;
            }
        }

        private string GetDefaultProviderLabel()
        {
            try
            {
                return ResourceLoaderInstance.ResourceLoader.GetString("AdvancedPaste_ActionProvider_Default");
            }
            catch
            {
                return "Default (use active provider)";
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            ViewModel?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
