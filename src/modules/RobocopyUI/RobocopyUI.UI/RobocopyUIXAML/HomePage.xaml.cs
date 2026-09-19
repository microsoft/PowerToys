// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.Storage.Pickers;
using RobocopyUI.Controls;
using RobocopyUI.Helpers;
using RobocopyUI.Models;
using RobocopyUI.Services.AI;

namespace RobocopyUI;

public sealed partial class HomePage : Page
{
    private readonly List<OptionEntry> optionEntries = [];

    public HomePage()
    {
        InitializeComponent();
        OutputStatusText.Text = ResourceLoaderInstance.ResourceLoader.GetString("Status_NotRunning");
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        OptionsListView.ItemsSource = OptionsDataHelper.GetMainOptionsLeftAsGroupedView();
        OptionsListViewRight.ItemsSource = OptionsDataHelper.GetMainOptionsRightAsGroupedView();
        FilterOptionsListView.ItemsSource = OptionsDataHelper.GetFilterOptionsAsGroupedView();
        LoggingOptionsListView.ItemsSource = OptionsDataHelper.GetLoggingOptionsAsGroupedView();
        AdvancedOptionsListView.ItemsSource = OptionsDataHelper.GetAdvancedOptionsAsGroupedView();

        base.OnNavigatedTo(e);
    }

    private void UpdateCommandPreview(object sender, EventArgs e)
    {
        CommandPreviewTextBox.Text = GetFullCommandLine();
    }

    private string GetFullCommandLine()
    {
        StringBuilder additionalArgs = new();

        foreach (var entry in optionEntries)
        {
            if (!string.IsNullOrEmpty(entry.CommandLineContent))
            {
                additionalArgs.Append(entry.CommandLineContent);
                additionalArgs.Append(' ');
            }
        }

        return $"robocopy.exe {SourceTextBox.Text.Trim('"')} {DestinationTextBox.Text.Trim('"')} {additionalArgs}";
    }

    private void OptionEntry_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not OptionEntry entry || optionEntries.Contains(entry))
        {
            return;
        }

        optionEntries.Add(entry);
        entry.OptionChanged += UpdateCommandPreview;
    }

    private void OptionEntry_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not OptionEntry entry)
        {
            return;
        }

        entry.OptionChanged -= UpdateCommandPreview;
        optionEntries.Remove(entry);
    }

    private async void AIAssistButton_Click(object sender, RoutedEventArgs e)
    {
        var catalog = GetOptionCatalog();

        var dialog = new AICommandDialog(catalog, SourceTextBox.Text, DestinationTextBox.Text)
        {
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && dialog.AcceptedPlan is RobocopyPlan plan)
        {
            ApplyPlan(plan);
        }
    }

    /// <summary>
    /// Builds the catalog of every known option, from the underlying data rather than the realized
    /// controls, so the AI model sees switches even on tabs that haven't been visited yet.
    /// </summary>
    private static List<RobocopyOptionDescriptor> GetOptionCatalog()
    {
        var allOptions = OptionsDataHelper.GetMainOptionsLeft()
            .Concat(OptionsDataHelper.GetMainOptionsRight())
            .Concat(OptionsDataHelper.GetFilterOptions())
            .Concat(OptionsDataHelper.GetLoggingOptions())
            .Concat(OptionsDataHelper.GetAdvancedOptions());

        return allOptions.Select(ToDescriptor).ToList();
    }

    private static RobocopyOptionDescriptor ToDescriptor(OptionContent option)
    {
        var kind = option.IsStorageOption ? RobocopyOptionKind.Storage
            : option.IsNumberOption ? RobocopyOptionKind.Number
            : option.IsTextOption ? RobocopyOptionKind.Text
            : option.IsMultiSelectOption ? RobocopyOptionKind.MultiSelect
            : option.IsRunHoursOption ? RobocopyOptionKind.RunHours
            : RobocopyOptionKind.Flag;

        return new RobocopyOptionDescriptor(
            option.OptionName,
            kind,
            option.OptionDescription,
            option.MultiSelectOptions?.Select(sub => new RobocopyOptionValue(sub.OptionName, sub.OptionDescription)).ToList() ?? []);
    }

    /// <summary>
    /// Applies a generated plan to the UI so the user can review and tweak it before running.
    /// </summary>
    private void ApplyPlan(RobocopyPlan plan)
    {
        SourceTextBox.Text = plan.Source;
        DestinationTextBox.Text = plan.Destination;

        var planOptions = plan.Options.ToDictionary(option => option.Name, option => option.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in GetRealizedOptionEntries())
        {
            if (planOptions.TryGetValue(entry.OptionName, out var value) && IsValueCompatible(entry, value))
            {
                entry.ApplyValue(value);
            }
            else
            {
                entry.ClearSelection();
            }
        }

        CommandPreviewTextBox.Text = GetFullCommandLine();
    }

    /// <summary>
    /// A switch name can be bound to more than one control (for example /LFSM exists both as a
    /// plain flag and as a storage option), so only apply the value to the matching variant.
    /// </summary>
    private static bool IsValueCompatible(OptionEntry entry, string value)
    {
        return entry.GetOptionKind() == RobocopyOptionKind.Flag
            ? string.IsNullOrEmpty(value)
            : !string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Enumerates every option control that is currently realized across all tabs, so a plan can be
    /// applied to (or cleared from) any of them regardless of which tab is on screen.
    /// </summary>
    private IEnumerable<OptionEntry> GetRealizedOptionEntries()
    {
        ListView[] optionLists = [OptionsListView, OptionsListViewRight, FilterOptionsListView, LoggingOptionsListView, AdvancedOptionsListView];

        foreach (var listView in optionLists)
        {
            foreach (var item in listView.Items)
            {
                if (listView.ContainerFromItem(item) is ListViewItem { ContentTemplateRoot: OptionEntry entry })
                {
                    yield return entry;
                }
            }
        }
    }

    private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        switch (sender.SelectedItem.Tag)
        {
            case "Options":
                OptionsContent.Visibility = Visibility.Visible;
                FiltersContent.Visibility = Visibility.Collapsed;
                LoggingContent.Visibility = Visibility.Collapsed;
                AdvancedContent.Visibility = Visibility.Collapsed;
                break;
            case "Filters":
                OptionsContent.Visibility = Visibility.Collapsed;
                FiltersContent.Visibility = Visibility.Visible;
                LoggingContent.Visibility = Visibility.Collapsed;
                AdvancedContent.Visibility = Visibility.Collapsed;
                break;
            case "Logging":
                OptionsContent.Visibility = Visibility.Collapsed;
                FiltersContent.Visibility = Visibility.Collapsed;
                LoggingContent.Visibility = Visibility.Visible;
                AdvancedContent.Visibility = Visibility.Collapsed;
                break;
            case "Advanced":
                OptionsContent.Visibility = Visibility.Collapsed;
                FiltersContent.Visibility = Visibility.Collapsed;
                LoggingContent.Visibility = Visibility.Collapsed;
                AdvancedContent.Visibility = Visibility.Visible;
                break;
            case "Output":
                OptionsContent.Visibility = Visibility.Collapsed;
                FiltersContent.Visibility = Visibility.Collapsed;
                LoggingContent.Visibility = Visibility.Collapsed;
                AdvancedContent.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private void SourceDestTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateCommandPreview(this, EventArgs.Empty);
        RunButton.IsEnabled = !string.IsNullOrWhiteSpace(SourceTextBox.Text) && !string.IsNullOrWhiteSpace(DestinationTextBox.Text);
    }

    private void SwapButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateCommandPreview(this, EventArgs.Empty);
        (SourceTextBox.Text, DestinationTextBox.Text) = (DestinationTextBox.Text, SourceTextBox.Text);
    }

    private async void SourceBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var folderPicker = new FolderPicker(button.XamlRoot.ContentIslandEnvironment.AppWindowId);
        folderPicker.Title = "Select source";
        var result = await folderPicker.PickSingleFolderAsync();
        if (result is null)
        {
            return;
        }

        SourceTextBox.Text = result.Path;
    }

    private async void DestinationBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var folderPicker = new FolderPicker(button.XamlRoot.ContentIslandEnvironment.AppWindowId);
        folderPicker.Title = "Select destination";
        var result = await folderPicker.PickSingleFolderAsync();
        if (result is null)
        {
            return;
        }

        DestinationTextBox.Text = result.Path;
    }

    private async void SaveOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        FileSavePicker fileSavePicker = new(((MenuFlyoutItem)sender).XamlRoot.ContentIslandEnvironment.AppWindowId);
        fileSavePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        fileSavePicker.FileTypeChoices.Add("Robocopy options file", [".rcj"]);
        var result = await fileSavePicker.PickSaveFileAsync();

        if (result is null)
        {
            return;
        }

        RunRobocopy(GetFullCommandLine().Replace("robocopy.exe", string.Empty).Trim() + " /SAVE:" + result.Path[..^4] + " /QUIT" + (string.IsNullOrEmpty(SourceTextBox.Text) ? " /NOSD" : string.Empty) + (string.IsNullOrEmpty(DestinationTextBox.Text) ? " /NODD" : string.Empty));
    }

    private async void LoadOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        FileOpenPicker fileOpenPicker = new(((MenuFlyoutItem)sender).XamlRoot.ContentIslandEnvironment.AppWindowId);
        fileOpenPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        fileOpenPicker.FileTypeFilter.Add(".rcj");
        var result = await fileOpenPicker.PickSingleFileAsync();
        if (result is null)
        {
            return;
        }

        RCJParser parser = new(await File.ReadAllTextAsync(result.Path));

        var commands = parser.Parse();

        if (commands is null || commands.Length == 0)
        {
            return;
        }

        void SetOptionContent(Controls.OptionEntry optionEntry, RCJParser.RCJCommand command)
        {
            optionEntry.IsSelected = true;
            if (command.Argument is null)
            {
                return;
            }

            if (optionEntry.IsStorageOption)
            {
                string numberPart = new string(command.Argument.TakeWhile(char.IsDigit).ToArray());
                string unitPart = command.Argument.Substring(numberPart.Length);
                optionEntry.StorageUnit = unitPart;
                if (int.TryParse(numberPart, out int number))
                {
                    optionEntry.NumberValue = number;
                }
            }

            if (optionEntry.IsNumberOption)
            {
                if (int.TryParse(command.Argument, out int number))
                {
                    optionEntry.NumberValue = number;
                }
            }

            if (optionEntry.IsTextOption)
            {
                optionEntry.TextValue = command.Argument;
            }

            if (optionEntry.IsRunHoursOption)
            {
                var parts = command.Argument.Split('-');
                if (parts.Length == 2 && parts.All(p => p.Length == 4 && int.TryParse(p, out _)))
                {
                    optionEntry.StartHour = int.Parse(parts[0][0..2], NumberStyles.None, CultureInfo.InvariantCulture);
                    optionEntry.EndHour = int.Parse(parts[1][0..2], NumberStyles.None, CultureInfo.InvariantCulture);
                    optionEntry.StartMinute = int.Parse(parts[0][2..4], NumberStyles.None, CultureInfo.InvariantCulture);
                    optionEntry.EndMinute = int.Parse(parts[1][2..4], NumberStyles.None, CultureInfo.InvariantCulture);
                }
            }

            if (optionEntry.IsMultiSelectOption)
            {
                optionEntry.SelectedItems = command.Argument;
            }
        }

        // Aggregate from left column
        foreach (var option in OptionsListView.Items)
        {
            if (OptionsListView.ContainerFromItem(option) is ListViewItem { ContentTemplateRoot: Controls.OptionEntry entry } && commands.Any(e => "/" + e.Command == entry.OptionName))
            {
                SetOptionContent(entry, commands.First(e => "/" + e.Command == entry.OptionName));
            }
        }

        // Aggregate from right column
        foreach (var option in OptionsListViewRight.Items)
        {
            if (OptionsListViewRight.ContainerFromItem(option) is ListViewItem { ContentTemplateRoot: Controls.OptionEntry entry } && commands.Any(e => "/" + e.Command == entry.OptionName))
            {
                SetOptionContent(entry, commands.First(e => "/" + e.Command == entry.OptionName));
            }
        }

        foreach (var option in FilterOptionsListView.Items)
        {
            if (FilterOptionsListView.ContainerFromItem(option) is ListViewItem { ContentTemplateRoot: Controls.OptionEntry entry } && commands.Any(e => "/" + e.Command == entry.OptionName))
            {
                SetOptionContent(entry, commands.First(e => "/" + e.Command == entry.OptionName));
            }
        }

        foreach (var option in LoggingOptionsListView.Items)
        {
            if (LoggingOptionsListView.ContainerFromItem(option) is ListViewItem { ContentTemplateRoot: Controls.OptionEntry entry } && commands.Any(e => "/" + e.Command == entry.OptionName))
            {
                SetOptionContent(entry, commands.First(e => "/" + e.Command == entry.OptionName));
            }
        }

        foreach (var option in AdvancedOptionsListView.Items)
        {
            if (AdvancedOptionsListView.ContainerFromItem(option) is ListViewItem { ContentTemplateRoot: Controls.OptionEntry entry } && commands.Any(e => "/" + e.Command == entry.OptionName))
            {
                SetOptionContent(entry, commands.First(e => "/" + e.Command == entry.OptionName));
            }
        }

        if (commands.Any(e => e.Command == "NOSD") && string.IsNullOrWhiteSpace(SourceTextBox.Text))
        {
            SourceTextBox.Text = string.Empty;
        }

        if (commands.Any(e => e.Command == "NODD") && string.IsNullOrWhiteSpace(DestinationTextBox.Text))
        {
            DestinationTextBox.Text = string.Empty;
        }

        if (commands.Any(e => e.Command == "SD") && commands.First(e => e.Command == "SD").Argument is not null)
        {
            SourceTextBox.Text = commands.First(e => e.Command == "SD").Argument;
        }

        if (commands.Any(e => e.Command == "DD") && commands.First(e => e.Command == "DD").Argument is not null)
        {
            DestinationTextBox.Text = commands.First(e => e.Command == "DD").Argument;
        }
    }

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        RunRobocopy(GetFullCommandLine().Replace("robocopy.exe", string.Empty).Trim());
    }

    private void RunRobocopy(string arguments)
    {
        OutputTextBox.Text = string.Empty;

        CommandOutputExpander.IsEnabled = true;
        StatusCodeText.Text = "-";
        OutputStatusText.Text = ResourceLoaderInstance.ResourceLoader.GetString("Status_Running");

        var startInfo = new ProcessStartInfo
        {
            FileName = "robocopy.exe",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        process.OutputDataReceived += (s, args) =>
        {
            if (args.Data != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (string.IsNullOrEmpty(args.Data))
                    {
                        return;
                    }

                    OutputTextBox.Text += args.Data + Environment.NewLine;
                });
            }
        };
        process.ErrorDataReceived += (s, args) =>
        {
            if (args.Data != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (string.IsNullOrEmpty(args.Data))
                    {
                        return;
                    }

                    OutputTextBox.Text += args.Data + Environment.NewLine;
                });
            }
        };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.Exited += (s, args) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusCodeText.Text = process.ExitCode.ToString(CultureInfo.InvariantCulture);
                OutputStatusText.Text = ResourceLoaderInstance.ResourceLoader.GetString("Status_" + (process.ExitCode <= 8 ? process.ExitCode : "Fail"));
            });
        };
    }
}
