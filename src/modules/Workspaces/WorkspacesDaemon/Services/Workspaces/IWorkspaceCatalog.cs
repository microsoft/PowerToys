// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.WorkspacesMCP.Services.Workspaces;

public interface IWorkspaceCatalog
{
    IReadOnlyList<WorkspaceEntry> Workspaces { get; }

    DateTime LoadedAtUtc { get; }
}
