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
using RobocopyUI.Helpers;

namespace RobocopyUI;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
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

    private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        switch (sender.SelectedItem.Tag)
        {
            case "Options":
                OptionsContent.Visibility = Visibility.Visible;
                FiltersContent.Visibility = Visibility.Collapsed;
                LoggingContent.Visibility = Visibility.Collapsed;
                AdvancedContent.Visibility = Visibility.Collapsed;
                CommandPreviewContent.Visibility = Visibility.Collapsed;
                OutputContent.Visibility = Visibility.Collapsed;
                break;
            case "Filters":
                OptionsContent.Visibility = Visibility.Collapsed;
                FiltersContent.Visibility = Visibility.Visible;
                LoggingContent.Visibility = Visibility.Collapsed;
                AdvancedContent.Visibility = Visibility.Collapsed;
                CommandPreviewContent.Visibility = Visibility.Collapsed;
                OutputContent.Visibility = Visibility.Collapsed;
                break;
            case "Logging":
                OptionsContent.Visibility = Visibility.Collapsed;
                FiltersContent.Visibility = Visibility.Collapsed;
                LoggingContent.Visibility = Visibility.Visible;
                AdvancedContent.Visibility = Visibility.Collapsed;
                CommandPreviewContent.Visibility = Visibility.Collapsed;
                OutputContent.Visibility = Visibility.Collapsed;
                break;
            case "Advanced":
                OptionsContent.Visibility = Visibility.Collapsed;
                FiltersContent.Visibility = Visibility.Collapsed;
                LoggingContent.Visibility = Visibility.Collapsed;
                AdvancedContent.Visibility = Visibility.Visible;
                CommandPreviewContent.Visibility = Visibility.Collapsed;
                OutputContent.Visibility = Visibility.Collapsed;
                break;
            case "CommandPreview":
                OptionsContent.Visibility = Visibility.Collapsed;
                FiltersContent.Visibility = Visibility.Collapsed;
                LoggingContent.Visibility = Visibility.Collapsed;
                AdvancedContent.Visibility = Visibility.Collapsed;
                CommandPreviewContent.Visibility = Visibility.Visible;
                OutputContent.Visibility = Visibility.Collapsed;
                CommandPreviewTextBox.Text = GetCommandLine();
                break;
            case "Output":
                OptionsContent.Visibility = Visibility.Collapsed;
                FiltersContent.Visibility = Visibility.Collapsed;
                LoggingContent.Visibility = Visibility.Collapsed;
                AdvancedContent.Visibility = Visibility.Collapsed;
                CommandPreviewContent.Visibility = Visibility.Collapsed;
                OutputContent.Visibility = Visibility.Visible;
                break;
        }
    }

    private string GetCommandLine()
    {
        StringBuilder additionalArgs = new();

        // Aggregate from left column
        foreach (var option in OptionsListView.Items)
        {
            if (OptionsListView.ContainerFromItem(option) is ListViewItem { ContentTemplateRoot: Controls.OptionEntry entry })
            {
                additionalArgs.Append(entry.GetCommandLine());
                if (!string.IsNullOrEmpty(entry.GetCommandLine()))
                {
                    additionalArgs.Append(' ');
                }
            }
        }

        // Aggregate from right column
        foreach (var option in OptionsListViewRight.Items)
        {
            if (OptionsListViewRight.ContainerFromItem(option) is ListViewItem { ContentTemplateRoot: Controls.OptionEntry entry })
            {
                additionalArgs.Append(entry.GetCommandLine());
                if (!string.IsNullOrEmpty(entry.GetCommandLine()))
                {
                    additionalArgs.Append(' ');
                }
            }
        }

        foreach (var option in FilterOptionsListView.Items)
        {
            if (FilterOptionsListView.ContainerFromItem(option) is ListViewItem { ContentTemplateRoot: Controls.OptionEntry entry })
            {
                additionalArgs.Append(entry.GetCommandLine());
                if (!string.IsNullOrEmpty(entry.GetCommandLine()))
                {
                    additionalArgs.Append(' ');
                }
            }
        }

        foreach (var option in LoggingOptionsListView.Items)
        {
            if (LoggingOptionsListView.ContainerFromItem(option) is ListViewItem { ContentTemplateRoot: Controls.OptionEntry entry })
            {
                additionalArgs.Append(entry.GetCommandLine());
                if (!string.IsNullOrEmpty(entry.GetCommandLine()))
                {
                    additionalArgs.Append(' ');
                }
            }
        }

        foreach (var option in AdvancedOptionsListView.Items)
        {
            if (AdvancedOptionsListView.ContainerFromItem(option) is ListViewItem { ContentTemplateRoot: Controls.OptionEntry entry })
            {
                additionalArgs.Append(entry.GetCommandLine());
                if (!string.IsNullOrEmpty(entry.GetCommandLine()))
                {
                    additionalArgs.Append(' ');
                }
            }
        }

        return $"robocopy.exe {SourceTextBox.Text.Trim('"')} {DestinationTextBox.Text.Trim('"')} {additionalArgs}";
    }

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        RunRobocopy(GetCommandLine().Replace("robocopy.exe", string.Empty).Trim());
    }

    private void RunRobocopy(string arguments)
    {
        OutputTextBox.Text = string.Empty;
        OutputSelectorBarItem.IsEnabled = true;
        OutputSelectorBarItem.IsSelected = true;
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
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        FileSavePicker fileSavePicker = new(((Button)sender).XamlRoot.ContentIslandEnvironment.AppWindowId);
        fileSavePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        fileSavePicker.FileTypeChoices.Add("Robocopy options file", [".rcj"]);
        var result = await fileSavePicker.PickSaveFileAsync();

        if (result is null)
        {
            return;
        }

        RunRobocopy(GetCommandLine().Replace("robocopy.exe", string.Empty).Trim() + " /SAVE:" + result.Path[..^4] + " /QUIT" + (string.IsNullOrEmpty(SourceTextBox.Text) ? " /NOSD" : string.Empty) + (string.IsNullOrEmpty(DestinationTextBox.Text) ? " /NODD" : string.Empty));
    }

    private void SourceTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RunButton.IsEnabled = !string.IsNullOrWhiteSpace(SourceTextBox.Text) && !string.IsNullOrWhiteSpace(DestinationTextBox.Text);
    }

    private void SwapButton_Click(object sender, RoutedEventArgs e)
    {
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

    private async void LoadButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        FileOpenPicker fileOpenPicker = new(((Button)sender).XamlRoot.ContentIslandEnvironment.AppWindowId);
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
}
