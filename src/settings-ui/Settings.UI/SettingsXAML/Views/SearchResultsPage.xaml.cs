// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using CommunityToolkit.WinUI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public partial class SearchResultsPage : Page
    {
        public SearchResultsViewModel ViewModel { get; set; }

        public SearchResultsPage()
        {
            ViewModel = new SearchResultsViewModel();
            InitializeComponent();
            DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is SearchResultsNavigationParams searchParams)
            {
                ViewModel.SetSearchResults(searchParams.Query, searchParams.Results);
                PageControl.ModuleDescription = $"{ResourceLoaderInstance.ResourceLoader.GetString("Search_ResultsFor")} '{searchParams.Query}'";
            }
        }

        public void RefreshEnabledState()
        {
            // Implementation if needed for IRefreshablePage
        }

        private void ModuleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is SettingsCard card && card.DataContext is SettingEntry tagEntry)
            {
                NavigateToModule(tagEntry);
            }
        }

        private void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is SettingsCard card && card.DataContext is SettingEntry tagEntry)
            {
                NavigateToSetting(tagEntry);
            }
        }

        private void NavigateToModule(SettingEntry settingEntry)
        {
            // Get the page type from the setting entry
            var pageType = GetPageTypeFromName(settingEntry.PageTypeName);
            if (pageType != null)
            {
                NavigationService.Navigate(pageType);
            }
        }

        private void NavigateToSetting(SettingEntry settingEntry)
        {
            // Get the page type from the setting entry
            var pageType = GetPageTypeFromName(settingEntry.PageTypeName);
            if (pageType != null)
            {
                // Create navigation parameters to highlight the specific setting
                var navigationParams = new NavigationParams(settingEntry.ElementName, settingEntry.ParentElementName);
                NavigationService.Navigate(pageType, navigationParams);
            }
        }

        // AOT-compatible type lookup using switch expression instead of reflection (IL2026)
        private Type GetPageTypeFromName(string pageTypeName)
        {
            if (string.IsNullOrEmpty(pageTypeName))
            {
                return null;
            }

            return pageTypeName switch
            {
                nameof(DashboardPage) => typeof(DashboardPage),
                nameof(GeneralPage) => typeof(GeneralPage),
                nameof(AdvancedPastePage) => typeof(AdvancedPastePage),
                nameof(AlwaysOnTopPage) => typeof(AlwaysOnTopPage),
                nameof(AwakePage) => typeof(AwakePage),
                nameof(CmdNotFoundPage) => typeof(CmdNotFoundPage),
                nameof(CmdPalPage) => typeof(CmdPalPage),
                nameof(ColorPickerPage) => typeof(ColorPickerPage),
                nameof(CropAndLockPage) => typeof(CropAndLockPage),
                nameof(EnvironmentVariablesPage) => typeof(EnvironmentVariablesPage),
                nameof(FancyZonesPage) => typeof(FancyZonesPage),
                nameof(FileLocksmithPage) => typeof(FileLocksmithPage),
                nameof(HostsPage) => typeof(HostsPage),
                nameof(ImageResizerPage) => typeof(ImageResizerPage),
                nameof(KeyboardManagerPage) => typeof(KeyboardManagerPage),
                nameof(LightSwitchPage) => typeof(LightSwitchPage),
                nameof(MeasureToolPage) => typeof(MeasureToolPage),
                nameof(MouseUtilsPage) => typeof(MouseUtilsPage),
                nameof(MouseWithoutBordersPage) => typeof(MouseWithoutBordersPage),
                nameof(NewPlusPage) => typeof(NewPlusPage),
                nameof(PeekPage) => typeof(PeekPage),
                nameof(PowerAccentPage) => typeof(PowerAccentPage),
                nameof(PowerLauncherPage) => typeof(PowerLauncherPage),
                nameof(PowerOcrPage) => typeof(PowerOcrPage),
                nameof(PowerPreviewPage) => typeof(PowerPreviewPage),
                nameof(PowerRenamePage) => typeof(PowerRenamePage),
                nameof(PowerDisplayPage) => typeof(PowerDisplayPage),
                nameof(RegistryPreviewPage) => typeof(RegistryPreviewPage),
                nameof(ShortcutGuidePage) => typeof(ShortcutGuidePage),
                nameof(WorkspacesPage) => typeof(WorkspacesPage),
                nameof(ZoomItPage) => typeof(ZoomItPage),
                _ => null,
            };
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    public class SearchResultsNavigationParams
#pragma warning restore SA1402 // File may only contain a single type
    {
        public string Query { get; set; }

        public List<SettingEntry> Results { get; set; }

        public SearchResultsNavigationParams(string query, List<SettingEntry> results)
        {
            Query = query;
            Results = results;
        }
    }
}
