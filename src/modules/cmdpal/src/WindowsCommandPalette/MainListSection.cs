// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.CmdPal.Extensions;
using Microsoft.UI.Dispatching;
using WindowsCommandPalette.Views;

namespace DeveloperCommandPalette;

// The MainListSection is for all non-recent actions. No apps.
public sealed class MainListSection : ISection, INotifyCollectionChanged
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public string Title => "Actions";

    private readonly MainViewModel _mainViewModel;
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    // Top-level list items, from builtin commands and extensions
    public ObservableCollection<MainListItem> TopLevelItems { get; set; }

    // Things we should enumerate over, depending on the search query.
    // This is either
    // * the last search results (if there was a query),
    // * OR one of:
    //   * Just the top-level actions (if there's no query)
    //   * OR the top-level actions AND the apps (if there's a query)
    private IEnumerable<IListItem> TopLevelItemsToEnumerate => TopLevelItems.Where(i => i != null && (!_mainViewModel.IsRecentCommand(i)));

    // Watch out future me!
    //
    // Don't do the whole linq query in Items itself. That'll evaluate the whole
    // query once per item, because the ListPage.xaml.cs will create one
    // ListViewItems per item in Items, and every time it does that, it calls
    // section.Items.
    //
    // instead run the query once when the action query changes, and store the
    // results.
    public IListItem[] Items => TopLevelItemsToEnumerate.ToArray();

    public MainListSection(MainViewModel viewModel)
    {
        this._mainViewModel = viewModel;
        TopLevelItems = new(_mainViewModel.TopLevelCommands.Select(w => w.Unsafe).Where(li => li != null).Select(li => new MainListItem(li!)));
        TopLevelItems.CollectionChanged += Bubble_CollectionChanged;
    }

    internal void UpdateQuery(string query)
    {
        var fallbacks = TopLevelItems.Select(i => i?.FallbackHandler).Where(fb => fb != null).Select(fb => fb!);
        foreach (var fb in fallbacks)
        {
            fb.UpdateQuery(query);
        }
    }

    private void Bubble_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            CollectionChanged?.Invoke(this, e);
        });
    }

    internal void Reset()
    {
        TopLevelItems.Clear();
    }
}
