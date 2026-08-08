// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

/// <summary>
/// Hands the host a large batch of items and keeps no reference to them, so the
/// only thing that can hold them alive afterwards is the host itself.
/// </summary>
internal sealed partial class BallastListPage : ListPage
{
    private readonly int _payloadBytes;
    private readonly int _iconSide;
    private readonly int _moreCommands;

    public BallastListPage(int count, int payloadBytes, int iconSide = 0, int moreCommands = 0)
    {
        Count = count;
        _payloadBytes = payloadBytes;
        _iconSide = iconSide;
        _moreCommands = moreCommands;

        Icon = new IconInfo(""); // Library
        Name = "Open";
        Title = HasIcons
            ? $"{count:N0} items with data-backed icons"
            : $"{count:N0} ballast items";
    }

    public int Count { get; }

    public bool HasIcons => _iconSide > 0;

    public int MoreCommandsPerItem => _moreCommands;

    /// <summary>
    /// Gets the approximate bytes this page allocates on the extension side.
    /// </summary>
    /// <remarks>
    /// For the icon variant the bytes live in WinRT streams rather than the
    /// managed heap, so they show up in the process working set and not in
    /// <c>GC.GetTotalMemory</c>.
    /// </remarks>
    public long ApproximateBytes => HasIcons
        ? (long)Count * ((_iconSide * _iconSide * 4) + 54)
        : (long)Count * _payloadBytes;

    public override IListItem[] GetItems()
    {
        // Deliberately not cached in a field. Once the host releases its proxies
        // these become unreachable from both processes and TrackedPayload's
        // finalizer fires, which is what the counters on the diagnostics page
        // are measuring.
        var generation = LeakTracker.StartGeneration();

        LeakLog.RecordAction(
            $"GetItems batch {generation}: {Count:N0} items, payload {_payloadBytes / 1024:N0} KB, icon {_iconSide}px, {_moreCommands} MoreCommands");

        var items = new IListItem[Count];

        for (var i = 0; i < Count; i++)
        {
            items[i] = new BallastListItem(generation, i, _payloadBytes, _iconSide, _moreCommands);
        }

        return items;
    }
}
