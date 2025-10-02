// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell.Pages;

internal sealed partial class ShellListPage : DynamicListPage, IDisposable
{
    private readonly Dictionary<string, ListItem> _historyItems = [];
    private readonly List<ListItem> _currentHistoryItems = [];

    private readonly IRunHistoryService _historyService;
    private readonly ITelemetryService? _telemetryService;

    private readonly Dictionary<string, ListItem> _currentPathItems = new();

    private ListItem? _exeItem;
    private List<ListItem> _pathItems = [];
    private ListItem? _uriItem;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _currentSearchTask;

    private bool _loadedInitialHistory;

    private string _currentSubdir = string.Empty;

    public ShellListPage(
        ISettingsInterface settingsManager,
        IRunHistoryService runHistoryService,
        ITelemetryService? telemetryService)
    {
        Icon = Icons.RunV2Icon;
        Id = "com.microsoft.cmdpal.shell";
        Name = ResourceLoaderInstance.GetString("cmd_plugin_name");
        PlaceholderText = ResourceLoaderInstance.GetString("list_placeholder_text");
        _historyService = runHistoryService;
        _telemetryService = telemetryService;

        EmptyContent = new CommandItem()
        {
            Title = ResourceLoaderInstance.GetString("cmd_plugin_name"),
            Icon = Icons.RunV2Icon,
            Subtitle = ResourceLoaderInstance.GetString("list_placeholder_text"),
        };
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (newSearch == oldSearch)
        {
            return;
        }

        DoUpdateSearchText(newSearch);
    }

    private void DoUpdateSearchText(string newSearch)
    {
        // Cancel any ongoing search
        _cancellationTokenSource?.Cancel();

        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        try
        {
            // Save the latest search task
            _currentSearchTask = BuildListItemsForSearchAsync(newSearch, cancellationToken);
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
        _ = ProcessSearchResultsAsync(_currentSearchTask, newSearch);
    }

    private async Task ProcessSearchResultsAsync(Task searchTask, string newSearch)
    {
        try
        {
            await searchTask;

            // Ensure this is still the latest task
            if (_currentSearchTask == searchTask)
            {
                // The search results have already been updated in BuildListItemsForSearchAsync
                IsLoading = false;
                RaiseItemsChanged();
            }
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation gracefully
        }
        catch (Exception)
        {
            // Handle other exceptions
            IsLoading = false;
        }
    }

    private async Task BuildListItemsForSearchAsync(string newSearch, CancellationToken cancellationToken)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();

        // Check for cancellation at the start
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // If the search text is the start of a path to a file (it might be a
        // UNC path), then we want to list all the files that start with that text:

        // 1. Check if the search text is a valid path
        // 2. If it is, then list all the files that start with that text
        var searchText = newSearch.Trim();

        var expanded = Environment.ExpandEnvironmentVariables(searchText);

        // Check for cancellation after environment expansion
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // TODO we can be smarter about only re-reading the filesystem if the
        // new search is just the oldSearch+some chars
        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            _pathItems.Clear();
            _exeItem = null;
            _uriItem = null;

            _currentHistoryItems.Clear();
            _currentHistoryItems.AddRange(_historyItems.Values);

            return;
        }

        // Reset the path resolution flag
        var couldResolvePath = false;

        var exe = string.Empty;
        var args = string.Empty;

        var exeExists = false;
        var fullExePath = string.Empty;
        var pathIsDir = false;

        try
        {
            // Create a timeout for file system operations (200ms)
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var timeoutToken = combinedCts.Token;

            // Use Task.Run with timeout - this will actually timeout even if the sync operations don't respond to cancellation
            var pathResolutionTask = Task.Run(
                () =>
            {
                ShellListPageHelpers.NormalizeCommandLineAndArgs(expanded, out exe, out args);

                // Don't check cancellation token here - let the Task timeout handle it
                exeExists = ShellListPageHelpers.FileExistInPath(exe, out fullExePath);
                pathIsDir = Directory.Exists(expanded);
                couldResolvePath = true;
            },
                CancellationToken.None); // Use None here since we're handling timeout differently

            // Wait for either completion or timeout
            await pathResolutionTask.WaitAsync(timeoutToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Main cancellation token was cancelled, re-throw
            throw;
        }
        catch (TimeoutException)
        {
            // Timeout occurred
            couldResolvePath = false;
        }
        catch (OperationCanceledException)
        {
            // Timeout occurred (from WaitAsync)
            couldResolvePath = false;
        }
        catch (Exception)
        {
            // Handle any other exceptions that might bubble up
            couldResolvePath = false;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        _pathItems.Clear();

        // We want to show path items:
        // * If there's no args, AND (the path doesn't exist OR the path is a dir)
        if (string.IsNullOrEmpty(args)
            && (!exeExists || pathIsDir)
            && couldResolvePath)
        {
            IsLoading = true;
            await CreatePathItemsAsync(expanded, searchText, cancellationToken);
        }

        // Check for cancellation before creating exe items
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (couldResolvePath && exeExists)
        {
            CreateAndAddExeItems(exe, args, fullExePath);
        }
        else
        {
            _exeItem = null;
        }

        // Only create the URI item if we didn't make a file or exe item for it.
        if (!exeExists && !pathIsDir)
        {
            CreateUriItems(searchText);
        }
        else
        {
            _uriItem = null;
        }

        var histItemsNotInSearch =
            _historyItems
                .Where(kv => !kv.Key.Equals(newSearch, StringComparison.OrdinalIgnoreCase));
        if (_exeItem is not null)
        {
            // If we have an exe item, we want to remove it from the history items
            histItemsNotInSearch = histItemsNotInSearch
                .Where(kv => !kv.Value.Title.Equals(_exeItem.Title, StringComparison.OrdinalIgnoreCase));
        }

        if (_uriItem is not null)
        {
            // If we have an uri item, we want to remove it from the history items
            histItemsNotInSearch = histItemsNotInSearch
                .Where(kv => !kv.Value.Title.Equals(_uriItem.Title, StringComparison.OrdinalIgnoreCase));
        }

        // Filter the history items based on the search text
        var filterHistory = (string query, KeyValuePair<string, ListItem> pair) =>
        {
            // Fuzzy search on the key (command string)
            var score = FuzzyStringMatcher.ScoreFuzzy(query, pair.Key);
            return score;
        };

        var filteredHistory =
            ListHelpers.FilterList<KeyValuePair<string, ListItem>>(
                histItemsNotInSearch,
                searchText,
                filterHistory)
            .Select(p => p.Value);

        _currentHistoryItems.Clear();
        _currentHistoryItems.AddRange(filteredHistory);

        // Final cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        timer.Stop();
        _telemetryService?.LogRunQuery(newSearch, GetItems().Length, (ulong)timer.ElapsedMilliseconds);
    }

    private static ListItem PathToListItem(string path, string originalPath, string args = "", Action<string>? addToHistory = null, ITelemetryService? telemetryService = null)
    {
        var pathItem = new PathListItem(path, originalPath, addToHistory, telemetryService);

        if (pathItem.IsDirectory)
        {
            return pathItem;
        }

        // Is this path an executable? If so, then make a RunExeItem
        if (IsExecutable(path))
        {
            var exeItem = new RunExeItem(Path.GetFileName(path), args, path, addToHistory, telemetryService)
            {
                TextToSuggest = path,
            };

            exeItem.MoreCommands = [
            .. exeItem.MoreCommands,
            .. pathItem.MoreCommands];
            return exeItem;
        }

        return pathItem;
    }

    public override IListItem[] GetItems()
    {
        if (!_loadedInitialHistory)
        {
            LoadInitialHistory();
        }

        List<ListItem> uriItems = _uriItem is not null ? [_uriItem] : [];
        List<ListItem> exeItems = _exeItem is not null ? [_exeItem] : [];

        return
            exeItems
            .Concat(_currentHistoryItems)
            .Concat(_pathItems)
            .Concat(uriItems)
            .ToArray();
    }

    internal static ListItem CreateExeItem(string exe, string args, string fullExePath, Action<string>? addToHistory, ITelemetryService? telemetryService)
    {
        // PathToListItem will return a RunExeItem if it can find a executable.
        // It will ALSO add the file search commands to the RunExeItem.
        return PathToListItem(fullExePath, exe, args, addToHistory, telemetryService);
    }

    private void CreateAndAddExeItems(string exe, string args, string fullExePath)
    {
        // If we already have an exe item, and the exe is the same, we can just update it
        if (_exeItem is RunExeItem exeItem && exeItem.FullExePath.Equals(fullExePath, StringComparison.OrdinalIgnoreCase))
        {
            exeItem.UpdateArgs(args);
        }
        else
        {
            _exeItem = CreateExeItem(exe, args, fullExePath, AddToHistory, _telemetryService);
        }
    }

    private static bool IsExecutable(string path)
    {
        // Is this path an executable?
        // check all the extensions in PATHEXT
        var extensions = Environment.GetEnvironmentVariable("PATHEXT")?.Split(';') ?? Array.Empty<string>();
        var extension = Path.GetExtension(path);
        return string.IsNullOrEmpty(extension) || extensions.Any(ext => string.Equals(extension, ext, StringComparison.OrdinalIgnoreCase));
    }

    private async Task CreatePathItemsAsync(string searchPath, string originalPath, CancellationToken cancellationToken)
    {
        var directoryPath = string.Empty;
        var searchPattern = string.Empty;

        var startsWithQuote = searchPath.Length > 0 && searchPath[0] == '"';
        var endsWithQuote = searchPath.Last() == '"';
        var trimmed = (startsWithQuote && endsWithQuote) ? searchPath.Substring(1, searchPath.Length - 2) : searchPath;
        var isDriveRoot = trimmed.Length == 2 && trimmed[1] == ':';

        // we should also handle just drive roots, ala c:\ or d:\
        // we need to handle this case first, because "C:" does exist, but we need to append the "\" in that case
        if (isDriveRoot)
        {
            directoryPath = trimmed + "\\";
            searchPattern = $"*";
        }

        // Easiest case: text is literally already a full directory
        else if (Directory.Exists(trimmed) && trimmed.EndsWith('\\'))
        {
            directoryPath = trimmed;
            searchPattern = $"*";
        }

        // Check if the search text is a valid path
        else if (Path.IsPathRooted(trimmed) && Path.GetDirectoryName(trimmed) is string directoryName)
        {
            directoryPath = directoryName;
            searchPattern = $"{Path.GetFileName(trimmed)}*";
        }

        // Check if the search text is a valid UNC path
        else if (trimmed.StartsWith(@"\\", System.StringComparison.CurrentCultureIgnoreCase) &&
                 trimmed.Contains(@"\\"))
        {
            directoryPath = trimmed;
            searchPattern = $"*";
        }

        // Check for cancellation before directory operations
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var dirExists = Directory.Exists(directoryPath);

        // searchPath is fully expanded, and originalPath is not. We might get:
        // * original: X%Y%Z\partial
        // * search: X_foo_Z\partial
        // and we want the result `X_foo_Z\partialOne` to use the suggestion `X%Y%Z\partialOne`
        //
        // To do this:
        // * Get the directoryPath
        // * trim that out of the beginning of searchPath -> searchPathTrailer
        // * everything left from searchPath? remove searchPathTrailer from the end of originalPath
        // that gets us the expanded original dir

        // Check if the directory exists
        if (dirExists)
        {
            // Check for cancellation before file system enumeration
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (directoryPath == _currentSubdir)
            {
                // Filter the items we already had
                var fuzzyString = searchPattern.TrimEnd('*');
                var newMatchedPathItems = new List<ListItem>();

                foreach (var kv in _currentPathItems)
                {
                    var score = string.IsNullOrEmpty(fuzzyString) ?
                        1 :
                        FuzzyStringMatcher.ScoreFuzzy(fuzzyString, kv.Key);
                    if (score > 0)
                    {
                        newMatchedPathItems.Add(kv.Value);
                    }
                }

                ListHelpers.InPlaceUpdateList(_pathItems, newMatchedPathItems);
                return;
            }

            // Get all the files in the directory that start with the search text
            // Run this on a background thread to avoid blocking
            var files = await Task.Run(() => Directory.GetFileSystemEntries(directoryPath, searchPattern), cancellationToken);

            // Check for cancellation after file enumeration
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var searchPathTrailer = trimmed.Remove(0, Math.Min(directoryPath.Length, trimmed.Length));
            var originalBeginning = originalPath.EndsWith(searchPathTrailer, StringComparison.CurrentCultureIgnoreCase) ?
                                        originalPath.Remove(originalPath.Length - searchPathTrailer.Length) :
                                        originalPath;

            if (isDriveRoot)
            {
                originalBeginning = string.Concat(originalBeginning, '\\');
            }

            // Create a list of commands for each file
            var newPathItems = files
                .Select(f => PathToListItem(f, originalBeginning))
                .ToDictionary(item => item.Title, item => item);

            // Final cancellation check before updating results
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            // Add the commands to the list
            _pathItems = newPathItems.Values.ToList();
            _currentSubdir = directoryPath;
            _currentPathItems.Clear();
            foreach ((var k, IListItem v) in newPathItems)
            {
                _currentPathItems[k] = (ListItem)v;
            }
        }
        else
        {
            _pathItems.Clear();
        }
    }

    internal void CreateUriItems(string searchText)
    {
        if (!System.Uri.TryCreate(searchText, UriKind.Absolute, out var uri))
        {
            _uriItem = null;
            return;
        }

        var command = new OpenUrlCommand(searchText) { Result = CommandResult.Dismiss() };
        _uriItem = new ListItem(command)
        {
            Title = searchText,
        };
    }

    private void LoadInitialHistory()
    {
        var hist = _historyService.GetRunHistory();
        var histItems = hist
            .Select(h => (h, ShellListPageHelpers.ListItemForCommandString(h, AddToHistory, _telemetryService)))
            .Where(tuple => tuple.Item2 is not null)
            .Select(tuple => (tuple.h, tuple.Item2!))
            .ToList();
        _historyItems.Clear();

        // Add all the history items to the _historyItems dictionary
        foreach (var (h, item) in histItems)
        {
            _historyItems[h] = item;
        }

        _currentHistoryItems.Clear();
        _currentHistoryItems.AddRange(histItems.Select(tuple => tuple.Item2));

        _loadedInitialHistory = true;
    }

    internal void AddToHistory(string commandString)
    {
        if (string.IsNullOrWhiteSpace(commandString))
        {
            return; // Do not add empty or whitespace items
        }

        _historyService.AddRunHistoryItem(commandString);
        LoadInitialHistory();
        DoUpdateSearchText(SearchText);
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}
