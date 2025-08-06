// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell.Pages;

internal sealed partial class ShellListPage : DynamicListPage, IDisposable
{
    private readonly ShellListPageHelpers _helper;

    private readonly List<ListItem> _topLevelItems = [];
    private readonly Dictionary<string, ListItem> _historyItems = [];
    private readonly List<ListItem> _currentHistoryItems = [];

    private readonly IRunHistoryService _historyService;

    private ListItem? _exeItem;
    private List<ListItem> _pathItems = [];
    private ListItem? _uriItem;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _currentSearchTask;

    private bool _loadedInitialHistory;

    public ShellListPage(SettingsManager settingsManager, IRunHistoryService runHistoryService, bool addBuiltins = false)
    {
        Icon = Icons.RunV2Icon;
        Id = "com.microsoft.cmdpal.shell";
        Name = Resources.cmd_plugin_name;
        PlaceholderText = Resources.list_placeholder_text;
        _helper = new(settingsManager);
        _historyService = runHistoryService;

        EmptyContent = new CommandItem()
        {
            Title = Resources.cmd_plugin_name,
            Icon = Icons.RunV2Icon,
            Subtitle = Resources.list_placeholder_text,
        };

        if (addBuiltins)
        {
            // here, we _could_ add built-in providers if we wanted. links to apps, calc, etc.
            // That would be a truly run-first experience
        }
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
        // Check for cancellation at the start
        cancellationToken.ThrowIfCancellationRequested();

        // If the search text is the start of a path to a file (it might be a
        // UNC path), then we want to list all the files that start with that text:

        // 1. Check if the search text is a valid path
        // 2. If it is, then list all the files that start with that text
        var searchText = newSearch.Trim();

        var expanded = Environment.ExpandEnvironmentVariables(searchText);

        // Check for cancellation after environment expansion
        cancellationToken.ThrowIfCancellationRequested();

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

        ShellHelpers.ParseExecutableAndArgs(expanded, out var exe, out var args);

        // Check for cancellation before file system operations
        cancellationToken.ThrowIfCancellationRequested();

        // Reset the path resolution flag
        var couldResolvePath = false;

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

        cancellationToken.ThrowIfCancellationRequested();

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
        cancellationToken.ThrowIfCancellationRequested();

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
            var score = StringMatcher.FuzzySearch(query, pair.Key).Score;
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
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static ListItem PathToListItem(string path, string originalPath, string args = "", Action<string>? addToHistory = null)
    {
        var pathItem = new PathListItem(path, originalPath, addToHistory);

        // Is this path an executable? If so, then make a RunExeItem
        if (IsExecutable(path))
        {
            var exeItem = new RunExeItem(Path.GetFileName(path), args, path, addToHistory);

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

        var filteredTopLevel = ListHelpers.FilterList(_topLevelItems, SearchText);
        List<ListItem> uriItems = _uriItem is not null ? [_uriItem] : [];
        List<ListItem> exeItems = _exeItem is not null ? [_exeItem] : [];

        return
            exeItems
            .Concat(filteredTopLevel)
            .Concat(_currentHistoryItems)
            .Concat(_pathItems)
            .Concat(uriItems)
            .ToArray();
    }

    internal static ListItem CreateExeItem(string exe, string args, string fullExePath, Action<string>? addToHistory)
    {
        // PathToListItem will return a RunExeItem if it can find a executable.
        // It will ALSO add the file search commands to the RunExeItem.
        return PathToListItem(fullExePath, exe, args, addToHistory);
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
            _exeItem = CreateExeItem(exe, args, fullExePath, AddToHistory);
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
        cancellationToken.ThrowIfCancellationRequested();

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
            cancellationToken.ThrowIfCancellationRequested();

            // Get all the files in the directory that start with the search text
            // Run this on a background thread to avoid blocking
            var files = await Task.Run(() => Directory.GetFileSystemEntries(directoryPath, searchPattern), cancellationToken);

            // Check for cancellation after file enumeration
            cancellationToken.ThrowIfCancellationRequested();

            var searchPathTrailer = trimmed.Remove(0, Math.Min(directoryPath.Length, trimmed.Length));
            var originalBeginning = originalPath.Remove(originalPath.Length - searchPathTrailer.Length);
            if (isDriveRoot)
            {
                originalBeginning = string.Concat(originalBeginning, '\\');
            }

            // Create a list of commands for each file
            var commands = files.Select(f => PathToListItem(f, originalBeginning)).ToList();

            // Final cancellation check before updating results
            cancellationToken.ThrowIfCancellationRequested();

            // Add the commands to the list
            _pathItems = commands;
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
            .Select(h => (h, ShellListPageHelpers.ListItemForCommandString(h, AddToHistory)))
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
