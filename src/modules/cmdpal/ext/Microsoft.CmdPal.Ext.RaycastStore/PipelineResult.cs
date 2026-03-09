// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.RaycastStore;

internal sealed class PipelineResult
{
    public bool Success { get; init; }

    public string? ExtensionPath { get; init; }

    public string? Error { get; init; }

    public string Output { get; init; } = string.Empty;
}
