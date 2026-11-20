// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// The rules Settings applies to CursorBrake's <c>edges</c> setting. The lookup matches the
    /// module's, so the editor shows what the module does: a one-way entry for the direction, then
    /// a both-ways entry for the pair, then Default. An entry without two distinct IDs or with an
    /// unknown mode counts for nothing in a lookup and is never rewritten by normalizing.
    /// </summary>
    public static class CursorBrakeEdgeModel
    {
        /// <summary>
        /// How many days the module keeps a pair of monitors it has not seen together, after which
        /// Settings drops the pair's entries. The module's SEEN_RETENTION_SECONDS in CursorBrakeEdges.h
        /// holds the same span.
        /// </summary>
        public const int SeenRetentionDays = 180;

        /// <summary>How many elements of the <c>edges</c> list the module reads, as MAX_EDGE_ENTRIES in EdgeSettings.h.</summary>
        public const int MaxEntries = 256;

        /// <summary>The longest monitor ID the module reads, as MAX_MONITOR_ID_LENGTH in EdgeSettings.h.</summary>
        public const int MaxIdLength = 512;

        /// <summary>The direction from <paramref name="from"/> to <paramref name="to"/>, as the module reads it.</summary>
        public static CursorBrakeEdgeValue Lookup(IEnumerable<CursorBrakeEdgeSetting> edges, string from, string to)
        {
            var valid = edges.Where(IsValid).ToList();
            var entry = valid.FirstOrDefault(e => e.OneWay && IsSameId(e.Monitors[0], from) && IsSameId(e.Monitors[1], to))
                ?? valid.FirstOrDefault(e => !e.OneWay && IsPair(e, from, to));
            return entry == null ? new CursorBrakeEdgeValue(CursorBrakeEdgeMode.Default, CursorBrakeProperties.DefaultPushDistance) : new CursorBrakeEdgeValue(entry.Mode, entry.PushDistance);
        }

        /// <summary>
        /// A copy of <paramref name="edges"/> with every entry for the pair replaced by the fewest
        /// entries that give each direction its value: one both-ways entry when the two are equal,
        /// else one one-way entry per direction that is not Default. The new entries take the place
        /// of the pair's first entry. A both-ways entry names its monitors in OrdinalIgnoreCase
        /// order, so the file reads the same however the pair was edited.
        /// </summary>
        public static List<CursorBrakeEdgeSetting> SetPair(IEnumerable<CursorBrakeEdgeSetting> edges, string first, string second, CursorBrakeEdgeValue firstToSecond, CursorBrakeEdgeValue secondToFirst)
        {
            var list = edges.ToList();
            int index = list.FindIndex(e => IsPair(e, first, second));
            list.RemoveAll(e => IsPair(e, first, second));
            list.InsertRange(index < 0 ? list.Count : index, EntriesFor(first, second, firstToSecond, secondToFirst));
            return list;
        }

        /// <summary>
        /// A copy of <paramref name="edges"/> with each pair's valid entries rewritten as
        /// <see cref="SetPair"/> writes them, from the values the lookup gives. Overlapping and
        /// repeated entries reduce to the one the module reads.
        /// </summary>
        public static List<CursorBrakeEdgeSetting> Normalize(IEnumerable<CursorBrakeEdgeSetting> edges)
        {
            var source = edges.ToList();
            var result = new List<CursorBrakeEdgeSetting>();
            var done = new List<(string First, string Second)>();
            foreach (var entry in source)
            {
                if (!IsValid(entry))
                {
                    result.Add(entry);
                    continue;
                }

                string first = entry.Monitors[0];
                string second = entry.Monitors[1];
                if (done.Any(p => IsSamePair(p.First, p.Second, first, second)))
                {
                    continue;
                }

                done.Add((first, second));
                result.AddRange(EntriesFor(first, second, Lookup(source, first, second), Lookup(source, second, first)));
            }

            return result;
        }

        /// <summary>
        /// A copy of <paramref name="edges"/> without the entries, both-ways and one-way, whose pair
        /// is not in <paramref name="seen"/>. The module keeps a pair in <c>seen</c> until it has gone
        /// 180 days without seeing the two monitors together. An entry without two IDs stays.
        /// </summary>
        public static List<CursorBrakeEdgeSetting> Prune(IEnumerable<CursorBrakeEdgeSetting> edges, IEnumerable<CursorBrakeSeenPair> seen, out bool changed)
        {
            var pairs = (seen ?? Enumerable.Empty<CursorBrakeSeenPair>()).Where(s => s?.Monitors is { Count: 2 }).ToList();
            var source = edges.ToList();
            var kept = source.Where(e => e?.Monitors is not { Count: 2 } || pairs.Any(s => IsSamePair(s.Monitors[0], s.Monitors[1], e.Monitors[0], e.Monitors[1]))).ToList();
            changed = kept.Count != source.Count;
            return kept;
        }

        /// <summary>
        /// The indices of the displays that can be entered without the cross key but left only with
        /// it: every way out, over a shared edge or through a corner, is a wall, and at least one way
        /// in is not. A move through a corner meets the stricter of the two sides it passes, as in the
        /// module's TrappedMonitors, so a corner is a way out unless a wall borders it.
        /// </summary>
        public static IEnumerable<int> TrappedMonitors(CursorBrakeDetectedEdges detected, IEnumerable<CursorBrakeEdgeSetting> edges)
        {
            var list = edges.ToList();
            for (int monitor = 0; monitor < detected.Monitors.Count; monitor++)
            {
                var exits = Exits(detected, list, monitor).ToList();
                if (exits.Count > 0 && exits.TrueForAll(exit => !exit.Open) && WaysIn(detected, list, monitor).Any())
                {
                    yield return monitor;
                }
            }
        }

        /// <summary>
        /// The number the editor shows for each monitor, by index: Windows' display number, as the
        /// module publishes it. A monitor the file gives no number numbers after the highest
        /// published one, in list order, so no number repeats.
        /// </summary>
        public static IEnumerable<int> Numbers(CursorBrakeDetectedEdges detected)
        {
            int next = detected.Monitors.Count == 0 ? 0 : detected.Monitors.Max(m => m.Number);
            var numbers = new List<int>(detected.Monitors.Count);
            foreach (var monitor in detected.Monitors)
            {
                numbers.Add(monitor.Number > 0 ? monitor.Number : ++next);
            }

            return numbers;
        }

        /// <summary>The value of the direction from one detected monitor to another, Default when either has no ID.</summary>
        public static CursorBrakeEdgeValue DirectionValue(CursorBrakeDetectedEdges detected, IEnumerable<CursorBrakeEdgeSetting> edges, int from, int to)
        {
            string fromId = detected.Monitors[from].Id;
            string toId = detected.Monitors[to].Id;
            if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId))
            {
                return new CursorBrakeEdgeValue(CursorBrakeEdgeMode.Default, CursorBrakeProperties.DefaultPushDistance);
            }

            return Lookup(edges, fromId, toId);
        }

        /// <summary>
        /// The entries the module reads, as Settings loads them: the first <see cref="MaxEntries"/>
        /// elements, malformed ones among them, without the elements that are null, name other than
        /// two monitors, or carry an ID that is null or longer than <see cref="MaxIdLength"/>, with
        /// each distance clamped into the module's range. Entries the module would skip for another
        /// reason, such as an unknown mode, stay as they are and count for nothing in a lookup.
        /// </summary>
        public static List<CursorBrakeEdgeSetting> Sanitize(IEnumerable<CursorBrakeEdgeSetting> edges)
        {
            return (edges ?? Enumerable.Empty<CursorBrakeEdgeSetting>())
                .Take(MaxEntries)
                .Where(e => e?.Monitors is { Count: 2 } ids && ids.TrueForAll(id => id != null && id.Length <= MaxIdLength))
                .Select(e => new CursorBrakeEdgeSetting
                {
                    Monitors = new List<string>(e.Monitors),
                    OneWay = e.OneWay,
                    Mode = e.Mode,
                    PushDistance = Math.Clamp(e.PushDistance, CursorBrakeProperties.MinPushDistance, CursorBrakeProperties.MaxPushDistance),
                })
                .ToList();
        }

        /// <summary>
        /// A copy of what the module published with every null list read as empty, every null
        /// monitor as a monitor without an ID or rect, so the edges' indices still hold, and every
        /// null or malformed edge and seen pair left out.
        /// </summary>
        public static CursorBrakeDetectedEdges Clean(CursorBrakeDetectedEdges detected)
        {
            ArgumentNullException.ThrowIfNull(detected);
            var clean = new CursorBrakeDetectedEdges { Version = detected.Version, Identified = detected.Identified };
            foreach (var monitor in detected.Monitors ?? new List<CursorBrakeDetectedMonitor>())
            {
                clean.Monitors.Add(monitor == null
                    ? new CursorBrakeDetectedMonitor()
                    : new CursorBrakeDetectedMonitor
                    {
                        Id = monitor.Id ?? string.Empty,
                        Name = monitor.Name ?? string.Empty,
                        Rect = monitor.Rect ?? new List<int>(),
                        Dpi = monitor.Dpi,
                        Primary = monitor.Primary,
                        Number = monitor.Number,
                        BuiltIn = monitor.BuiltIn,
                    });
            }

            foreach (var edge in detected.Edges ?? new List<CursorBrakeDetectedEdge>())
            {
                if (edge?.Monitors is { Count: 2 })
                {
                    clean.Edges.Add(new CursorBrakeDetectedEdge { Monitors = edge.Monitors, Axis = edge.Axis ?? string.Empty, At = edge.At, From = edge.From, To = edge.To });
                }
            }

            foreach (var seen in detected.Seen ?? new List<CursorBrakeSeenPair>())
            {
                if (seen?.Monitors is { Count: 2 } ids && ids.TrueForAll(id => id != null))
                {
                    clean.Seen.Add(new CursorBrakeSeenPair { Monitors = ids, LastSeenUtc = seen.LastSeenUtc ?? string.Empty });
                }
            }

            return clean;
        }

        /// <summary>
        /// When the module drops a pair last seen together at <paramref name="lastSeenUtc"/>, or null
        /// when that lies past the end of the calendar.
        /// </summary>
        public static DateTime? RemovedAfter(DateTime lastSeenUtc)
        {
            return lastSeenUtc <= DateTime.MaxValue.AddDays(-SeenRetentionDays) ? lastSeenUtc.AddDays(SeenRetentionDays) : null;
        }

        /// <summary>
        /// The displays the cursor enters <paramref name="monitor"/> from without the cross key,
        /// over a shared edge or through a corner, by index.
        /// </summary>
        public static IEnumerable<int> WaysIn(CursorBrakeDetectedEdges detected, IEnumerable<CursorBrakeEdgeSetting> edges, int monitor)
        {
            var list = edges.ToList();
            return Enumerable.Range(0, detected.Monitors.Count)
                .Where(other => other != monitor && Exits(detected, list, other).Any(exit => exit.Other == monitor && exit.Open))
                .ToList();
        }

        /// <summary>Whether two pairs of IDs name the same two monitors, in either order.</summary>
        public static bool IsSamePair(string a1, string a2, string b1, string b2)
        {
            return (IsSameId(a1, b1) && IsSameId(a2, b2)) || (IsSameId(a1, b2) && IsSameId(a2, b1));
        }

        /// <summary>
        /// Whether two monitor IDs name the same monitor. Only ASCII letters fold, as the module's
        /// SameMonitorId folds them; device paths are ASCII.
        /// </summary>
        public static bool IsSameId(string a, string b)
        {
            if (a == null || b == null)
            {
                return a == null && b == null;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (FoldAscii(a[i]) != FoldAscii(b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static char FoldAscii(char c)
        {
            return c is >= 'A' and <= 'Z' ? (char)(c - 'A' + 'a') : c;
        }

        // Each way out of `from`: the display it leads to, and whether the cursor gets through
        // without the cross key. A shared edge is open unless its direction is a wall. A corner,
        // where `from` meets a display at a single point, is open unless a wall borders one of
        // the two sides of `from` at that point, as the module judges a move through it.
        private static IEnumerable<(int Other, bool Open)> Exits(CursorBrakeDetectedEdges detected, List<CursorBrakeEdgeSetting> edges, int from)
        {
            foreach (int neighbor in Neighbors(detected, from))
            {
                yield return (neighbor, DirectionValue(detected, edges, from, neighbor).Mode != CursorBrakeEdgeMode.Wall);
            }

            for (int other = 0; other < detected.Monitors.Count; other++)
            {
                if (other != from && CornerOpen(detected, edges, from, other) is bool open)
                {
                    yield return (other, open);
                }
            }
        }

        private static bool? CornerOpen(CursorBrakeDetectedEdges detected, List<CursorBrakeEdgeSetting> edges, int from, int to)
        {
            if (!TryRect(detected.Monitors[from], out var a) || !TryRect(detected.Monitors[to], out var b))
            {
                return null;
            }

            bool right = b.Left == a.Right;
            bool below = b.Top == a.Bottom;
            if (!(right || b.Right == a.Left) || !(below || b.Bottom == a.Top))
            {
                return null;
            }

            int x = right ? a.Right - 1 : a.Left;
            int y = below ? a.Bottom - 1 : a.Top;
            bool Walled(int? neighbor) => neighbor is int n && DirectionValue(detected, edges, from, n).Mode == CursorBrakeEdgeMode.Wall;
            return !Walled(NeighborAcross(detected, from, true, right, y)) && !Walled(NeighborAcross(detected, from, false, below, x));
        }

        // The display across one side of `source` at `along`, the coordinate along that side, as
        // the module's NeighborAcross finds it: the first whose opposite side lies on that side's
        // line and whose span holds `along`.
        private static int? NeighborAcross(CursorBrakeDetectedEdges detected, int source, bool sideBySide, bool farSide, int along)
        {
            TryRect(detected.Monitors[source], out var rect);
            for (int i = 0; i < detected.Monitors.Count; i++)
            {
                if (i == source || !TryRect(detected.Monitors[i], out var other))
                {
                    continue;
                }

                bool onLine = sideBySide ? (farSide ? other.Left == rect.Right : other.Right == rect.Left) : (farSide ? other.Top == rect.Bottom : other.Bottom == rect.Top);
                bool overlap = sideBySide ? other.Top < rect.Bottom && rect.Top < other.Bottom : other.Left < rect.Right && rect.Left < other.Right;
                bool spans = sideBySide ? along >= other.Top && along < other.Bottom : along >= other.Left && along < other.Right;
                if (onLine && overlap && spans)
                {
                    return i;
                }
            }

            return null;
        }

        private static bool TryRect(CursorBrakeDetectedMonitor monitor, out (int Left, int Top, int Right, int Bottom) rect)
        {
            rect = default;
            if (monitor?.Rect is not { Count: 4 } r || r[2] <= r[0] || r[3] <= r[1])
            {
                return false;
            }

            rect = (r[0], r[1], r[2], r[3]);
            return true;
        }

        private static IEnumerable<int> Neighbors(CursorBrakeDetectedEdges detected, int monitor)
        {
            foreach (var edge in detected.Edges)
            {
                if (edge?.Monitors is not { Count: 2 } || edge.Monitors.Any(i => i < 0 || i >= detected.Monitors.Count))
                {
                    continue;
                }

                if (edge.Monitors[0] == monitor)
                {
                    yield return edge.Monitors[1];
                }
                else if (edge.Monitors[1] == monitor)
                {
                    yield return edge.Monitors[0];
                }
            }
        }

        private static bool IsValid(CursorBrakeEdgeSetting entry)
        {
            return entry?.Monitors is { Count: 2 }
                && !string.IsNullOrEmpty(entry.Monitors[0])
                && !string.IsNullOrEmpty(entry.Monitors[1])
                && !IsSameId(entry.Monitors[0], entry.Monitors[1])
                && entry.Mode is CursorBrakeEdgeMode.Custom or CursorBrakeEdgeMode.Off or CursorBrakeEdgeMode.Wall;
        }

        private static bool IsPair(CursorBrakeEdgeSetting entry, string first, string second)
        {
            return entry?.Monitors is { Count: 2 } && IsSamePair(entry.Monitors[0], entry.Monitors[1], first, second);
        }

        private static IEnumerable<CursorBrakeEdgeSetting> EntriesFor(string first, string second, CursorBrakeEdgeValue firstToSecond, CursorBrakeEdgeValue secondToFirst)
        {
            if (firstToSecond == secondToFirst)
            {
                if (firstToSecond.Mode != CursorBrakeEdgeMode.Default)
                {
                    bool inOrder = StringComparer.OrdinalIgnoreCase.Compare(first, second) <= 0;
                    yield return new CursorBrakeEdgeSetting
                    {
                        Monitors = { inOrder ? first : second, inOrder ? second : first },
                        Mode = firstToSecond.Mode,
                        PushDistance = firstToSecond.PushDistance,
                    };
                }

                yield break;
            }

            if (firstToSecond.Mode != CursorBrakeEdgeMode.Default)
            {
                yield return new CursorBrakeEdgeSetting { Monitors = { first, second }, OneWay = true, Mode = firstToSecond.Mode, PushDistance = firstToSecond.PushDistance };
            }

            if (secondToFirst.Mode != CursorBrakeEdgeMode.Default)
            {
                yield return new CursorBrakeEdgeSetting { Monitors = { second, first }, OneWay = true, Mode = secondToFirst.Mode, PushDistance = secondToFirst.PushDistance };
            }
        }
    }
}
