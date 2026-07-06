// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Awake.Core;
using Awake.Core.Models;
using Awake.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace Awake
{
    /// <summary>
    /// Lets the user pick a running app to keep the system awake while it runs. Selecting an app
    /// records it as the pending selection and returns to the launch page; it starts when the user
    /// presses Start (or live-rebinds if a session is already running).
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)]
    public sealed partial class AwakeAppPickerPage : Page
    {
        private AwakeFlyoutNavigationContext? _context;
        private List<RunningAppInfo> _windowedApps = new();
        private List<RunningAppInfo> _allProcesses = new();
        private bool _allProcessesLoaded;
        private bool _agentsSubscribed;
        private const int MaxResults = 100;

        public AwakeAppPickerPage()
        {
            InitializeComponent();
        }

        public AwakeFlyoutViewModel ViewModel { get; private set; } = default!;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is AwakeFlyoutNavigationContext context)
            {
                _context = context;
                ViewModel = context.ViewModel;
            }

            _ = LoadRunningAppsAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            if (_agentsSubscribed)
            {
                AgentStatusStore.Changed -= OnAgentStatusChanged;
                _agentsSubscribed = false;
            }
        }

        private async Task LoadRunningAppsAsync()
        {
            AppSearchBox.Text = string.Empty;
            AppListView.ItemsSource = null;
            AppEmptyText.Visibility = Visibility.Collapsed;
            AppLoadingRing.IsActive = true;
            AppLoadingRing.Visibility = Visibility.Visible;

            _windowedApps = await RunningAppsProvider.GetRunningAppsAsync();

            AppLoadingRing.IsActive = false;
            AppLoadingRing.Visibility = Visibility.Collapsed;

            await ApplyAppFilterAsync(string.Empty);

            // Load the full process list in the background so the first search is responsive.
            _ = LoadAllProcessesAsync();
        }

        private async Task LoadAllProcessesAsync()
        {
            if (_allProcessesLoaded)
            {
                return;
            }

            _allProcesses = await RunningAppsProvider.GetAllProcessesAsync();
            _allProcessesLoaded = true;
        }

        private async Task ApplyAppFilterAsync(string query)
        {
            // Empty query shows the curated windowed-app list; typing searches every process.
            bool searching = !string.IsNullOrWhiteSpace(query);
            List<RunningAppInfo> source = searching && _allProcessesLoaded ? _allProcesses : _windowedApps;

            IEnumerable<RunningAppInfo> filtered = source;
            if (searching)
            {
                filtered = source.Where(a =>
                    a.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || a.WindowTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase));
            }

            List<RunningAppInfo> list = filtered.Take(MaxResults).ToList();

            // Build icons lazily for only the items about to be shown (the full process list can be
            // large, so we avoid materializing hundreds of bitmaps up front).
            foreach (RunningAppInfo app in list)
            {
                if (app.Icon is null && app.IconBytes is not null)
                {
                    app.Icon = await RunningAppsProvider.BuildIconAsync(app.IconBytes);
                }
            }

            AppListView.ItemsSource = list;
            AppEmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void OnAppSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                // If the user starts typing before the full list finished loading, fetch it now.
                if (!string.IsNullOrWhiteSpace(sender.Text) && !_allProcessesLoaded)
                {
                    await LoadAllProcessesAsync();
                }

                await ApplyAppFilterAsync(sender.Text);
            }
        }

        private void OnAppSelected(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is RunningAppInfo app)
            {
                ViewModel.SetPendingApp(app.ProcessId, app.DisplayName, app.Icon);
                GoBack();
            }
        }

        private void OnSourceSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // The Segmented control raises SelectionChanged while its items are being added during
            // InitializeComponent(), before the named panels exist. Ignore those early events.
            if (AppsPanel is null || AgentsPanel is null)
            {
                return;
            }

            // Index 0 = Apps, index 1 = Agents.
            bool showAgents = SourceSelector.SelectedIndex == 1;

            AppsPanel.Visibility = showAgents ? Visibility.Collapsed : Visibility.Visible;
            AgentsPanel.Visibility = showAgents ? Visibility.Visible : Visibility.Collapsed;

            if (showAgents)
            {
                AgentStatusStore.EnsureWatching();
                if (!_agentsSubscribed)
                {
                    AgentStatusStore.Changed += OnAgentStatusChanged;
                    _agentsSubscribed = true;
                }

                LoadAgents();
            }
        }

        private void OnAgentStatusChanged(object? sender, EventArgs e)
        {
            // The watcher fires on a background thread; refresh on the UI thread.
            _ = DispatcherQueue.TryEnqueue(LoadAgents);
        }

        private void LoadAgents()
        {
            List<AgentInfo> agents = AgentStatusStore.GetAgents().ToList();

            AgentListView.ItemsSource = agents;
            AgentEmptyText.Visibility = agents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnAgentSelected(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AgentInfo agent)
            {
                ViewModel.SetPendingAgent(agent.Id, agent.DisplayName);
                GoBack();
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => GoBack();

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                GoBack();
                e.Handled = true;
            }
        }

        private void GoBack()
        {
            if (Frame != null && Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}
