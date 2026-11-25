// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PowerScripts.UI.ViewModels;

namespace PowerScripts.Editor.Views;

public sealed partial class PowerScriptsActivityPage : Page
{
    private const string RunningGroupKey = "now";

    private static readonly string[] KnownStatuses = ["running", "succeeded", "failed", "interrupted"];
    private static readonly CompositeFormat DayFormat = CompositeFormat.Parse(PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityDayFormat"));

    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _isActive;
    private bool _hasRefreshed;

    private PowerScriptsViewModel ViewModel { get; set; }

    private ObservableCollection<PowerScriptActivityGroup> Groups { get; } = new();

    private bool ShowFailedOnly => ActivityFilter.SelectedIndex == 1;

    private string SelectedSource => (SourceFilter.SelectedItem as ComboBoxItem)?.Tag as string;

    public PowerScriptsActivityPage()
    {
        InitializeComponent();
        SourceFilter.Items.Add(new ComboBoxItem { Content = PowerScriptsViewModel.GetResourceString("PowerScripts_ActivitySourceAny") });
        foreach (var source in PowerScriptActivityItem.KnownSources)
        {
            SourceFilter.Items.Add(new ComboBoxItem { Content = PowerScriptActivityItem.GetSourceName(source), Tag = source });
        }

        SourceFilter.SelectedIndex = 0;
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

    // Succeeded runs show no status text; failures read critical, interruptions and unknowns read caution.
    public static Visibility StatusTextVisibility(string status, string tone)
    {
        var visible = tone switch
        {
            "critical" => IsStatus(status, "failed"),
            "caution" => IsStatus(status, "interrupted") || IsStatus(status, "unknown"),
            _ => IsStatus(status, "running"),
        };

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = e.Parameter as PowerScriptsViewModel ??
            throw new ArgumentException("Activity navigation requires the PowerScripts view model.", nameof(e));
        Bindings.Update();
    }

    private static bool IsProblem(PowerScriptActivityItem item) =>
        item.Status is not "succeeded" and not "running" || item.HasExitCode;

    private static string GroupKey(PowerScriptActivityItem item) =>
        item.IsRunning ? RunningGroupKey : item.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string GroupHeader(PowerScriptActivityItem item)
    {
        if (item.IsRunning)
        {
            return PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityNow");
        }

        var day = item.StartedUtc.ToLocalTime().Date;
        var today = DateTime.Today;
        if (day == today)
        {
            return PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityToday");
        }

        if (day == today.AddDays(-1))
        {
            return PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityYesterday");
        }

        return day.Year == today.Year
            ? string.Format(CultureInfo.CurrentCulture, DayFormat, day)
            : day.ToString("D", CultureInfo.CurrentCulture);
    }

    // Moves items in place so rows keep their containers (and focus) across the periodic refresh.
    private static void Reconcile<T>(ObservableCollection<T> target, IReadOnlyList<T> desired)
    {
        var desiredSet = desired.ToHashSet();
        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (!desiredSet.Contains(target[index]))
            {
                target.RemoveAt(index);
            }
        }

        for (var index = 0; index < desired.Count; index++)
        {
            var current = target.IndexOf(desired[index]);
            if (current < 0)
            {
                target.Insert(index, desired[index]);
            }
            else if (current != index)
            {
                target.Move(current, index);
            }
        }
    }

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
        _hasRefreshed = true;
        SyncVisibleActivity();
    }

    private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null)
        {
            SyncVisibleActivity();
        }
    }

    private void ShowAll_Click(object sender, RoutedEventArgs e)
    {
        ActivityFilter.SelectedIndex = 0;
        SourceFilter.SelectedIndex = 0;
    }

    private void SyncVisibleActivity()
    {
        var failedOnly = ShowFailedOnly;
        var source = SelectedSource;
        var visible = ViewModel.ActivityItems
            .Where(item => (!failedOnly || IsProblem(item)) && (source is null || item.ConsumerId == source))
            .OrderByDescending(item => item.IsRunning)
            .ThenByDescending(item => item.StartedUtc)
            .ToList();

        var desiredGroups = new List<PowerScriptActivityGroup>();
        foreach (var day in visible.GroupBy(GroupKey))
        {
            var group = Groups.FirstOrDefault(existing => existing.Key == day.Key) ?? new PowerScriptActivityGroup { Key = day.Key };
            group.Header = GroupHeader(day.First());
            Reconcile(group.Items, day.ToList());
            desiredGroups.Add(group);
        }

        Reconcile(Groups, desiredGroups);

        var hasHistory = ViewModel.ActivityItems.Count > 0;
        ClearButton.IsEnabled = hasHistory;
        EmptyState.Visibility = _hasRefreshed && !hasHistory ? Visibility.Visible : Visibility.Collapsed;
        NoMatches.Visibility = hasHistory && visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ActivityRow_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not PowerScriptActivityItem run)
        {
            return;
        }

        var script = ViewModel.Scripts.FirstOrDefault(candidate => string.Equals(candidate.Id, run.ScriptId, StringComparison.OrdinalIgnoreCase));
        if (script is null)
        {
            return;
        }

        try
        {
            if (!Frame.Navigate(typeof(PowerScriptSettingsPage), new PowerScriptSettingsPage.NavigationContext(ViewModel, script)))
            {
                throw new InvalidOperationException(PowerScriptsViewModel.GetResourceString("PowerScripts_SettingsNavigationFailed"));
            }
        }
        catch (Exception exception)
        {
            Logger.LogError("Unable to open a PowerScript from Activity.", exception);
        }
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
