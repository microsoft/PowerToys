// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    private readonly RobocopyJob job = new();
    private bool syncing = true;
    private bool isSimpleMode = true;

    public HomePage()
    {
        InitializeComponent();

        syncing = true;
        isSimpleMode = SimpleModeSettings.GetIsSimpleMode();
        ModeSegmented.SelectedIndex = isSimpleMode ? 0 : 1;
        syncing = false;
        ApplyModeVisibility();
        UpdateCommandPreview();
        OutputStatusText.Text = ResourceLoaderInstance.ResourceLoader.GetString("Status_NotRunning");
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        OptionsListView.ItemsSource = OptionsDataHelper.GetMainOptionsLeftAsGroupedView();
        OptionsListViewRight.ItemsSource = OptionsDataHelper.GetMainOptionsRightAsGroupedView();
        FilterOptionsListView.ItemsSource = OptionsDataHelper.GetFilterOptionsAsGroupedView();
        LoggingOptionsListView.ItemsSource = OptionsDataHelper.GetLoggingOptionsAsGroupedView();
        AdvancedOptionsListView.ItemsSource = OptionsDataHelper.GetAdvancedOptionsAsGroupedView();

        if (isSimpleMode && job.Options.Count == 0)
        {
            ApplySimpleFromUi();
        }
        else
        {
            RefreshSimpleFromJob();
        }

        UpdateCommandPreview();
        base.OnNavigatedTo(e);
    }

    private void UpdateCommandPreview()
    {
        CommandPreviewTextBox.Text = job.RenderCommandLine();
        UpdateSimpleWarnings();
    }

    private void OptionEntry_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not OptionEntry entry || optionEntries.Contains(entry))
        {
            return;
        }

        optionEntries.Add(entry);
        entry.OptionChanged += OptionEntry_OptionChanged;
        ApplyJobValueToEntry(entry);
    }

    private void OptionEntry_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not OptionEntry entry)
        {
            return;
        }

        entry.OptionChanged -= OptionEntry_OptionChanged;
        optionEntries.Remove(entry);
    }

    private void OptionEntry_OptionChanged(object sender, EventArgs e)
    {
        if (syncing || sender is not OptionEntry entry)
        {
            return;
        }

        if (entry.TryGetPlanOption(out var option))
        {
            job.SetOption(option.Name, option.Value);
        }
        else if (!string.IsNullOrWhiteSpace(entry.OptionName))
        {
            job.RemoveOption(entry.OptionName);
        }

        RefreshEntriesFromJob();
        RefreshSimpleFromJob();
        UpdateCommandPreview();
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
        syncing = true;
        try
        {
            SourceTextBox.Text = plan.Source;
            DestinationTextBox.Text = plan.Destination;
            job.Source = plan.Source;
            job.Destination = plan.Destination;
            job.ReplaceOptions(plan.Options);
        }
        finally
        {
            syncing = false;
        }

        RefreshEntriesFromJob();
        RefreshSimpleFromJob();
        UpdateCommandPreview();
        RunButton.IsEnabled = !string.IsNullOrWhiteSpace(job.Source) && !string.IsNullOrWhiteSpace(job.Destination);
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
        return optionEntries;
    }

    private void ApplyJobValueToEntry(OptionEntry entry)
    {
        if (!job.HasOption(entry.OptionName))
        {
            entry.ClearSelection();
            return;
        }

        var value = job.GetValue(entry.OptionName);
        if (IsValueCompatible(entry, value))
        {
            entry.ApplyValue(value);
        }
        else
        {
            entry.ClearSelection();
        }
    }

    private void RefreshEntriesFromJob()
    {
        syncing = true;
        try
        {
            foreach (var entry in GetRealizedOptionEntries())
            {
                ApplyJobValueToEntry(entry);
            }
        }
        finally
        {
            syncing = false;
        }
    }

    private void ModeSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncing)
        {
            return;
        }

        isSimpleMode = ModeSegmented.SelectedIndex == 0;
        SimpleModeSettings.SetIsSimpleMode(isSimpleMode);

        if (isSimpleMode)
        {
            RefreshSimpleFromJob();
        }
        else
        {
            RefreshEntriesFromJob();
        }

        ApplyModeVisibility();
    }

    private void CustomizeInAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        syncing = true;
        ModeSegmented.SelectedIndex = 1;
        syncing = false;
        isSimpleMode = false;
        SimpleModeSettings.SetIsSimpleMode(false);
        RefreshEntriesFromJob();
        ApplyModeVisibility();
    }

    private void ApplyModeVisibility()
    {
        SimpleContent.Visibility = isSimpleMode ? Visibility.Visible : Visibility.Collapsed;
        AdvancedSelectorBar.Visibility = isSimpleMode ? Visibility.Collapsed : Visibility.Visible;

        SelectorPanels.Visibility = !isSimpleMode ? Visibility.Visible : Visibility.Collapsed;
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

        ApplyModeVisibility();
    }

    private void SimpleJobRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplySimpleFromUiIfInteractive();

    private void SimpleOptions_Changed(object sender, RoutedEventArgs e) => ApplySimpleFromUiIfInteractive();

    private void SimpleOptions_TextChanged(object sender, TextChangedEventArgs e) => ApplySimpleFromUiIfInteractive();

    private void SimpleNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => ApplySimpleFromUiIfInteractive();

    private void SimpleCopyFasterToggle_Toggled(object sender, RoutedEventArgs e)
    {
        SimpleThreadCountNumberBox.Visibility = SimpleCopyFasterToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        ApplySimpleFromUiIfInteractive();
    }

    private void SimpleRetryToggle_Toggled(object sender, RoutedEventArgs e)
    {
        SimpleRetryPanel.Visibility = SimpleRetryToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        ApplySimpleFromUiIfInteractive();
    }

    private async void SimpleLogToggle_Toggled(object sender, RoutedEventArgs e)
    {
        SimpleLogPanel.Visibility = SimpleLogToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        if (SimpleLogToggle.IsOn && string.IsNullOrWhiteSpace(SimpleLogPathTextBox.Text))
        {
            await PickLogFileAsync();
            if (string.IsNullOrWhiteSpace(SimpleLogPathTextBox.Text))
            {
                syncing = true;
                SimpleLogToggle.IsOn = false;
                syncing = false;
                SimpleLogPanel.Visibility = Visibility.Collapsed;
            }
        }

        ApplySimpleFromUiIfInteractive();
    }

    private void ApplySimpleFromUiIfInteractive()
    {
        if (syncing)
        {
            return;
        }

        ApplySimpleFromUi();
        RefreshEntriesFromJob();
        UpdateCommandPreview();
    }

    private void ApplySimpleFromUi()
    {
        var suppressNotifications = syncing;
        job.ApplySimple(GetSelectedJobKind(), ReadSimpleOptions());
        syncing = true;
        try
        {
            if (SimpleKeepAllPropertiesToggle.IsOn)
            {
                SimpleKeepPermissionsToggle.IsOn = false;
                SimpleKeepPermissionsToggle.IsEnabled = false;
            }
            else
            {
                SimpleKeepPermissionsToggle.IsEnabled = true;
            }
        }
        finally
        {
            syncing = suppressNotifications;
        }

        UpdateSimpleWarnings();
    }

    private SimpleCopyTaskKind GetSelectedJobKind()
    {
        if (SimpleJobRadioButtons.SelectedItem is RadioButton { Tag: string tag }
            && Enum.TryParse(tag, out SimpleCopyTaskKind kind))
        {
            return kind;
        }

        return SimpleCopyTaskKind.CopyFilesAndFolders;
    }

    private SimpleCopyOptions ReadSimpleOptions()
    {
        return new SimpleCopyOptions
        {
            KeepPermissions = SimpleKeepPermissionsToggle.IsOn,
            KeepAllProperties = SimpleKeepAllPropertiesToggle.IsOn,
            SkipNewerAtDestination = SimpleSkipNewerToggle.IsOn,
            ExcludeFileTypes = SimpleExcludeFilesTextBox.Text,
            ExcludeFolders = SimpleExcludeFoldersTextBox.Text,
            CopyFaster = SimpleCopyFasterToggle.IsOn,
            ThreadCount = ToCount(SimpleThreadCountNumberBox.Value, SimpleCopyTask.DefaultThreadCount),
            RetryFailedFiles = SimpleRetryToggle.IsOn,
            RetryCount = ToCount(SimpleRetryCountNumberBox.Value, SimpleCopyTask.DefaultRetryCount),
            RetryWaitSeconds = ToCount(SimpleRetryWaitNumberBox.Value, SimpleCopyTask.DefaultRetryWaitSeconds),
            PreviewOnly = SimplePreviewToggle.IsOn,
            WriteLog = SimpleLogToggle.IsOn,
            LogPath = SimpleLogPathTextBox.Text,
        };
    }

    private void RefreshSimpleFromJob()
    {
        syncing = true;
        try
        {
            var snapshot = job.InferSimple();
            SelectJobRadio(snapshot.Kind);

            SimpleKeepAllPropertiesToggle.IsOn = snapshot.Options.KeepAllProperties;
            SimpleKeepPermissionsToggle.IsOn = snapshot.Options.KeepPermissions;
            SimpleKeepPermissionsToggle.IsEnabled = !snapshot.Options.KeepAllProperties;
            SimpleSkipNewerToggle.IsOn = snapshot.Options.SkipNewerAtDestination;
            SimpleExcludeFilesTextBox.Text = snapshot.Options.ExcludeFileTypes;
            SimpleExcludeFoldersTextBox.Text = snapshot.Options.ExcludeFolders;
            SimpleCopyFasterToggle.IsOn = snapshot.Options.CopyFaster;
            SimpleThreadCountNumberBox.Value = snapshot.Options.ThreadCount;
            SimpleThreadCountNumberBox.Visibility = snapshot.Options.CopyFaster ? Visibility.Visible : Visibility.Collapsed;
            SimpleRetryToggle.IsOn = snapshot.Options.RetryFailedFiles;
            SimpleRetryCountNumberBox.Value = snapshot.Options.RetryCount;
            SimpleRetryWaitNumberBox.Value = snapshot.Options.RetryWaitSeconds;
            SimpleRetryPanel.Visibility = snapshot.Options.RetryFailedFiles ? Visibility.Visible : Visibility.Collapsed;
            SimplePreviewToggle.IsOn = snapshot.Options.PreviewOnly;
            SimpleLogToggle.IsOn = snapshot.Options.WriteLog && !string.IsNullOrWhiteSpace(snapshot.Options.LogPath);
            SimpleLogPathTextBox.Text = snapshot.Options.LogPath;
            SimpleLogPanel.Visibility = SimpleLogToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
            SimpleAdditionalOptionsText.Visibility = snapshot.HasAdditionalOptions ? Visibility.Visible : Visibility.Collapsed;
            UpdateSimpleWarnings();
        }
        finally
        {
            syncing = false;
        }
    }

    private void SelectJobRadio(SimpleCopyTaskKind kind)
    {
        var tag = kind.ToString();
        foreach (var item in SimpleJobRadioButtons.Items.OfType<RadioButton>())
        {
            item.IsChecked = string.Equals(item.Tag as string, tag, StringComparison.Ordinal);
        }
    }

    private void UpdateSimpleWarnings()
    {
        var warnings = RobocopyCommand.GetDestructiveWarnings(
            job.Options,
            key => ResourceLoaderInstance.ResourceLoader.GetString(key));
        SimpleWarningInfoBar.IsOpen = warnings.Count > 0;
        SimpleWarningInfoBar.Message = warnings.Count > 0 ? string.Join(' ', warnings) : string.Empty;
    }

    private static int ToCount(double value, int fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
        {
            return fallback;
        }

        return (int)value;
    }

    private void SourceDestTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        job.Source = SourceTextBox.Text;
        job.Destination = DestinationTextBox.Text;
        UpdateCommandPreview();
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

    private async void SimpleLogBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        await PickLogFileAsync();
    }

    private async Task PickLogFileAsync()
    {
        FileSavePicker fileSavePicker = new(SimpleLogBrowseButton.XamlRoot.ContentIslandEnvironment.AppWindowId);
        fileSavePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        fileSavePicker.FileTypeChoices.Add(ResourceLoaderInstance.ResourceLoader.GetString("SimpleLogFileType"), [".log", ".txt"]);
        var result = await fileSavePicker.PickSaveFileAsync();
        if (result is not null)
        {
            SimpleLogPathTextBox.Text = result.Path;
        }
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

        RunRobocopy(job.RenderArguments() + " /SAVE:" + result.Path[..^4] + " /QUIT" + (string.IsNullOrEmpty(SourceTextBox.Text) ? " /NOSD" : string.Empty) + (string.IsNullOrEmpty(DestinationTextBox.Text) ? " /NODD" : string.Empty));
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

        var options = commands
            .Where(command => command.Command is not ("NOSD" or "NODD" or "SD" or "DD" or "SAVE" or "QUIT"))
            .Select(command => new RobocopyPlanOption("/" + command.Command, command.Argument ?? string.Empty))
            .ToList();

        job.ReplaceOptions(options);

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

        RefreshEntriesFromJob();
        RefreshSimpleFromJob();
        UpdateCommandPreview();
    }

    private void RunButton_Click(SplitButton sender, SplitButtonClickEventArgs e)
    {
        RunRobocopy(job.RenderArguments());
    }

    private void RunRobocopy(string arguments)
    {
        OutputTextBox.Text = string.Empty;
        StatusCodeText.Text = "-";
        OutputStatusText.Text = ResourceLoaderInstance.ResourceLoader.GetString("Status_Running");
        CommandOutputExpander.IsEnabled = true;

        ApplyModeVisibility();
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
        var completionLock = new object();
        var processExited = false;
        var outputCompleted = false;
        var errorCompleted = false;
        var completionReported = false;

        void TryUpdateCompletedStatus()
        {
            lock (completionLock)
            {
                if (!processExited || !outputCompleted || !errorCompleted || completionReported)
                {
                    return;
                }

                completionReported = true;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                StatusCodeText.Text = process.ExitCode.ToString(CultureInfo.InvariantCulture);
                var statusKey = RobocopyExecutionHelper.GetStatusResourceKey(process.ExitCode);
                var statusText = ResourceLoaderInstance.ResourceLoader.GetString(statusKey);
                OutputStatusText.Text = string.IsNullOrWhiteSpace(statusText)
                    ? ResourceLoaderInstance.ResourceLoader.GetString("Status_Fail")
                    : statusText;
            });
        }

        process.OutputDataReceived += (s, args) =>
        {
            if (args.Data == null)
            {
                lock (completionLock)
                {
                    outputCompleted = true;
                }

                TryUpdateCompletedStatus();
                return;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                if (string.IsNullOrEmpty(args.Data))
                {
                    return;
                }

                OutputTextBox.Text += args.Data + Environment.NewLine;
            });
        };
        process.ErrorDataReceived += (s, args) =>
        {
            if (args.Data == null)
            {
                lock (completionLock)
                {
                    errorCompleted = true;
                }

                TryUpdateCompletedStatus();
                return;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                if (string.IsNullOrEmpty(args.Data))
                {
                    return;
                }

                OutputTextBox.Text += args.Data + Environment.NewLine;
            });
        };
        process.Exited += (s, args) =>
        {
            lock (completionLock)
            {
                processExited = true;
            }

            TryUpdateCompletedStatus();
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private void RunExternalButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "robocopy.exe",
                Arguments = job.RenderArguments(),
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal,
            };

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start robocopy: {ex}");
        }
    }
}
