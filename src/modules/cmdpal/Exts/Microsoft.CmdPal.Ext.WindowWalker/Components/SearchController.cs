// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;

namespace Microsoft.CmdPal.Ext.WindowWalker.Components;

/// <summary>
/// Responsible for searching and finding matches for the strings provided.
/// Essentially the UI independent model of the application
/// </summary>
internal sealed class SearchController
{
    /// <summary>
    /// the current search text
    /// </summary>
    private string searchText;

    /// <summary>
    /// Open window search results
    /// </summary>
    private List<SearchResult>? searchMatches;

    /// <summary>
    /// Singleton pattern
    /// </summary>
    private static SearchController? instance;

    /// <summary>
    /// Gets or sets the current search text
    /// </summary>
    internal string SearchText
    {
        get => searchText;

        set =>
            searchText = value.ToLower(CultureInfo.CurrentCulture).Trim();
    }

    /// <summary>
    /// Gets the open window search results
    /// </summary>
    internal List<SearchResult> SearchMatches => new List<SearchResult>(searchMatches ?? []).OrderByDescending(x => x.Score).ToList();

    /// <summary>
    /// Gets singleton Pattern
    /// </summary>
    internal static SearchController Instance
    {
        get
        {
            instance ??= new SearchController();

            return instance;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchController"/> class.
    /// Initializes the search controller object
    /// </summary>
    private SearchController()
    {
        searchText = string.Empty;
    }

    /// <summary>
    /// Event handler for when the search text has been updated
    /// </summary>
    internal void UpdateSearchText(string searchText)
    {
        SearchText = searchText;
        SyncOpenWindowsWithModel();
    }

    /// <summary>
    /// Syncs the open windows with the OpenWindows Model
    /// </summary>
    internal void SyncOpenWindowsWithModel()
    {
        System.Diagnostics.Debug.Print("Syncing WindowSearch result with OpenWindows Model");

        var snapshotOfOpenWindows = OpenWindows.Instance.Windows;

        searchMatches = string.IsNullOrWhiteSpace(SearchText) ? AllOpenWindows(snapshotOfOpenWindows) : FuzzySearchOpenWindows(snapshotOfOpenWindows);
    }

    /// <summary>
    /// Search method that matches the title of windows with the user search text
    /// </summary>
    /// <param name="openWindows">what windows are open</param>
    /// <returns>Returns search results</returns>
    private List<SearchResult> FuzzySearchOpenWindows(List<Window> openWindows)
    {
        List<SearchResult> result = [];
        var searchStrings = new SearchString(searchText, SearchResult.SearchType.Fuzzy);

        foreach (var window in openWindows)
        {
            var titleMatch = FuzzyMatching.FindBestFuzzyMatch(window.Title, searchStrings.SearchText);
            var processMatch = FuzzyMatching.FindBestFuzzyMatch(window.Process.Name ?? string.Empty, searchStrings.SearchText);

            if ((titleMatch.Count != 0 || processMatch.Count != 0) && window.Title.Length != 0)
            {
                result.Add(new SearchResult(window, titleMatch, processMatch, searchStrings.SearchType));
            }
        }

        System.Diagnostics.Debug.Print("Found " + result.Count + " windows that match the search text");

        return result;
    }

    /// <summary>
    /// Search method that matches all the windows with a title
    /// </summary>
    /// <param name="openWindows">what windows are open</param>
    /// <returns>Returns search results</returns>
    private List<SearchResult> AllOpenWindows(List<Window> openWindows)
    {
        List<SearchResult> result = [];

        foreach (var window in openWindows)
        {
            if (window.Title.Length != 0)
            {
                result.Add(new SearchResult(window));
            }
        }

        return SettingsManager.Instance.InMruOrder
            ? result.ToList()
            : result
                .OrderBy(w => w.Result.Title)
                .ToList();
    }

    /// <summary>
    /// Event args for a window list update event
    /// </summary>
    internal sealed class SearchResultUpdateEventArgs : EventArgs
    {
    }
}
