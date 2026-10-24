// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

internal sealed partial class SampleIncrementalLoadingPage : DynamicListPage
{
    private const int BatchSize = 8;
    private const int TotalItemCount = 512;

    private readonly Lock _stateLock = new();
    private readonly int _initialItemCount;
    private readonly ICommandContextItem[] _itemCommands;
    private IListItem[] _items;
    private int _loadMoreCount;

    public SampleIncrementalLoadingPage(bool useGrid, int initialItemCount = 1)
    {
        _initialItemCount = initialItemCount;
        Id = $"samples.incremental-loading.{(useGrid ? "grid" : "list")}.{initialItemCount}";
        Name = useGrid ? "Small-batch incremental loading (grid)" : "Small-batch incremental loading (list)";
        Icon = new IconInfo("\uE8A5");
        GridProperties = useGrid ? new SmallGridLayout() : null;
        PlaceholderText = $"Type anything to restart with {initialItemCount} item(s)";
        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Title = "Waiting for the first batch",
            Subtitle = "More items are available. The first batch should load without scrolling or keyboard input.",
        };
        _itemCommands = new ICommandContextItem[]
        {
            new CommandContextItem(new AnonymousCommand(ResetItems)
            {
                Name = "Restart incremental loading",
                Result = CommandResult.KeepOpen(),
            }),
            new CommandContextItem(new AnonymousCommand(ToggleLayout)
            {
                Name = "Switch between list and grid",
                Result = CommandResult.KeepOpen(),
            }),
        };
        _items = CreateInitialItems();
        UpdateStatus();
    }

    public override bool HasMoreItems
    {
        get => Volatile.Read(ref _items).Length < TotalItemCount;
        set => base.HasMoreItems = value;
    }

    public override IListItem[] GetItems() => Volatile.Read(ref _items);

    public override void LoadMore()
    {
        lock (_stateLock)
        {
            var items = _items;
            if (items.Length >= TotalItemCount)
            {
                return;
            }

            var nextItems = new IListItem[Math.Min(items.Length + BatchSize, TotalItemCount)];
            Array.Copy(items, nextItems, items.Length);
            for (var index = items.Length; index < nextItems.Length; index++)
            {
                nextItems[index] = CreateItem(index);
            }

            Volatile.Write(ref _items, nextItems);
            _loadMoreCount++;
        }

        UpdateStatus();
        RaiseItemsChanged(GetItems().Length);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => ResetItems();

    private void ResetItems()
    {
        lock (_stateLock)
        {
            Volatile.Write(ref _items, CreateInitialItems());
            _loadMoreCount = 0;
        }

        UpdateStatus();
        RaiseItemsChanged(GetItems().Length);
    }

    private void ToggleLayout() => GridProperties = GridProperties is null ? new SmallGridLayout() : null;

    private void UpdateStatus()
    {
        var itemCount = GetItems().Length;
        Title = $"Incremental loading: {itemCount}/{TotalItemCount} items, {Volatile.Read(ref _loadMoreCount)} batches";
        base.HasMoreItems = itemCount < TotalItemCount;
    }

    private IListItem[] CreateInitialItems()
    {
        var items = new IListItem[_initialItemCount];
        for (var index = 0; index < items.Length; index++)
        {
            items[index] = CreateItem(index);
        }

        return items;
    }

    private ListItem CreateItem(int index) => new(new NoOpCommand { Id = $"{Id}.item.{index}" })
    {
        Title = $"Item {index + 1:D3}",
        Subtitle = $"Up to {BatchSize} items per batch. Resize the window or use Ctrl+K to restart or switch layouts.",
        Icon = Icon,
        MoreCommands = _itemCommands,
    };
}
