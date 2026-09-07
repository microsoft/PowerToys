// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.MainPage;

// Tracks the searchable command catalog, including items that did not match the previous query.
// Shares MainListPage's collection lock so snapshots and publication use the same generation.
internal sealed partial class MainListPageCommandCatalog : IDisposable
{
    private readonly ObservableCollection<TopLevelViewModel> _commands;
    private readonly Action _onChanged;
    private readonly HashSet<TopLevelViewModel> _subscriptions = new(ReferenceEqualityComparer.Instance);
    private IListItem[] _items = [];
    private long _generation;
    private bool _disposed;

    internal MainListPageCommandCatalog(ObservableCollection<TopLevelViewModel> commands, Action onChanged)
    {
        _commands = commands;
        _onChanged = onChanged;
        lock (_commands)
        {
            UpdateSubscriptions();
            _commands.CollectionChanged += CommandsChanged;
        }
    }

    internal bool IsCurrent(long generation)
    {
        lock (_commands)
        {
            return !_disposed && generation == _generation;
        }
    }

    internal IReadOnlyList<IListItem> Snapshot(IEnumerable<IListItem>? previousMatches, long previousGeneration, out long generation)
    {
        lock (_commands)
        {
            generation = _generation;
            return previousMatches is not null && IsCurrent(previousGeneration)
                ? previousMatches.ToArray()
                : _items;
        }
    }

    internal void Invalidate()
    {
        lock (_commands)
        {
            if (_disposed)
            {
                return;
            }

            _generation++;
            _onChanged();
        }
    }

    private void CommandsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        lock (_commands)
        {
            if (_disposed)
            {
                return;
            }

            UpdateSubscriptions();
            Invalidate();
        }
    }

    private void UpdateSubscriptions()
    {
        // Fallback titles change for each query and already have their own refresh path. Watching
        // them here would recursively start another fallback batch on every such change.
        var current = _commands.Where(command => !command.IsFallback).ToArray();
        var retained = new HashSet<TopLevelViewModel>(current, ReferenceEqualityComparer.Instance);
        foreach (var removed in _subscriptions.Where(item => !retained.Contains(item)).ToArray())
        {
            removed.PropChanged -= ItemChanged;
            _subscriptions.Remove(removed);
        }

        foreach (var item in current)
        {
            if (_subscriptions.Add(item))
            {
                item.PropChanged += ItemChanged;
            }
        }

        _items = current;
    }

    private void ItemChanged(object sender, IPropChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(ICommandItem.Title) or nameof(ICommandItem.Subtitle)
            or nameof(ICommand.Name) or nameof(ICommandItem.Command)))
        {
            return;
        }

        lock (_commands)
        {
            // An event already in flight can arrive after its item was removed or replaced.
            if (sender is TopLevelViewModel item && _subscriptions.Contains(item))
            {
                Invalidate();
            }
        }
    }

    public void Dispose()
    {
        lock (_commands)
        {
            _disposed = true;
            _commands.CollectionChanged -= CommandsChanged;
            foreach (var item in _subscriptions)
            {
                item.PropChanged -= ItemChanged;
            }

            _subscriptions.Clear();
            _items = [];
        }
    }
}
