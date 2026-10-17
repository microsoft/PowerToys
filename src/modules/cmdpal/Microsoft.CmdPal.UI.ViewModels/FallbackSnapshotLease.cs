// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Counts the references to one fallback result snapshot and closes it when the last
/// reference goes away.
/// </summary>
/// <remarks>
/// <para>
/// A snapshot is an object in the extension process. Every row built from it, and
/// every command started from one of those rows, reads through it. Closing it too
/// early breaks all of them, so each user takes a reference first.
/// </para>
/// <para>
/// There are two kinds of reference. The owner is the page that shows the snapshot;
/// it holds one reference and gives it back when a newer query replaces the snapshot.
/// Callers of <see cref="Acquire"/> hold the others, for as long as one row or one
/// command needs the data. The count starts at 1 because the owner exists as soon as
/// the lease does.
/// </para>
/// </remarks>
internal sealed partial class FallbackSnapshotLease
{
    private readonly IFallbackCommandResult _snapshot;
    private readonly Action<FallbackSnapshotLease>? _released;
    private int _referenceCount = 1;

    internal FallbackSnapshotLease(
        IFallbackCommandResult snapshot,
        Action<FallbackSnapshotLease>? released = null)
    {
        _snapshot = snapshot;
        _released = released;
    }

    internal IFallbackCommandResult Snapshot => _snapshot;

    /// <summary>
    /// Takes a reference on the snapshot.
    /// </summary>
    /// <returns>
    /// Null when the snapshot already closed. The caller must then stop: the data is
    /// gone. Dispose the result to give the reference back.
    /// </returns>
    internal IDisposable? Acquire() => TryIncrement() ? new Lease(this) : null;

    /// <summary>
    /// Takes a second owner reference on a snapshot that is already published.
    /// </summary>
    /// <remarks>
    /// An extension can report the same snapshot again as it adds items. The page then
    /// holds the old state and the new state at the same time, so the snapshot needs an
    /// owner reference for each. The page gives the old one back right after.
    /// </remarks>
    /// <returns>False when the snapshot already closed.</returns>
    internal bool TryAcquireOwner() => TryIncrement();

    private bool TryIncrement()
    {
        // Compare and swap, because a count of 0 is final. A plain increment could
        // revive a snapshot that another thread is closing.
        while (true)
        {
            var count = Volatile.Read(ref _referenceCount);
            if (count == 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _referenceCount, count + 1, count) == count)
            {
                return true;
            }
        }
    }

    internal void ReleaseOwner() => Release();

    private void Release()
    {
        if (Interlocked.Decrement(ref _referenceCount) != 0)
        {
            return;
        }

        if (_released is not null)
        {
            _released(this);
            return;
        }

        CloseSnapshot();
    }

    internal void CloseSnapshot() => CloseSnapshot(_snapshot);

    /// <summary>
    /// Closes a snapshot that no one holds a reference on.
    /// </summary>
    internal static void CloseSnapshot(IFallbackCommandResult snapshot)
    {
        try
        {
            (snapshot as IDisposable)?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to close a fallback result snapshot.", ex);
        }
    }

    private sealed partial class Lease(FallbackSnapshotLease owner) : IDisposable
    {
        private FallbackSnapshotLease? _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release();
        }
    }
}
