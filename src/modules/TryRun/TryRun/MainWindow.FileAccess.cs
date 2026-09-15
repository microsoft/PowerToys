// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Windows;
using Microsoft.Win32;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun;

public partial class MainWindow
{
    private FileAccessRun? fileAccessRun;
    private RunEnvironment? fileAccessEnvironment;

    private void OnViewBlockedAccess(object sender, RoutedEventArgs e) => ResultsTabs.SelectedItem = IsolationTab;

    private async void OnReviewFileRead(object sender, RoutedEventArgs e)
    {
        await ReviewFileReadAsync((grant, request) => new FileAccessGrantWindow(grant, request) { Owner = this }.ShowDialog() == true);
    }

    private async void OnChooseReadOnlyFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Choose one file to allow this program to read", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(this) == true)
        {
            await ReviewSelectedFileAsync(dialog.FileName, (grant, request) => new FileAccessGrantWindow(grant, request) { Owner = this }.ShowDialog() == true);
        }
    }

    internal async Task ReviewSelectedFileAsync(string path, Func<FileReadGrant, ExecutionRequest, bool> confirm)
    {
        if (cancellation is not null || !showingRun || fileAccessRun is not { } previous || fileAccessEnvironment is null)
        {
            return;
        }

        try
        {
            using var grant = FileReadGrant.CreateSelectedFile(path, previous.Request, fileAccessEnvironment);
            if (confirm(grant, previous.Request))
            {
                await RunConfiguredAsync(previous with { Request = previous.Request with { Policy = grant.Policy } });
            }
        }
        catch (Exception exception)
        {
            FileAccessHint.Text = "Read-only access was not added. " + exception.Message;
        }
    }

    internal async Task ReviewFileReadAsync(Func<FileReadGrant, ExecutionRequest, bool> confirm)
    {
        if (cancellation is not null || !showingRun || fileAccessRun is not { } previous || fileAccessEnvironment is null ||
            IsolationGrid.SelectedItem is not IsolationEvent observation || isolationReport?.Events.Contains(observation) != true ||
            isolationReport.CaptureStatus is not "Complete" and not "Partial")
        {
            return;
        }

        try
        {
            using var grant = FileReadGrant.Create(observation, previous.Request, fileAccessEnvironment);
            if (confirm(grant, previous.Request))
            {
                var retry = previous with { Request = previous.Request with { Policy = grant.Policy } };
                await RunConfiguredAsync(retry);
            }
        }
        catch (Exception exception)
        {
            FileAccessHint.Text = "Read-only access was not added. " + exception.Message;
        }
    }

    private void UpdateFileAccessActions()
    {
        if (ReviewFileReadButton is null)
        {
            return;
        }

        var blocked = isolationReport?.Events.Count(item => item.NativeDenial is not null) ?? 0;
        var canChoose = cancellation is null && showingRun && fileAccessRun?.Request.IsLinux == false && fileAccessEnvironment is not null && fileAccessRun.Request.Policy?.Get("captureMode") == "Block";
        ViewBlockedAccessButton.Visibility = blocked > 0 || canChoose ? Visibility.Visible : Visibility.Collapsed;
        ViewBlockedAccessButton.Content = blocked > 0 ? $"View blocked access ({blocked})" : "Review file access";
        ChooseReadOnlyFileButton.IsEnabled = canChoose;
        var selected = IsolationGrid.SelectedItem as IsolationEvent;
        ReviewFileReadButton.IsEnabled = cancellation is null && showingRun && fileAccessRun?.Request.IsLinux == false && fileAccessEnvironment is not null &&
            isolationReport?.CaptureStatus is "Complete" or "Partial" && selected is not null && FileReadGrant.CanReview(selected);
        FileAccessHint.Text = cancellation is not null
            ? "Access remains restricted. Finish or stop the run to collect its report and review individual file requests."
            : selected?.NativeDenial is { } denial
                ? denial.ResourceType == "file" && denial.Access == "read"
                    ? "This read was blocked by MXC. Review the exact file before adding read-only access for a new run."
                    : "This access was blocked by MXC. This step supports read-only grants for individual files; writes, folders and other resources require separate review."
                : "Select a native blocked file-read record, or choose a file yourself. MXC can omit file denials; choosing a file is an explicit new grant, not confirmation of the error's cause.";
    }

    private static string ResourceDetail(IsolationEvent observation)
    {
        var resource = observation.NativeDenial?.Resource ?? observation.Resource;
        return string.Concat(resource.Select(character => char.IsControl(character) || CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.Format ? "\\u" + ((int)character).ToString("X4", CultureInfo.InvariantCulture) : character.ToString()));
    }
}
