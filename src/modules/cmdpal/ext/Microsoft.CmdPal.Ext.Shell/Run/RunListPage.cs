// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Ext.Shell;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation.Collections;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Microsoft.CmdPal.Ext.Run;

/// <summary>
/// This page powers the Run Dialog. It provides a list of items based on what
/// the user is typing, including history items and file system items. It's a
/// heavily trimmed version of what's in the ShellListPage, which is what powers
/// the PowerToys shell plugin.
///
/// Because the run dialog doesn't actually need to select an item to run, this
/// page is a bit different than other dynamic list pages. The items it returns
/// _don't do anything_. They just have icons and titles to display in the
/// suggestions list. They also have TextToSuggest set, so that if the user
/// arrow-keys through the list, the text in the input box updates to match.
///
/// The list of results we return is made up of:
///  * The history items that match what the user is typing
///  * The files in the "current directory" that match what the user is typing.
///    E.g. if the user types "C:\Win", we'll show files in C:\ that match "Win"
///  * A notable difference: we don't have an "exe" item for exactly what the
///    user typed.
///
/// When the user actually clicks the Run button, whatever is in the input box
/// is what gets executed, regardless of what item is selected in the list.
///
/// This class uses the AsyncDynamicListPage base class to handle all the
/// cancellation and task management for async searches. We're just building
/// items in BuildListItemsForSearchAsync.
///
/// Integration with the OS-internal APIs happens via the IRunHistoryService,
/// which provides access to the run history, as well as command line parsing
/// functionality.
/// </summary>
public sealed partial class RunListPage : AsyncDynamicListPage
{
    private static readonly Tag HistoryTag = new() { Icon = Icons.HistoryIcon };
    private readonly Dictionary<string, ListItem> _historyItems = [];
    private readonly List<ListItem> _currentHistoryItems = [];

    private readonly IRunHistoryService _historyService;
    private readonly ITelemetryService? _telemetryService;

    private readonly Dictionary<string, RunExeItem> _currentPathItems = new();
    private readonly List<ListItem> _pathItems = [];
    private ListItem? _exeItem;

    private bool _loadedInitialHistory;

    private string _currentSubdir = string.Empty;

    public RunListPage(
        IRunHistoryService runHistoryService,
        ITelemetryService? telemetryService,
        bool suppressUriFallback = false)
    {
        Icon = Icons.RunV2Icon;
        Id = "com.microsoft.cmdpal.run";

        Name = ResourceLoaderInstance.GetString("Run_plugin_name");
        Title = ResourceLoaderInstance.GetString("Run_generic_run_command");
        PlaceholderText = ResourceLoaderInstance.GetString("Run_list_placeholder_text");

        _historyService = runHistoryService;
        _telemetryService = telemetryService;
    }

    protected async override Task BuildListItemsForSearchAsync(string newSearch, CancellationToken cancellationToken)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();

        // Check for cancellation at the start
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var searchText = newSearch.Trim();

        var withLeadingTilde = searchText.StartsWith("~", StringComparison.InvariantCultureIgnoreCase);
        var correctedSearchText = searchText;
        if (withLeadingTilde)
        {
            correctedSearchText = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    $".\\{searchText[1..]}");
            correctedSearchText = Path.GetFullPath(correctedSearchText);
        }

        // Previously, we were expanding environment variables before searching
        // the filesystem. Now that we're using the IACListISF, we don't need to
        // do that ourselves - it will handle it for us.
        var expanded = correctedSearchText;

        // Check for cancellation after environment expansion
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        _exeItem = null;

        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            _pathItems.Clear();
            _currentHistoryItems.Clear();
            _currentHistoryItems.AddRange(_historyItems.Values);
            return;
        }

        var couldResolvePath = false;
        var isFile = false;
        ParseCommandlineResult? parseResult = null;

        // Create a timeout for file system operations (200ms)
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var timeoutToken = combinedCts.Token;

        // Use Task.Run with timeout - this will actually timeout even if the
        // sync operations don't respond to cancellation
        var pathResolutionTask = Task.Run(
            () =>
            {
                parseResult = _historyService.ParseCommandline(expanded, string.Empty);
                if (parseResult is ParseCommandlineResult res && res.Result == 0)
                {
                    isFile = File.Exists(res.FilePath);
                }

                couldResolvePath = true;
            },
            CancellationToken.None); // Use None here since we're handling timeout differently

        int hr = HRESULT.E_FAIL;
        try
        {
            // Wait for either completion or timeout
            await pathResolutionTask.WaitAsync(timeoutToken);
            if (parseResult is ParseCommandlineResult r)
            {
                hr = r.Result;
            }
        }
        catch (TimeoutException)
        {
            // Timeout occurred
            couldResolvePath = false;
            hr = (HRESULT)(-2147024638);  // 0x80070102 = E_WAITTIMEOUT
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred (from WaitAsync)
            couldResolvePath = false;
            hr = (HRESULT)(-2147023673);  // 0x800704C7 = E_CANCELLED
        }
        catch (Exception)
        {
            // Handle any other exceptions that might bubble up
            couldResolvePath = false;
            hr = HRESULT.E_UNEXPECTED;
        }

        var pathResolutionTime = timer.ElapsedMilliseconds;
        _telemetryService?.LogEvent("BuildListItems_PathResolution", new PropertySet()
        {
            { "newSearch", newSearch },
            { "correctedSearchText", correctedSearchText },
            { "expanded", expanded },
            { "withLeadingTilde", withLeadingTilde },
            { "couldResolvePath", couldResolvePath },
            { "isFile", isFile },
            { "durationMs", pathResolutionTime },
            { "result", hr },
        });

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // If we successfully parsed without a timeout,
        // and we did get a result,
        // and that result succeeded...
        if (couldResolvePath &&
            parseResult is ParseCommandlineResult res
            && res.Result == 0)
        {
            // we'll build some results.

            // If it's not a file, and there are no arguments, then we'll try to
            // add the items in this path. CreatePathItemsAsync will find the
            // child items in the last fullty specified path, and add them to
            // the page's _pathItems.
            var filePath = res.FilePath;
            var args = res.Arguments;

            // wacky edge case:
            //
            // if the current subdir has a space in it, but we're filtering that
            // dir, then we still need to update the path items. We'll want to
            // start the args after the first space _after_ the current subdir.
            var resolvedSearchString = filePath + " " + args; // what the OS thinks the filepath & args were
            var currentSubdirLength = _currentSubdir.Length;
            var isFilteringCurrentSubdir = resolvedSearchString.Length > currentSubdirLength;
            var currentSubdirHasSpace = _currentSubdir.Contains(' ');

            // don't apply this logic
            // * if we're backspacing out of a subdir with a space
            // * if our subdir didn't already have a space
            if (isFilteringCurrentSubdir &&
                currentSubdirHasSpace)
            {
                var inputAfterSubdir = resolvedSearchString.Substring(currentSubdirLength);
                var firstSpaceAfterSubdir = inputAfterSubdir.IndexOf(' ');
                if (firstSpaceAfterSubdir != -1)
                {
                    filePath = resolvedSearchString.Substring(0, _currentSubdir.Length + firstSpaceAfterSubdir);
                    args = inputAfterSubdir.Substring(firstSpaceAfterSubdir);
                }
                else
                {
                    filePath = resolvedSearchString;
                    args = string.Empty;
                }
            }

            if (!isFile)
            {
                await CreatePathItemsAsync(filePath, correctedSearchText, withLeadingTilde, cancellationToken);
            }
            else
            {
                // We straight up got a path to a file, or there are arguments
                // (which implies there's a file to run), then _don't_ add a
                // single item for that result.
                //
                // This is a major difference from CmdPal's shell page. PT
                // CmdPal _needs_ a command to be able to run something.
                //
                // The run dialog, however, will run whatever the user typed
                // when the button is pressed. It totally ignores the actual
                // results from the page.
                //
                // If for some reason you wanted a command here, you could
                // uncomment the following lines to create an exe item for it.
                // _exeItem = RunDialogHelpers.CreateListItemForCommandResult(res);
            }
        }
        else
        {
            // If we didn't resolve the path, or we timed out, we'll just create
            // nothing.
            //
            // This is again different than CmdPal's shell page - we don't care
            // if we can't parse the thing. Ultimately, the button will try to
            // run the command even if there's no selected item.
        }

        if (parseResult is ParseCommandlineResult res2
            && _pathItems.Count == 0)
        {
            var item = new RunExeItem(res2.FilePath, res2.Arguments, res2.FilePath, (s) => _historyService.AddRunHistoryItem(s), _telemetryService)
            {
                Title = Path.GetFileName(res2.FilePath),

                // TextToSuggest = res2.FilePath,
            };
            _exeItem = item;
        }

        FilterHistoryItems(newSearch, searchText);
    }

    private async Task CreatePathItemsAsync(
        string fullFilePath,
        string searchText,
        bool withLeadingTilde,
        CancellationToken cancellationToken)
    {
        // The way AutoComplete handles "expanding" paths means passing the
        // entire string up to the last slash as the query to autocomplete on.
        //
        // we'll replicate that here
        var lastSlash = searchText.LastIndexOfAny(['\\', '/']);
        var directoryPath = lastSlash != -1 ? searchText.Substring(0, lastSlash) : searchText;

        // AutoComplete is crazy, and will just append the child items to a full
        // path, without inserting a \. So you'll get things like
        // "C:\windows\system32cmd.exe" for "C:\windows\system32"
        //
        // To mitigate: always make sure we've got a slash there.
        if (!directoryPath.EndsWith(Path.DirectorySeparatorChar))
        {
            directoryPath += Path.DirectorySeparatorChar;
        }

        // Check for cancellation before directory operations
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        _telemetryService?.LogEvent("CreatePathItems_ResolvedPath", new PropertySet()
        {
            { "fullFilePath", fullFilePath },
            { "searchText", searchText },
            { "directoryPath", directoryPath },
        });

        // Check for cancellation before file system enumeration
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // If the directory we're in changed, then first rebuild the cache
        // of all the items in the directory, _then_ filter them below.
        if (!_currentSubdir.Equals(directoryPath, StringComparison.OrdinalIgnoreCase))
        {
            var succeededWithoutBeingCancelled = await ChangeDirectory(directoryPath, withLeadingTilde, cancellationToken);
            if (!succeededWithoutBeingCancelled)
            {
                return;
            }
        }

        var newMatchedPathItems = FilterCurrentDirectoryFiles(
            fullFilePath,
            directoryPath,
            _currentSubdir,
            _currentPathItems.AsReadOnly(),
            _telemetryService);

        ListHelpers.InPlaceUpdateList(_pathItems, newMatchedPathItems);
    }

    internal static List<ListItem> FilterCurrentDirectoryFiles(
        string fullFilePath,
        string directoryPath,
        string currentSubdir,
        IReadOnlyDictionary<string, RunExeItem> currentPathItems,
        ITelemetryService? telemetryService)
    {
        // Filter the items from this directory
        // Add one to the length of directoryPath - if there's no trailing
        // slash, we don't want the search for "c:\windows\s" to search "\s"
        var endsInSlash = directoryPath.EndsWith("\\", StringComparison.InvariantCultureIgnoreCase);
        var expectedSlice = directoryPath.Length + (endsInSlash ? 0 : 1);
        var isAnythingAfterSlash = expectedSlice < fullFilePath.Length;

        // fuzzyString is everything that's after the last slash. We're
        // going to use that as the text to search through the results in
        // the _currentSubdir
        var fuzzyString = isAnythingAfterSlash ? fullFilePath.Substring(expectedSlice) : string.Empty;
        var newMatchedPathItems = new List<ListItem>();
        var searchIsEmpty = string.IsNullOrEmpty(fuzzyString);

        // fast pass: if the search is empty, return everything
        if (searchIsEmpty)
        {
            foreach (var kv in currentPathItems)
            {
                newMatchedPathItems.Add(kv.Value);
            }
        }
        else
        {
            foreach (var kv in currentPathItems)
            {
                if (MatchesFilter(fuzzyString, kv.Key, kv.Value.FullPath))
                {
                    newMatchedPathItems.Add(kv.Value);
                }
            }
        }

        telemetryService?.LogEvent("CreatePathItems_Filtered", new PropertySet()
        {
                { "dir", currentSubdir },
                { "fuzzyString", fuzzyString },
                { "filteredCount", newMatchedPathItems.Count },
        });

        return newMatchedPathItems;
    }

    /// <summary>
    /// Returns true if we should include this result (as specified in
    /// `haystack`) in the results for a given search `needle`. This attempts to
    /// mimick the behavior of CACLIShellFolder, which is what RunDlg uses to
    /// build its suggestions list.
    ///
    /// If the needle is an exact match for the haystack, then we only want to
    /// include this result if the file path is a directory, not a file.
    /// </summary>
    private static bool MatchesFilter(string needle, string haystack, string fullPath)
    {
        if (haystack.Equals(needle, StringComparison.OrdinalIgnoreCase))
        {
            // if it was an exact match, we only want it in the results if this
            // is a directory. That's what RunDlg does. This behavior is
            // somewhere around `CACLIShellFolder::_PassesFilter`
            return Directory.Exists(fullPath);
        }

        return haystack.StartsWith(needle, StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Change the current directory that we are browsing to the specified
    /// directory. This will rebuild the cache of items in _currentPathItems,
    /// and set it to all the ListItems corresponding to the files in that path.
    /// It will also update _currentSubdir to the new path.
    ///
    /// Returns true if the directory change was successful, false if it was cancelled.
    /// <param name="directoryPath">The path of the directory to change to.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// </summary>
    private async Task<bool> ChangeDirectory(string directoryPath, bool withLeadingTilde, CancellationToken cancellationToken)
    {
        _telemetryService?.LogEvent("CreatePathItems_ChangedDirectory", new PropertySet()
        {
                    { "old", _currentSubdir },
                    { "new", directoryPath },
        });

        var newPathItems = await BuildItemsForDirectory(
            directoryPath,
            withLeadingTilde,
            _historyService,
            _telemetryService,
            cancellationToken);

        if (newPathItems is null)
        {
            return false;
        }

        // Final cancellation check before updating results
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        // Add the commands to the list
        _pathItems.Clear();
        _currentSubdir = directoryPath;
        _currentPathItems.Clear();
        foreach ((var k, var v) in newPathItems)
        {
            _currentPathItems[k] = v;
        }

        return true;
    }

    /// <summary>
    /// Builds a dictionary of ListItems for all files in the specified
    /// directory. Returns null if the operation was cancelled.
    ///
    /// To populate the list of items in the directory, we're using the Shell's
    /// ACListISF API, which is what powers the AutoComplete in RunDlg.
    /// </summary>
    /// <param name="directoryPath">The path of the directory to build items for.</param>
    /// <param name="telemetryService">An optional telemetry service for logging events.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A dictionary mapping file names to ListItems, or null if cancelled.</returns>
    internal static async Task<Dictionary<string, RunExeItem>?> BuildItemsForDirectory(
        string directoryPath,
        bool withLeadingTilde,
        IRunHistoryService historyService,
        ITelemetryService? telemetryService,
        CancellationToken cancellationToken)
    {
        // Get all the files in the directory.
        // Run this on the OLE STA thread, because GetSuggestions needs OLE to work.
        var files = await StaHelperService.RunOnStaAsync(() => GetSuggestionsForPath(directoryPath), cancellationToken) ?? [];

        telemetryService?.LogEvent("BuildItemsForDirectory", new PropertySet()
        {
            { "dir", directoryPath },
            { "fileCount", files.Length },
        });

        // Check for cancellation after file enumeration
        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        // Create a list of commands for each file
        var newPathItems = new Dictionary<string, RunExeItem>(files.Length);
        foreach (var f in files)
        {
            var textToSuggest = f;
            if (withLeadingTilde)
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (textToSuggest.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
                {
                    textToSuggest = string.Concat("~", textToSuggest.AsSpan(userProfile.Length));
                }
            }

            var item = new RunExeItem(f, string.Empty, f, null, null)
            {
                Title = Path.GetFileName(f),
                TextToSuggest = textToSuggest,
            };

            newPathItems.Add(item.Title, item); // matches ToDictionary behavior (throws on duplicate keys)
        }

        return newPathItems;
    }

    private void FilterHistoryItems(string newSearch, string searchText)
    {
        var histItemsNotInSearchList = new List<KeyValuePair<string, ListItem>>(_historyItems.Count);
        var itemsToRemove = new List<string>(_pathItems.Count);
        foreach (var item in _pathItems)
        {
            if (item is IFileItem f)
            {
                itemsToRemove.Add(f.FullPath);
            }
        }

        foreach (var kv in _historyItems)
        {
            // Skip if the key exactly matches the new search text
            if (kv.Key.Equals(newSearch, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var historyItem = kv.Value;
            var historyPath = historyItem is IFileItem file ? file.FullPath : historyItem.Title;
            var found = false;
            foreach (var item in itemsToRemove)
            {
                if (historyPath.Equals(item, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                histItemsNotInSearchList.Add(kv);
            }
        }

        // Filter the history items based on the search text. Do this with a
        // full substring match on the start, not a fuzzy match
        var filterStartsWith = (string query, KeyValuePair<string, ListItem> pair) =>
        {
            return pair.Key.StartsWith(query, StringComparison.CurrentCultureIgnoreCase) ? 1 : 0;
        };

        var filteredHistoryPairs = ListHelpers.FilterList<KeyValuePair<string, ListItem>>(
            histItemsNotInSearchList,
            searchText,
            filterStartsWith);

        _currentHistoryItems.Clear();
        foreach (var pair in filteredHistoryPairs)
        {
            _currentHistoryItems.Add(pair.Value);
        }
    }

    public override IListItem[] GetItems()
    {
        if (!_loadedInitialHistory)
        {
            LoadInitialHistory();
        }

        var totalCount =
            (_exeItem is not null ? 1 : 0) +
            _currentHistoryItems.Count +
            _pathItems.Count
            ;

        var combined = new IListItem[totalCount];
        var index = 0;

        if (_exeItem is not null)
        {
            combined[index++] = _exeItem;
        }

        for (var i = 0; i < _currentHistoryItems.Count; i++)
        {
            combined[index++] = _currentHistoryItems[i];
        }

        for (var i = 0; i < _pathItems.Count; i++)
        {
            combined[index++] = _pathItems[i];
        }

        return combined;
    }

    private void LoadInitialHistory()
    {
        var timer = new Stopwatch();
        timer.Start();

        var hist = _historyService.GetRunHistory();
        var histItems = new List<(string, ListItem)>(hist.Count);
        for (var i = 0; i < hist.Count; i++)
        {
            var h = hist[i];

            var item = RunDialogHelpers.CreateListItemForCommandString(h, _historyService, _telemetryService);

            if (item is not null)
            {
                item.Title = h;
                item.Tags = [HistoryTag];

                histItems.Add((h, item));
            }
        }

        _historyItems.Clear();

        // Add all the history items to the _historyItems dictionary
        foreach (var (h, item) in histItems)
        {
            _historyItems[h] = item;
        }

        _currentHistoryItems.Clear();
        for (var i = 0; i < histItems.Count; i++)
        {
            _currentHistoryItems.Add(histItems[i].Item2);
        }

        timer.Stop();
        var itemsToLoad = hist.Count;
        var itemsLoaded = histItems.Count;

        _telemetryService?.LogEvent("LoadHistory", new PropertySet()
        {
            { "itemsToLoad", itemsToLoad },
            { "itemsLoaded", itemsLoaded },
            { "durationMs", timer.ElapsedMilliseconds },
        });

        _loadedInitialHistory = true;
    }

    public void AddToHistory(string commandString)
    {
        if (string.IsNullOrWhiteSpace(commandString))
        {
            return; // Do not add empty or whitespace items
        }

        _historyService.AddRunHistoryItem(commandString);
        LoadInitialHistory();
        DoUpdateSearchText(SearchText);
    }

    /// <summary>
    /// Gets file system suggestions for a path using the Shell's ACListISF API.
    /// This method is called on the persistent STA thread where OLE is already initialized.
    /// </summary>
    internal static string[] GetSuggestionsForPath(string query)
    {
        // OLE is already initialized on the STA thread, so we can directly
        // call the shell APIs here.
        const uint ACLO_FILESYSONLY = 16;
        PInvoke.CoCreateInstance(
            PInvoke.CLSID_ACListISF,
            null,
            CLSCTX.CLSCTX_INPROC_SERVER,
            IUnknown.IID_Guid,
            out var iUnk);
        if (iUnk is not IACList2 list ||
            iUnk is not IEnumString enumString)
        {
            return [];
        }

        list.SetOptions(ACLO_FILESYSONLY);
        try
        {
            list.Expand(query);
        }
        catch (FileNotFoundException)
        {
            // Totally fine if we can't find the file they typed.
            // Just return an empty array
            return [];
        }

        // catch (Exception ex) when (ex.IsInterop())
        catch (Exception)
        {
            return [];
        }

        List<string> results = new();
        PWSTR[] enumResult = [null];
        var hr = enumString.Next(enumResult);
        while (hr == HRESULT.S_OK)
        {
            var pwstr = enumResult[0];
            results.Add(new(pwstr.AsSpan()));
            unsafe
            {
                Marshal.FreeCoTaskMem((IntPtr)(char*)pwstr);
            }

            hr = enumString.Next(enumResult);
        }

        return results.ToArray();
    }
}
