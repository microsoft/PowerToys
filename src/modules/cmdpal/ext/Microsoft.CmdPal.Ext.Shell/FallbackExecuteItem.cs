// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CmdPal.Ext.Shell.Pages;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

internal sealed partial class FallbackExecuteItem : FallbackCommandItem, IDisposable
{
    private readonly Action<string>? _addToHistory;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _currentUpdateTask;

    public FallbackExecuteItem(SettingsManager settings, Action<string>? addToHistory)
        : base(
            new NoOpCommand() { Id = "com.microsoft.run.fallback" },
            Resources.shell_command_display_title)
    {
        Title = string.Empty;
        Subtitle = Properties.Resources.generic_run_command;
        Icon = Icons.RunV2Icon; // Defined in Icons.cs and contains the execute command icon.
        _addToHistory = addToHistory;
    }

    public override void UpdateQuery(string query)
    {
        // Cancel any ongoing query processing
        _cancellationTokenSource?.Cancel();

        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        try
        {
            // Save the latest update task
            _currentUpdateTask = DoUpdateQueryAsync(query, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // DO NOTHING HERE
            return;
        }
        catch (Exception)
        {
            // Handle other exceptions
            return;
        }

        // Await the task to ensure only the latest one gets processed
        _ = ProcessUpdateResultsAsync(_currentUpdateTask);
    }

    private async Task ProcessUpdateResultsAsync(Task updateTask)
    {
        try
        {
            await updateTask;
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation gracefully
        }
        catch (Exception)
        {
            // Handle other exceptions
        }
    }

    private async Task DoUpdateQueryAsync(string query, CancellationToken cancellationToken)
    {
        // Check for cancellation at the start
        cancellationToken.ThrowIfCancellationRequested();

        var searchText = query.Trim();
        var expanded = Environment.ExpandEnvironmentVariables(searchText);
        searchText = expanded;
        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            Command = null;
            Title = string.Empty;
            return;
        }

        ShellHelpers.ParseExecutableAndArgs(searchText, out var exe, out var args);

        // Check for cancellation before file system operations
        cancellationToken.ThrowIfCancellationRequested();

        var exeExists = false;
        var fullExePath = string.Empty;
        var pathIsDir = false;

        try
        {
            // Create a timeout for file system operations (200ms)
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var timeoutToken = combinedCts.Token;

            // Use Task.Run with timeout for file system operations
            var fileSystemTask = Task.Run(
                () =>
                {
                    exeExists = ShellListPageHelpers.FileExistInPath(exe, out fullExePath);
                    pathIsDir = Directory.Exists(exe);
                },
                CancellationToken.None);

            // Wait for either completion or timeout
            await fileSystemTask.WaitAsync(timeoutToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Main cancellation token was cancelled, re-throw
            throw;
        }
        catch (TimeoutException)
        {
            // Timeout occurred - use defaults
            return;
        }
        catch (OperationCanceledException)
        {
            // Timeout occurred (from WaitAsync) - use defaults
            return;
        }
        catch (Exception)
        {
            // Handle any other exceptions that might bubble up
            return;
        }

        // Check for cancellation before updating UI properties
        cancellationToken.ThrowIfCancellationRequested();

        if (exeExists)
        {
            // TODO we need to probably get rid of the settings for this provider entirely
            var exeItem = ShellListPage.CreateExeItem(exe, args, fullExePath, _addToHistory);
            Title = exeItem.Title;
            Subtitle = exeItem.Subtitle;
            Icon = exeItem.Icon;
            Command = exeItem.Command;
            MoreCommands = exeItem.MoreCommands;
        }
        else if (pathIsDir)
        {
            var pathItem = new PathListItem(exe, query, _addToHistory);
            Command = pathItem.Command;
            MoreCommands = pathItem.MoreCommands;
            Title = pathItem.Title;
            Subtitle = pathItem.Subtitle;
            Icon = pathItem.Icon;
        }
        else if (System.Uri.TryCreate(searchText, UriKind.Absolute, out var uri))
        {
            Command = new OpenUrlWithHistoryCommand(searchText, _addToHistory) { Result = CommandResult.Dismiss() };
            Title = searchText;
        }
        else
        {
            Command = null;
            Title = string.Empty;
        }

        // Final cancellation check
        cancellationToken.ThrowIfCancellationRequested();
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }

    internal static bool SuppressFileFallbackIf(string query)
    {
        var searchText = query.Trim();
        var expanded = Environment.ExpandEnvironmentVariables(searchText);
        searchText = expanded;
        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            return false;
        }

        ShellHelpers.ParseExecutableAndArgs(searchText, out var exe, out var args);
        var exeExists = ShellListPageHelpers.FileExistInPath(exe, out var fullExePath);
        var pathIsDir = Directory.Exists(exe);

        return exeExists || pathIsDir;
    }
}
