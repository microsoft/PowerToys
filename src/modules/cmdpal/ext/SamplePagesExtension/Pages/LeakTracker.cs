// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;

namespace SamplePagesExtension.Pages;

/// <summary>
/// Tracks what the host has and has not released, per category.
/// <para>
/// The extension deliberately keeps no reference to the items it returns, so
/// once the host releases its COM proxies these objects become collectable and
/// their finalizers run. Anything still alive after a full collection is being
/// held by the host - the extension cannot free it, because the CCW refcount
/// the host holds pins the object from this side.
/// </para>
/// </summary>
internal static class LeakTracker
{
    private static int _generations;

    /// <summary>Gets ballast payloads, one per list item.</summary>
    public static LeakCounter Payloads { get; } = new("Items");

    /// <summary>Gets icon stream references, one per data-backed icon.</summary>
    public static LeakCounter Streams { get; } = new("Icon streams");

    /// <summary>Gets commands - each item's primary command plus one per context item.</summary>
    public static LeakCounter Commands { get; } = new("Commands");

    /// <summary>Gets MoreCommands context items.</summary>
    public static LeakCounter ContextItems { get; } = new("Context items");

    public static IReadOnlyList<LeakCounter> All { get; } = [Payloads, Commands, ContextItems, Streams];

    public static int Generations => Volatile.Read(ref _generations);

    public static int StartGeneration() => Interlocked.Increment(ref _generations);

    public static void Reset()
    {
        Interlocked.Exchange(ref _generations, 0);

        foreach (var counter in All)
        {
            counter.ResetTotals();
        }
    }

    /// <summary>
    /// Collect, drain the finalizer queue, then collect again.
    /// </summary>
    /// <remarks>
    /// A bare <c>GC.Collect()</c> - including the periodic one extensions run -
    /// only promotes finalizable objects onto the finalizer queue. Their memory
    /// is not reclaimed, and the extension-side objects they reference are not
    /// released, until the finalizers have run and a second collection sweeps
    /// them.
    /// </remarks>
    public static void ForceFullCollection()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
    }
}
