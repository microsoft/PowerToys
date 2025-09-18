// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;

namespace PowerToys.WorkspacesMCP.Services;

public class WorkspaceStateProvider : IWorkspaceStateProvider, IDisposable
{
    private readonly ManualResetEventSlim _firstReady = new(false);
    private ImmutableWorkspaceSnapshot? _current;

    public ImmutableWorkspaceSnapshot? Current => Volatile.Read(ref _current);

    internal void Publish(ImmutableWorkspaceSnapshot snapshot)
    {
        Volatile.Write(ref _current, snapshot);
        _firstReady.Set();
    }

    public ImmutableWorkspaceSnapshot GetOrWait(int timeoutMs = 1500)
    {
        var existing = _current;
        if (existing != null)
        {
            return existing;
        }

        _firstReady.Wait(timeoutMs);
        return _current ?? new ImmutableWorkspaceSnapshot(
            TimestampUtc: DateTime.UtcNow,
            Apps: Array.Empty<PowerToys.WorkspacesMCP.Models.AppInfo>(),
            Windows: Array.Empty<PowerToys.WorkspacesMCP.Models.WindowInfo>(),
            VisibleWindows: 0,
            Version: 0);
    }

    public void Dispose()
    {
        _firstReady.Dispose();
        GC.SuppressFinalize(this);
    }
}
