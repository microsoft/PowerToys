// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Ext.Run;
using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

internal sealed partial class FallbackExecuteItem : FallbackCommandItem, IDisposable
{
    private const string _id = "com.microsoft.cmdpal.builtin.shell.fallback";
    private static readonly char[] _systemDirectoryRoots = ['\\', '/'];

    private readonly IRunHistoryService _historyService;
    private readonly ITelemetryService _telemetryService;

    private readonly Lock _queryLock = new(); // track the latest query to discard old overlapping queries
    private long _latestQuery;
    private CancellationTokenSource? _cancellationTokenSource;

    public FallbackExecuteItem(IRunHistoryService historyService, ITelemetryService telemetryService)
        : base(
            new NoOpCommand() { Id = _id },
            ResourceLoaderInstance.GetString("shell_command_display_title"),
            _id)
    {
        Title = string.Empty;
        Subtitle = ResourceLoaderInstance.GetString("generic_run_command");
        Icon = Icons.RunV2Icon;
        _historyService = historyService;
        _telemetryService = telemetryService;
    }

    public override void UpdateQuery(string query)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        long queryNumber;
        CancellationTokenSource? displaced;

        // Together, so an older query that gets here late can't cancel a newer one
        lock (_queryLock)
        {
            queryNumber = ++_latestQuery;
            displaced = _cancellationTokenSource;
            _cancellationTokenSource = cancellationTokenSource;
        }

        // Cancel any ongoing query processing
        displaced?.Cancel();

        try
        {
            DoUpdateQuery(query, queryNumber, cancellationToken);
        }
        catch (Exception)
        {
            // Handle other exceptions
            return;
        }
    }

    private void DoUpdateQuery(string query, long queryNumber, CancellationToken cancellationToken)
    {
        // Check for cancellation at the start
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var searchText = query.Trim();
        Expand(ref searchText);

        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            lock (_queryLock)
            {
                if (queryNumber == _latestQuery)
                {
                    Command = null;
                    Title = string.Empty;
                }
            }

            return;
        }

        // Use the same parsing logic as RunListPage to resolve the command line
        ParseCommandlineResult? parseResult = null;

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var timeoutToken = combinedCts.Token;

            var pathResolutionTask = Task.Run(
                () => _historyService.ParseCommandline(searchText, string.Empty),
                CancellationToken.None);

            pathResolutionTask.Wait(timeoutToken);
            parseResult = pathResolutionTask.Result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch
        {
            // Timeout or other error - continue with null parseResult
        }

        // Check for cancellation before updating UI properties
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        string filePath;
        string args;

        if (parseResult is ParseCommandlineResult res)
        {
            filePath = res.FilePath;
            args = res.Arguments;
        }
        else
        {
            filePath = searchText;
            args = string.Empty;
        }

        // Create a RunExeItem to resolve the icon from the file path
        var exeItem = new RunExeItem(
            filePath,
            args,
            filePath,
            (s) => _historyService.AddRunHistoryItem(s),
            _telemetryService);

        // Parsing also succeeds for text that names no file, trust its check instead of making another
        var targetFound = parseResult is { Success: true, IsUri: false, TargetFound: true };
        IIconInfo icon = (targetFound ? exeItem.Icon : null) ?? Icons.RunV2Icon;

        lock (_queryLock)
        {
            if (queryNumber != _latestQuery)
            {
                return;
            }

            Title = ResourceLoaderInstance.GetString("Run_command_line_command_title");
            Subtitle = searchText;
            Command = exeItem.Command;

            // Until the new menu is ready, so the old one can't run the old arguments
            MoreCommands = [];

            // Keep the instance when only the arguments change, so the row doesn't reload it
            if (!IsSameIcon(Icon, icon))
            {
                Icon = icon;
            }
        }

        // Building the menu checks for a folder, which can be slow on a network path
        var moreCommands = exeItem.MoreCommands;

        lock (_queryLock)
        {
            if (queryNumber == _latestQuery)
            {
                MoreCommands = moreCommands;
            }
        }
    }

    // Items for the same file carry equal Shell icon requests, this is in-extension, so safe to compare
    private static bool IsSameIcon(IIconInfo? current, IIconInfo next)
    {
        if (ReferenceEquals(current, next))
        {
            return true;
        }

        return current?.Light?.Icon is { Length: > 0 } light
               && light == next.Light?.Icon
               && current.Dark?.Icon == next.Dark?.Icon;
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
