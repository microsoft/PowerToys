// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using RobocopyUI.Models;

namespace RobocopyUI;

public sealed partial class HomePage : Page
{
    private List<OptionContent>? mainOptions;
    private List<OptionContent>? filterOptions;
    private List<OptionContent>? loggingOptions;
    private List<OptionContent>? advancedOptions;

    public HomePage()
    {
        InitializeComponent();
        InflateOptions();
    }

    private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        switch (sender.SelectedItem.Tag)
        {
            case "Options":
                OptionsContent.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                FiltersContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                LoggingContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                AdvancedContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                CommandPreviewContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                OutputContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                break;
            case "Filters":
                OptionsContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                FiltersContent.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                LoggingContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                AdvancedContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                CommandPreviewContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                OutputContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                break;
            case "Logging":
                OptionsContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                FiltersContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                LoggingContent.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                AdvancedContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                CommandPreviewContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                OutputContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                break;
            case "Advanced":
                OptionsContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                FiltersContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                LoggingContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                AdvancedContent.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                CommandPreviewContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                OutputContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                break;
            case "CommandPreview":
                OptionsContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                FiltersContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                LoggingContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                AdvancedContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                CommandPreviewContent.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                OutputContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                CommandPreviewTextBox.Text = GetCommandLine();
                break;
            case "Output":
                OptionsContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                FiltersContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                LoggingContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                AdvancedContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                CommandPreviewContent.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                OutputContent.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                break;
        }
    }

    private string GetCommandLine()
    {
        StringBuilder additionalArgs = new();
        foreach (var option in OptionsListView.Items)
        {
            if (OptionsListView.ContainerFromItem(option) is ListViewItem container && container.ContentTemplateRoot is Controls.OptionEntry entry)
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
            if (FilterOptionsListView.ContainerFromItem(option) is ListViewItem container && container.ContentTemplateRoot is Controls.OptionEntry entry)
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
            if (LoggingOptionsListView.ContainerFromItem(option) is ListViewItem container && container.ContentTemplateRoot is Controls.OptionEntry entry)
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
            if (AdvancedOptionsListView.ContainerFromItem(option) is ListViewItem container && container.ContentTemplateRoot is Controls.OptionEntry entry)
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

    private void RunButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        RunRobocopy(GetCommandLine().Replace("robocopy.exe", string.Empty).Trim());
    }

    private void RunRobocopy(string arguments)
    {
        OutputTextBox.Text = string.Empty;
        OutputSelectorBarItem.IsEnabled = true;
        OutputSelectorBarItem.IsSelected = true;
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "robocopy.exe",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        Process process = new Process
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

    private async void SaveButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
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

    private void SwapButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        string temp = SourceTextBox.Text;
        SourceTextBox.Text = DestinationTextBox.Text;
        DestinationTextBox.Text = temp;
    }

    private async void SourceBrowseButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
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

    private async void DestinationBrowseButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
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

    private void InflateOptions()
    {
        mainOptions =
        [
            new OptionContent { OptionName = "/S" },
            new OptionContent { OptionName = "/E" },
            new OptionContent { OptionName = "/LEV", IsNumberOption = true },
            new OptionContent { OptionName = "/Z" },
            new OptionContent { OptionName = "/B" },
            new OptionContent { OptionName = "/ZB" },
            new OptionContent { OptionName = "/J" },
            new OptionContent { OptionName = "/EFSRAW" },
            new OptionContent
            {
                OptionName = "/COPY",
                IsMultiSelectOption = true,
                MultiSelectOptions =
                [
                    new OptionContent { OptionName = "D" },
                    new OptionContent { OptionName = "A" },
                    new OptionContent { OptionName = "T" },
                    new OptionContent { OptionName = "X" },
                    new OptionContent { OptionName = "S" },
                    new OptionContent { OptionName = "O" },
                    new OptionContent { OptionName = "U" },
                ],
            },
            new OptionContent { OptionName = "/NOCOPY" },
            new OptionContent { OptionName = "/SECFIX" },
            new OptionContent { OptionName = "/TIMFIX" },
            new OptionContent { OptionName = "/PURGE" },
            new OptionContent { OptionName = "/MIR" },
            new OptionContent { OptionName = "/MOV" },
            new OptionContent { OptionName = "/MOVE" },
            new OptionContent
            {
                OptionName = "/A+",
                IsMultiSelectOption = true,
                MultiSelectOptions =
                [
                    new OptionContent { OptionName = "R" },
                    new OptionContent { OptionName = "A" },
                    new OptionContent { OptionName = "S" },
                    new OptionContent { OptionName = "H" },
                    new OptionContent { OptionName = "C" },
                    new OptionContent { OptionName = "N" },
                    new OptionContent { OptionName = "E" },
                    new OptionContent { OptionName = "T" },
                ],
            },
            new OptionContent
            {
                OptionName = "/A-",
                IsMultiSelectOption = true,
                MultiSelectOptions =
                [
                    new OptionContent { OptionName = "R" },
                    new OptionContent { OptionName = "A" },
                    new OptionContent { OptionName = "S" },
                    new OptionContent { OptionName = "H" },
                    new OptionContent { OptionName = "C" },
                    new OptionContent { OptionName = "N" },
                    new OptionContent { OptionName = "E" },
                    new OptionContent { OptionName = "T" },
                    new OptionContent { OptionName = "O" },
                ],
            },
            new OptionContent { OptionName = "/CREATE" },
            new OptionContent { OptionName = "/FAT" },
            new OptionContent { OptionName = "/256" },
            new OptionContent { OptionName = "/MON", IsNumberOption = true },
            new OptionContent { OptionName = "/MOT", IsNumberOption = true },
            new OptionContent { OptionName = "/RH", IsRunHoursOption = true },
            new OptionContent { OptionName = "/PF" },
            new OptionContent { OptionName = "/IPG", IsNumberOption = true },
            new OptionContent { OptionName = "/SJ" },
            new OptionContent { OptionName = "/SL" },
            new OptionContent { OptionName = "/MT", IsNumberOption = true },
            new OptionContent
            {
                OptionName = "/DCOPY",
                IsMultiSelectOption = true,
                MultiSelectOptions =
                [
                    new OptionContent { OptionName = "D" },
                    new OptionContent { OptionName = "A" },
                    new OptionContent { OptionName = "T" },
                    new OptionContent { OptionName = "X" },
                    new OptionContent { OptionName = "E" },
                ],
            },
            new OptionContent { OptionName = "/NODCOPY" },
            new OptionContent { OptionName = "/NOOFFLOAD" },
            new OptionContent { OptionName = "/COMPRESS" },
            new OptionContent { OptionName = "/SPARSE:Y" },
            new OptionContent { OptionName = "/SPARSE:N" },
            new OptionContent { OptionName = "/NOCLONE" },
            new OptionContent { OptionName = "/IoMaxSize", IsNumberOption = true, IsStorageOption = true },
            new OptionContent { OptionName = "/IoRate", IsNumberOption = true, IsStorageOption = true },
            new OptionContent { OptionName = "/Threshold", IsNumberOption = true, IsStorageOption = true },
            new OptionContent { OptionName = "/R", IsNumberOption = true },
            new OptionContent { OptionName = "/W", IsNumberOption = true },
            new OptionContent { OptionName = "/REG" },
            new OptionContent { OptionName = "/TBD" },
            new OptionContent { OptionName = "/LFSM" },
            new OptionContent { OptionName = "/LFSM", IsNumberOption = true, IsStorageOption = true },
        ];

        filterOptions =
        [
            new OptionContent { OptionName = "/A" },
            new OptionContent { OptionName = "/M" },
            new OptionContent
            {
                OptionName = "/IA",
                IsMultiSelectOption = true,
                MultiSelectOptions =
                [
                    new OptionContent { OptionName = "R" },
                    new OptionContent { OptionName = "A" },
                    new OptionContent { OptionName = "S" },
                    new OptionContent { OptionName = "H" },
                    new OptionContent { OptionName = "C" },
                    new OptionContent { OptionName = "N" },
                    new OptionContent { OptionName = "E" },
                    new OptionContent { OptionName = "T" },
                    new OptionContent { OptionName = "O" },
                ],
            },
            new OptionContent
            {
                OptionName = "/XA",
                IsMultiSelectOption = true,
                MultiSelectOptions =
                [
                    new OptionContent { OptionName = "R" },
                    new OptionContent { OptionName = "A" },
                    new OptionContent { OptionName = "S" },
                    new OptionContent { OptionName = "H" },
                    new OptionContent { OptionName = "C" },
                    new OptionContent { OptionName = "N" },
                    new OptionContent { OptionName = "E" },
                    new OptionContent { OptionName = "T" },
                    new OptionContent { OptionName = "O" },
                ],
            },
            new OptionContent { OptionName = "/XC" },
            new OptionContent { OptionName = "/XN" },
            new OptionContent { OptionName = "/XO" },
            new OptionContent { OptionName = "/XX" },
            new OptionContent { OptionName = "/XL" },
            new OptionContent { OptionName = "/IS" },
            new OptionContent { OptionName = "/IT" },
            new OptionContent { OptionName = "/MAX", IsNumberOption = true },
            new OptionContent { OptionName = "/MIN", IsNumberOption = true },
            new OptionContent { OptionName = "/MAXAGE", IsNumberOption = true },
            new OptionContent { OptionName = "/MINAGE", IsNumberOption = true },
            new OptionContent { OptionName = "/MAXLAD", IsNumberOption = true },
            new OptionContent { OptionName = "/MINLAD", IsNumberOption = true },
            new OptionContent { OptionName = "/FFT" },
            new OptionContent { OptionName = "/DST" },
            new OptionContent { OptionName = "/XJ" },
            new OptionContent { OptionName = "/XJD" },
            new OptionContent { OptionName = "/XJF" },
            new OptionContent { OptionName = "/IM" },
            new OptionContent { OptionName = "/XF", IsTextOption = true },
            new OptionContent { OptionName = "/XD", IsTextOption = true },
        ];

        loggingOptions =
        [
            new OptionContent { OptionName = "/L" },
            new OptionContent { OptionName = "/X" },
            new OptionContent { OptionName = "/V" },
            new OptionContent { OptionName = "/TS" },
            new OptionContent { OptionName = "/FP" },
            new OptionContent { OptionName = "/BYTES" },
            new OptionContent { OptionName = "/NS" },
            new OptionContent { OptionName = "/NC" },
            new OptionContent { OptionName = "/NFL" },
            new OptionContent { OptionName = "/NDL" },
            new OptionContent { OptionName = "/NP" },
            new OptionContent { OptionName = "/ETA" },
            new OptionContent { OptionName = "/LOG", IsTextOption = true },
            new OptionContent { OptionName = "/LOG+", IsTextOption = true },
            new OptionContent { OptionName = "/UNILOG", IsTextOption = true },
            new OptionContent { OptionName = "/UNILOG+", IsTextOption = true },
            new OptionContent { OptionName = "/TEE" },
            new OptionContent { OptionName = "/NJH" },
            new OptionContent { OptionName = "/NJS" },
            new OptionContent { OptionName = "/UNICODE" },
        ];

        advancedOptions =
        [
            new OptionContent { OptionName = "/QUIT" },
            new OptionContent { OptionName = "/NOSD" },
            new OptionContent { OptionName = "/NODD" },
        ];

        OptionsListView.ItemsSource = mainOptions;
        FilterOptionsListView.ItemsSource = filterOptions;
        LoggingOptionsListView.ItemsSource = loggingOptions;
        AdvancedOptionsListView.ItemsSource = advancedOptions;
    }
}
