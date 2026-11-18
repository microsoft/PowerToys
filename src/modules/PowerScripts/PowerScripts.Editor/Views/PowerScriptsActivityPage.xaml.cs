// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PowerScripts.UI.ViewModels;

namespace PowerScripts.Editor.Views;

public sealed partial class PowerScriptsActivityPage : Page
{
    private static readonly string[] KnownStatuses = ["running", "succeeded", "failed", "interrupted"];

    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _isActive;

    private PowerScriptsViewModel ViewModel { get; set; }

    private ObservableCollection<PowerScriptActivityItem> VisibleActivity { get; } = new();

    private bool ShowFailedOnly => ActivityFilter.SelectedIndex == 1;

    public PowerScriptsActivityPage()
    {
        InitializeComponent();
        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
    }

    public static bool IsStatus(string status, string expected) =>
        expected == "unknown"
            ? !KnownStatuses.Contains(status, StringComparer.Ordinal)
            : string.Equals(status, expected, StringComparison.Ordinal);

    public static Visibility StatusVisibility(string status, string expected) =>
        IsStatus(status, expected) ? Visibility.Visible : Visibility.Collapsed;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = e.Parameter as PowerScriptsViewModel ??
            throw new ArgumentException("Activity navigation requires the PowerScripts view model.", nameof(e));
        Bindings.Update();
    }

    private static bool IsProblem(PowerScriptActivityItem item) =>
        item.Status is not "succeeded" and not "running" || (item.ExitCode is int exitCode && exitCode != 0);

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _isActive = true;
        SyncVisibleActivity();
        await RefreshAsync();
        if (_isActive)
        {
            _refreshTimer.Start();
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _isActive = false;
        _refreshTimer.Stop();
    }

    private async Task RefreshAsync(bool clearCompleted = false)
    {
        await ViewModel.RefreshActivityAsync(clearCompleted);
        SyncVisibleActivity();
    }

    private void ActivityFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null)
        {
            SyncVisibleActivity();
        }
    }

    private void ShowAll_Click(object sender, RoutedEventArgs e) => ActivityFilter.SelectedIndex = 0;

    private void SyncVisibleActivity()
    {
        var failedOnly = ShowFailedOnly;
        var desired = ViewModel.ActivityItems.Where(item => !failedOnly || IsProblem(item)).ToList();
        var desiredSet = desired.ToHashSet();

        for (var index = VisibleActivity.Count - 1; index >= 0; index--)
        {
            if (!desiredSet.Contains(VisibleActivity[index]))
            {
                VisibleActivity.RemoveAt(index);
            }
        }

        for (var index = 0; index < desired.Count; index++)
        {
            var current = VisibleActivity.IndexOf(desired[index]);
            if (current < 0)
            {
                VisibleActivity.Insert(index, desired[index]);
            }
            else if (current != index)
            {
                VisibleActivity.Move(current, index);
            }
        }

        NoFailedRuns.Visibility = failedOnly && desired.Count == 0 && ViewModel.ActivityItems.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityClearTitle"),
                Content = PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityClearHelp"),
                PrimaryButtonText = PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityClearConfirm"),
                CloseButtonText = PowerScriptsViewModel.GetResourceString("PowerScripts_Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await RefreshAsync(clearCompleted: true);
            }
        }
        finally
        {
            if (_isActive)
            {
                _refreshTimer.Start();
            }
        }
    }
}
