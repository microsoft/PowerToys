// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Opt-in dev tooling: counts live view-models so a leak can be confirmed
/// without a debugger.
/// </summary>
/// <remarks>
/// Register and RecordCleanup are [Conditional], so shipping builds do not
/// compile the calls at all - the guarantee is at the call site, not a runtime
/// flag someone could flip. Instances are held weakly rather than counted with
/// finalizers, which would delay collection of the very objects being measured.
/// </remarks>
public static class ViewModelInstanceTracker
{
    // Drop collected entries periodically so leaving tracking on does not grow
    // the registry without bound.
    private const int PruneInterval = 1024;

    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, List<WeakReference<object>>> Registry = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> Cleanups = new(StringComparer.Ordinal);

    // Counted separately from the registry, which shrinks as pruning drops
    // collected entries.
    private static readonly Dictionary<string, int> Created = new(StringComparer.Ordinal);

    private static bool _isEnabled;

    /// <summary>
    /// Gets a value indicating whether this build compiled the tracking calls in at all.
    /// </summary>
    public static bool IsAvailable =>
#if CMDPAL_DIAGNOSTICS
        true;
#else
        false;
#endif

    /// <summary>
    /// Gets or sets a value indicating whether new view-models are recorded.
    /// Cannot be turned on when <see cref="IsAvailable"/> is false.
    /// </summary>
    public static bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value && IsAvailable;
    }

    [Conditional("CMDPAL_DIAGNOSTICS")]
    public static void Register(object instance)
    {
        if (!_isEnabled)
        {
            return;
        }

        var name = instance.GetType().Name;

        lock (Gate)
        {
            if (!Registry.TryGetValue(name, out var instances))
            {
                instances = [];
                Registry[name] = instances;
            }

            instances.Add(new WeakReference<object>(instance));
            Created[name] = Created.GetValueOrDefault(name) + 1;

            if (instances.Count % PruneInterval == 0)
            {
                Registry[name] = Prune(instances);
            }
        }
    }

    /// <summary>
    /// Records that cleanup ran, so "cleanup never ran" can be told apart from
    /// "cleanup ran but something still holds this".
    /// </summary>
    [Conditional("CMDPAL_DIAGNOSTICS")]
    public static void RecordCleanup(object instance)
    {
        if (!_isEnabled)
        {
            return;
        }

        var name = instance.GetType().Name;

        lock (Gate)
        {
            Cleanups[name] = Cleanups.TryGetValue(name, out var count) ? count + 1 : 1;
        }
    }

    /// <summary>
    /// Counts what is still alive per type. Only meaningful after a collection -
    /// see <see cref="ForceFullCollection"/>.
    /// </summary>
    public static IReadOnlyList<(string TypeName, int Alive, int Seen, int CleanedUp)> Snapshot()
    {
        lock (Gate)
        {
            var results = new List<(string TypeName, int Alive, int Seen, int CleanedUp)>(Registry.Count);

            foreach (var (name, instances) in Registry)
            {
                var survivors = Prune(instances);

                results.Add((name, survivors.Count, Created.GetValueOrDefault(name), Cleanups.GetValueOrDefault(name)));
                Registry[name] = survivors;
            }

            results.Sort(static (x, y) => y.Alive.CompareTo(x.Alive));
            return results;
        }
    }

    public static void Reset()
    {
        lock (Gate)
        {
            Registry.Clear();
            Cleanups.Clear();
            Created.Clear();
        }
    }

    /// <summary>
    /// Collect, drain, collect. A bare collect only queues the finalizers, and it
    /// is the finalizers that release the cross-process proxies.
    /// </summary>
    public static void ForceFullCollection()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
    }

    // Rebuild rather than removing in place; removing by index from a List<T> is
    // O(n) per removal, which is O(n^2) over the thousands this is meant to measure.
    private static List<WeakReference<object>> Prune(List<WeakReference<object>> instances)
    {
        var survivors = new List<WeakReference<object>>(instances.Count);

        foreach (var reference in instances)
        {
            if (reference.TryGetTarget(out _))
            {
                survivors.Add(reference);
            }
        }

        return survivors;
    }
}
