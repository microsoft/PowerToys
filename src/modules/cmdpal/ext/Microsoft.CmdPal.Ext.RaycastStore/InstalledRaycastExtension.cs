// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.RaycastStore;

internal sealed class InstalledRaycastExtension
{
    public string Name { get; init; } = string.Empty;

    public string RaycastName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;
}
