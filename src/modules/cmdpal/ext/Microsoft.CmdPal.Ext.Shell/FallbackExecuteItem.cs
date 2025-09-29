// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CmdPal.Ext.Shell.Pages;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

internal sealed partial class FallbackExecuteItem : FallbackCommandItem, IDisposable
{
    private static readonly char[] _systemDirectoryRoots = ['\\', '/'];

    private readonly Action<string>? _addToHistory;
    private readonly ITelemetryService _telemetryService;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _currentUpdateTask;

    public FallbackExecuteItem(SettingsManager settings, Action<string>? addToHistory, ITelemetryService telemetryService)
        : base(
            new NoOpCommand() { Id = "com.microsoft.run.fallback" },
            ResourceLoaderInstance.GetString("shell_command_display_title"))
    {
        Title = string.Empty;
        Subtitle = ResourceLoaderInstance.GetString("generic_run_command");
        Icon = Icons.RunV2Icon; // Defined in Icons.cs and contains the execute command icon.
        _addToHistory = addToHistory;
        _telemetryService = telemetryService;
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
        Expand(ref searchText);

        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            Command = null;
            Title = string.Empty;
            return;
        }

        ShellListPageHelpers.NormalizeCommandLineAndArgs(searchText, out var exe, out var args);

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
            var exeItem = ShellListPage.CreateExeItem(exe, args, fullExePath, _addToHistory, telemetryService: _telemetryService);
            Title = exeItem.Title;
            Subtitle = exeItem.Subtitle;
            Icon = exeItem.Icon;
            Command = exeItem.Command;
            MoreCommands = exeItem.MoreCommands;
        }
        else if (pathIsDir)
        {
            var pathItem = new PathListItem(exe, query, _addToHistory, _telemetryService);
            Command = pathItem.Command;
            MoreCommands = pathItem.MoreCommands;
            Title = pathItem.Title;
            Subtitle = pathItem.Subtitle;
            Icon = pathItem.Icon;
        }
        else if (System.Uri.TryCreate(searchText, UriKind.Absolute, out var uri))
        {
            Command = new OpenUrlWithHistoryCommand(searchText, _addToHistory, _telemetryService) { Result = CommandResult.Dismiss() };
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
        Expand(ref searchText);

        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            return false;
        }

        ShellHelpers.ParseExecutableAndArgs(searchText, out var exe, out var args);
        var exeExists = ShellListPageHelpers.FileExistInPath(exe, out var fullExePath);
        var pathIsDir = Directory.Exists(exe);

        return exeExists || pathIsDir;
    }

    private static void Expand(ref string searchText)
    {
        if (searchText.Length == 0)
        {
            return;
        }

        var singleCharQuery = searchText.Length == 1;

        searchText = Environment.ExpandEnvironmentVariables(searchText);

        if (!TryExpandHome(ref searchText))
        {
            TryExpandRoot(ref searchText);
        }
    }

    private static bool TryExpandHome(ref string searchText)
    {
        if (searchText[0] == '~')
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (searchText.Length == 1)
            {
                searchText = home;
            }
            else if (_systemDirectoryRoots.Contains(searchText[1]))
            {
                searchText = Path.Combine(home, searchText[2..]);
            }

            return true;
        }

        return false;
    }

    private static bool TryExpandRoot(ref string searchText)
    {
        if (_systemDirectoryRoots.Contains(searchText[0]) && (searchText.Length == 1 || !_systemDirectoryRoots.Contains(searchText[1])))
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (root != null)
            {
                searchText = searchText.Length == 1 ? root : Path.Combine(root, searchText[1..]);
                return true;
            }
        }

        return false;
    }
}
