// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

/// <summary>
/// Reproduces and measures the "leaving a big list page doesn't give the memory
/// back" problem.
/// <para>
/// Open one of the ballast pages, navigate away, then come back here and force a
/// full collection. If <c>Alive</c> does not drop to zero, the host is still
/// holding proxies to those list items and the extension cannot free them.
/// </para>
/// </summary>
internal sealed partial class MemoryLeakDiagnosticsPage : ListPage
{
    private const int PayloadBytes = 20 * 1024;

    // 64x64 at 32bpp is ~16 KB per icon, in the range a real extension's
    // thumbnails occupy.
    private const int IconSide = 64;

    private const int MoreCommandsPerItem = 3;

    private readonly BallastListPage[] _ballastPages =
    [
        new BallastListPage(1_000, PayloadBytes),
        new BallastListPage(5_000, PayloadBytes),
        new BallastListPage(10_000, PayloadBytes),
        new BallastListPage(1_000, 0, IconSide),
        new BallastListPage(5_000, 0, IconSide),
        new BallastListPage(10_000, 0, IconSide),
        new BallastListPage(1_000, 0, 0, MoreCommandsPerItem),
        new BallastListPage(5_000, 0, 0, MoreCommandsPerItem),
        new BallastListPage(1_000, 0, IconSide, MoreCommandsPerItem),
    ];

    public MemoryLeakDiagnosticsPage()
    {
        Icon = new IconInfo("\uE9D9");
        Name = "Memory Leak Diagnostics";
        Title = "Track list-item retention across navigation";
        ShowDetails = true;
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>(_ballastPages.Length + 3) { BuildStatusItem() };

        items.Add(new ListItem(RefreshingCommand("Collect", () =>
        {
            LeakLog.RecordAction("Force GC + drain finalizers");
            LeakTracker.ForceFullCollection();
        }))
        {
            Title = "Force GC + drain finalizers",
            Subtitle = "Collect, WaitForPendingFinalizers, collect again - then re-read the counters",
        });

        items.Add(new ListItem(new CopyTextCommand(_lastStatusLine) { Name = "Copy" })
        {
            Title = "Copy status line",
            Subtitle = _lastStatusLine,
            Section = "History",
        });

        items.Add(new ListItem(new CopyTextCommand(LeakLog.Dump()) { Name = "Copy" })
        {
            Title = $"Copy full history ({LeakLog.Count:N0} entries)",
            Subtitle = "Every batch, action and measurement in order - paste straight into a bug report",
            Section = "History",
        });

        items.Add(new ListItem(RefreshingCommand("Reset", () =>
        {
            LeakTracker.Reset();
            LeakLog.RecordAction("Counters reset (live counts deliberately kept)");
        }))
        {
            Title = "Reset counters",
            Subtitle = "Start a fresh measurement without restarting the extension",
            Section = "History",
        });

        items.Add(new ListItem(RefreshingCommand("Clear", LeakLog.Clear))
        {
            Title = "Clear history",
            Subtitle = "Empties the log without touching the counters",
            Section = "History",
        });

        foreach (var page in _ballastPages)
        {
            var megabytes = page.ApproximateBytes / (1024 * 1024);
            var extras = new List<string>(2);

            if (page.HasIcons)
            {
                extras.Add($"~{megabytes:N0} MB in WinRT streams (watch working set, not heap)");
            }
            else if (megabytes > 0)
            {
                extras.Add($"~{megabytes:N0} MB on the managed heap");
            }

            if (page.MoreCommandsPerItem > 0)
            {
                extras.Add($"{page.Count * page.MoreCommandsPerItem:N0} context items built eagerly");
            }

            items.Add(new ListItem(page)
            {
                Title = page.MoreCommandsPerItem > 0
                    ? $"Open {page.Count:N0} items with {page.MoreCommandsPerItem} MoreCommands each"
                    : page.HasIcons
                        ? $"Open {page.Count:N0} items WITH data-backed icons"
                        : $"Open {page.Count:N0} items, no icons",
                Subtitle = string.Join(" - ", extras),
                Section = page.MoreCommandsPerItem > 0
                    ? "Commands and MoreCommands"
                    : page.HasIcons ? "Icon stream proxies" : "Plain ballast (control)",
            });
        }

        return items.ToArray();
    }

    private double _peakWorkingSetMb;
    private string _lastStatusLine = "Not measured yet.";

    private ListItem BuildStatusItem()
    {
        var heapMb = GC.GetTotalMemory(forceFullCollection: false) / (1024d * 1024d);
        var workingSetMb = Environment.WorkingSet / (1024d * 1024d);

        // Commit, unlike working set, is not affected by the OS trimming resident
        // pages. If commit stays flat across repeated cycles the allocator is
        // reusing memory it already holds, which is the opposite of a leak.
        double privateMb;
        using (var process = Process.GetCurrentProcess())
        {
            privateMb = process.PrivateMemorySize64 / (1024d * 1024d);
        }

        // Whether this keeps climbing over repeated open/close cycles is the
        // difference between a leak and pages the OS simply hasn't trimmed.
        _peakWorkingSetMb = Math.Max(_peakWorkingSetMb, workingSetMb);

        var streamMbAlive = LeakTracker.Streams.BytesAlive / (1024d * 1024d);
        var stillHeld = LeakTracker.All.Where(static c => c.Alive > 0).ToList();

        var verdict = stillHeld.Count == 0
            ? "Everything has been released. Any leftover working set is untrimmed pages, not retention."
            : "Still held by the host: " + string.Join(", ", stillHeld.Select(static c => $"{c.Alive:N0} {c.Name.ToLowerInvariant()}"));

        var counters = LeakTracker.All
            .Select(static c => $"**{c.Name}:** {c.Alive:N0} alive of {c.Created:N0} created, {c.Released:N0} released");

        var body = string.Join(
            Environment.NewLine + Environment.NewLine,
            [
                .. counters,
                $"**Icon stream bytes still alive:** {streamMbAlive:N1} MB",
                $"**Batches handed out:** {LeakTracker.Generations:N0}",
            $"**Extension managed heap:** {heapMb:N1} MB",
            $"**Extension working set:** {workingSetMb:N1} MB (peak {_peakWorkingSetMb:N1} MB)",
            $"**Extension private bytes (commit):** {privateMb:N1} MB",
            "Icon bytes live in WinRT streams, so they never show up in the managed heap. Streams alive is the number that matters - working set cannot tell retention apart from untrimmed pages.",
            "Counts only fall after the finalizers run, and one drain often leaves stragglers: finalizing a reference is what makes its stream unreachable, which the *next* collection finds. Press *Force GC + drain finalizers* twice before believing a number.",
            "If streams alive returns to zero each cycle but peak working set keeps climbing, the bytes are being freed and simply not returned to the OS - that is not a leak.",
            ]);

        var summary = string.Join("  |  ", LeakTracker.All.Select(static c => $"{c.Name} {c.Alive:N0}/{c.Created:N0}"));

        _lastStatusLine = $"{summary}  |  heap {heapMb:N1} MB  |  WS {workingSetMb:N1} MB  |  commit {privateMb:N1} MB";

        // Recorded on every read, so the history shows how the numbers moved
        // between actions rather than just where they ended up.
        LeakLog.RecordSnapshot(_lastStatusLine);

        return new ListItem(RefreshingCommand("Refresh", static () => { }))
        {
            Title = $"{summary}  |  WS: {workingSetMb:N1} MB",
            Subtitle = $"{verdict} Select to refresh.",
            Details = new Details
            {
                Title = "Extension process",
                Body = body,
            },
        };
    }

    private AnonymousCommand RefreshingCommand(string name, Action action) =>
        new(() =>
        {
            action();
            RaiseItemsChanged();
        })
        {
            Name = name,
            Result = CommandResult.KeepOpen(),
        };
}
