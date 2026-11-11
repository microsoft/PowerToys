// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using PowerScripts.Client;

namespace PowerScripts.UI.ViewModels;

public partial class PowerScriptsViewModel
{
    /// <summary>Use the editor's installation-aware discovery and the ordinary consuming client.</summary>
    public Task<PowerScriptsClient> CreateRunClientAsync(CancellationToken cancellationToken = default)
    {
        var root = ScriptsFolder;
        return Task.Run(
            () => new PowerScriptsClient(
                PowerScriptConsumer.Editor,
                hostPath: ResolveHostPath(),
                scriptsRoot: root),
            cancellationToken);
    }
}
