// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

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

    internal IDisposable? Acquire()
    {
        while (true)
        {
            var count = Volatile.Read(ref _referenceCount);
            if (count == 0)
            {
                return null;
            }

            if (Interlocked.CompareExchange(ref _referenceCount, count + 1, count) == count)
            {
                return new Lease(this);
            }
        }
    }

    internal bool TryAcquireOwner()
    {
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

    internal void CloseSnapshot()
    {
        try
        {
            (_snapshot as IDisposable)?.Dispose();
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
