// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.WorkspacesMCP.Services.Workspaces;

/// <summary>
/// Represents a single workspace definition entry loaded from workspaces.json.
/// </summary>
public sealed record WorkspaceEntry(string Id, string? Name)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : $"{Name} ({Id})";
}
