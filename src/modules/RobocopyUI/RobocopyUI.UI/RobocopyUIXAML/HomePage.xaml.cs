// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

namespace RobocopyUI
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
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
            foreach (var option in OptionsContentLeft.Children)
            {
                if (option is OptionEntry entry)
                {
                    additionalArgs.Append(entry.GetCommandLine());
                    if (!string.IsNullOrEmpty(entry.GetCommandLine()))
                    {
                        additionalArgs.Append(' ');
                    }
                }
            }

            foreach (var option in OptionsContentRight.Children)
            {
                if (option is OptionEntry entry)
                {
                    additionalArgs.Append(entry.GetCommandLine());
                    if (!string.IsNullOrEmpty(entry.GetCommandLine()))
                    {
                        additionalArgs.Append(' ');
                    }
                }
            }

            foreach (var option in FilterOptions.Children)
            {
                if (option is OptionEntry entry)
                {
                    additionalArgs.Append(entry.GetCommandLine());
                    if (!string.IsNullOrEmpty(entry.GetCommandLine()))
                    {
                        additionalArgs.Append(' ');
                    }
                }
            }

            foreach (var option in LoggingOptions.Children)
            {
                if (option is OptionEntry entry)
                {
                    additionalArgs.Append(entry.GetCommandLine());
                    if (!string.IsNullOrEmpty(entry.GetCommandLine()))
                    {
                        additionalArgs.Append(' ');
                    }
                }
            }

            foreach (var option in AdvancedOptions.Children)
            {
                if (option is OptionEntry entry)
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
    }
}
