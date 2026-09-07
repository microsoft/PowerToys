// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Projects the filtered, flat list into native grid groups without changing its
/// ordering or owning its items. All access is on the presentation's UI thread.
/// Invalidations are coalesced; the owner synchronizes after a complete list update.
/// </summary>
public sealed partial class GridItemsViewModel : IDisposable
{
    private readonly Dictionary<ListItemViewModel, HeaderState> _observedItems = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ListItemViewModel, int> _itemIndices = new(ReferenceEqualityComparer.Instance);
    private ObservableCollection<ListItemViewModel>? _source;
    private long _sourceVersion;
    private long _synchronizedVersion = -1;
    private bool _isSynchronizing;

    public ObservableCollection<GridItemGroupViewModel> Groups { get; } = [];

    public event EventHandler? Invalidated;

    public bool HasPendingChanges => _sourceVersion != _synchronizedVersion;

    public long Version { get; private set; }

    public int ItemCount { get; private set; }

    /// <summary>
    /// Detaching the source also detaches item notifications. The last projection
    /// stays intact until the next Synchronize, so a detached instance can still
    /// answer queries about what it published. It cannot tell whether anything is
    /// still observing the groups it would go on to update, so an owner whose
    /// presentation is going away releases the whole instance instead.
    /// </summary>
    public void SetSource(ObservableCollection<ListItemViewModel>? source)
    {
        if (ReferenceEquals(_source, source))
        {
            return;
        }

        if (_source is not null)
        {
            _source.CollectionChanged -= Source_CollectionChanged;
        }

        foreach (var item in _observedItems.Keys)
        {
            item.PropertyChanged -= Item_PropertyChanged;
        }

        _observedItems.Clear();
        _source = source;

        if (_source is not null)
        {
            _source.CollectionChanged += Source_CollectionChanged;
        }

        Invalidate();
    }

    public bool Synchronize()
    {
        if (_isSynchronizing || !HasPendingChanges)
        {
            return false;
        }

        _isSynchronizing = true;
        var version = _sourceVersion;
        try
        {
            // Collection notifications can pump XAML layout. Work from a snapshot
            // and leave any reentrant source change pending for the next pass.
            var items = _source?.ToArray() ?? [];
            SynchronizeSubscriptions(items);

            var reusableGroups = new Dictionary<GroupKey, GridItemGroupViewModel>(Groups.Count);
            foreach (var group in Groups)
            {
                reusableGroups.Add(new(group.Header, group.HeaderOccurrence), group);
            }

            var occurrences = new Dictionary<ListItemViewModel, int>(ReferenceEqualityComparer.Instance);
            List<GridItemGroupViewModel> groups = [];
            List<ListItemViewModel> tiles = [];
            GridItemGroupViewModel? current = null;
            var itemCount = 0;
            _itemIndices.Clear();

            GridItemGroupViewModel GetGroup(ListItemViewModel? header, int occurrence)
                => reusableGroups.GetValueOrDefault(new(header, occurrence)) ?? new GridItemGroupViewModel(header, occurrence);

            void CommitGroup()
            {
                if (current is null)
                {
                    return;
                }

                current.FirstItemIndex = itemCount;
                current.RefreshHeader();
                ListHelpers.InPlaceUpdateList(current.Items, tiles, out _);

                // ListItemViewModel equality compares extension models. The
                // projection must still publish the source's current wrappers.
                for (var i = 0; i < tiles.Count; i++)
                {
                    if (!ReferenceEquals(current.Items[i], tiles[i]))
                    {
                        current.Items[i] = tiles[i];
                    }
                }

                groups.Add(current);
                itemCount += tiles.Count;
                tiles.Clear();
            }

            foreach (var item in items)
            {
                if (!item.IsInteractive)
                {
                    CommitGroup();
                    var occurrence = occurrences.GetValueOrDefault(item);
                    occurrences[item] = occurrence + 1;
                    current = GetGroup(item, occurrence);
                }
                else
                {
                    current ??= GetGroup(null, 0);
                    _itemIndices.TryAdd(item, itemCount + tiles.Count);
                    tiles.Add(item);
                }
            }

            // Empty groups preserve consecutive and trailing structural rows.
            CommitGroup();
            ListHelpers.InPlaceUpdateList(Groups, groups, out _);
            ItemCount = itemCount;
            _synchronizedVersion = version;
            Version++;
        }
        finally
        {
            _isSynchronizing = false;
        }

        if (HasPendingChanges)
        {
            Invalidated?.Invoke(this, EventArgs.Empty);
        }

        return true;
    }

    public int IndexOf(ListItemViewModel item) => _itemIndices.GetValueOrDefault(item, -1);

    public GridItemGroupViewModel? GroupFromItemIndex(int index)
    {
        if (index < 0 || index >= ItemCount)
        {
            return null;
        }

        var low = 0;
        var high = Groups.Count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (Groups[middle].FirstItemIndex <= index)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return Groups[low - 1];
    }

    public void Dispose() => SetSource(null);

    private void SynchronizeSubscriptions(ListItemViewModel[] items)
    {
        var currentItems = new HashSet<ListItemViewModel>(items, ReferenceEqualityComparer.Instance);
        foreach (var item in _observedItems.Keys.ToArray())
        {
            if (!currentItems.Contains(item))
            {
                item.PropertyChanged -= Item_PropertyChanged;
                _observedItems.Remove(item);
            }
        }

        foreach (var item in currentItems)
        {
            var state = HeaderState.FromItem(item);
            if (_observedItems.TryAdd(item, state))
            {
                item.PropertyChanged += Item_PropertyChanged;
            }
            else
            {
                _observedItems[item] = state;
            }
        }
    }

    private void Source_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Invalidate();

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ListItemViewModel item ||
            (e.PropertyName is not (null or "" or nameof(ListItemViewModel.Type) or nameof(ListItemViewModel.Section))))
        {
            return;
        }

        var state = HeaderState.FromItem(item);
        if (_observedItems.TryGetValue(item, out var previous) && state != previous)
        {
            _observedItems[item] = state;
            Invalidate();
        }
    }

    private void Invalidate()
    {
        var wasPending = HasPendingChanges;
        _sourceVersion++;
        if (!wasPending)
        {
            Invalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private readonly record struct GroupKey(ListItemViewModel? Header, int Occurrence)
    {
        public bool Equals(GroupKey other) => ReferenceEquals(Header, other.Header) && Occurrence == other.Occurrence;

        public override int GetHashCode() => HashCode.Combine(Header is null ? 0 : RuntimeHelpers.GetHashCode(Header), Occurrence);
    }

    private readonly record struct HeaderState(ListItemType Type, string Title)
    {
        public static HeaderState FromItem(ListItemViewModel item)
            => new(item.Type, item.IsInteractive ? string.Empty : item.Section);
    }
}
