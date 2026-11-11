// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PowerScripts.Client;

namespace PowerScripts.UI.ViewModels;

/// <summary>Uses the ordinary Host contract; no preview-specific execution or approval path exists.</summary>
internal interface IPowerScriptOperationPreviewBackend
{
    Task<string> CreateAsync(string root, JsonObject request, CancellationToken cancellationToken);

    Task<PowerScriptTransformResult> RunAsync(
        string root,
        string id,
        PowerScriptTransformInput input,
        CancellationToken cancellationToken);

    Task RevokeAsync(string root, string id, CancellationToken cancellationToken);
}
