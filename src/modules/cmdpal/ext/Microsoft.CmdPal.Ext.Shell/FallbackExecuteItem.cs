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
    private CancellationTokenSource? _cancellationTokenSource;
    private RunExeItem? _currentExeItem;

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
        // Cancel any ongoing query processing
        _cancellationTokenSource?.Cancel();

        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        try
        {
            DoUpdateQuery(query, cancellationToken);
        }
        catch (Exception)
        {
            // Handle other exceptions
            return;
        }
    }

    private void DoUpdateQuery(string query, CancellationToken cancellationToken)
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
            Command = null;
            Title = string.Empty;
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
                () => { parseResult = _historyService.ParseCommandline(searchText, string.Empty); },
                CancellationToken.None);

            pathResolutionTask.Wait(timeoutToken);
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

        // Unsubscribe from the old item's property changes
        if (_currentExeItem is not null)
        {
            _currentExeItem.PropChanged -= OnExeItemPropChanged;
            _currentExeItem = null;
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
        _currentExeItem = new RunExeItem(
            filePath,
            args,
            filePath,
            (s) => _historyService.AddRunHistoryItem(s),
            _telemetryService);
        _currentExeItem.PropChanged += OnExeItemPropChanged;

        Title = ResourceLoaderInstance.GetString("Run_command_line_command_title");
        Subtitle = searchText;
        Command = _currentExeItem.Command;
        MoreCommands = _currentExeItem.MoreCommands;

        // Trigger the lazy icon load by accessing the Icon property.
        // FileItem loads icons asynchronously and raises PropChanged when done.
        var icon = _currentExeItem.Icon;
        Icon = icon ?? Icons.RunV2Icon;

        // Final cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }

    private void OnExeItemPropChanged(object sender, IPropChangedEventArgs args)
    {
        if (args.PropertyName == nameof(Icon) && _currentExeItem is not null)
        {
            Icon = _currentExeItem.Icon ?? Icons.RunV2Icon;
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        if (_currentExeItem is not null)
        {
            _currentExeItem.PropChanged -= OnExeItemPropChanged;
        }
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
