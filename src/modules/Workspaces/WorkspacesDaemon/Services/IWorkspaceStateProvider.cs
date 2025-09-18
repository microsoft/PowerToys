// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.WorkspacesMCP.Services;

public interface IWorkspaceStateProvider
{
    /// <summary>Latest snapshot or null if not produced yet.</summary>
    ImmutableWorkspaceSnapshot? Current { get; }

    /// <summary>Wait (briefly) for first snapshot; returns empty snapshot if still unavailable.</summary>
    ImmutableWorkspaceSnapshot GetOrWait(int timeoutMs = 1500);
}
