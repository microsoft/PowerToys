// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.ModuleContracts;
using WorkspacesCsharpLibrary.Data;

namespace Workspaces.ModuleServices;

/// <summary>
/// Workspaces-specific operations.
/// </summary>
public interface IWorkspaceService : IModuleService
{
    Task<OperationResult> LaunchWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task<OperationResult> LaunchEditorAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> SnapshotAsync(string? targetPath = null, CancellationToken cancellationToken = default);

    Task<OperationResult<IReadOnlyList<ProjectWrapper>>> GetWorkspacesAsync(CancellationToken cancellationToken = default);
}
