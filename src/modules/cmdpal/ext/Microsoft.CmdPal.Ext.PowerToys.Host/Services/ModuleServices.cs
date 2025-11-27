// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.ModuleContracts;

namespace Microsoft.CmdPal.Ext.PowerToys.Services;

/// <summary>
/// Centralized access to module services for the Command Palette host.
/// </summary>
internal static class ModuleServices
{
    private static readonly IWorkspaceService _workspaceService = new WorkspaceService();

    public static IWorkspaceService Workspaces() => _workspaceService;
}
