// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts;

internal interface IPowerScriptsService
{
    Task<PowerScriptsCatalog> DiscoverAsync(CancellationToken cancellationToken);

    Task<PowerScriptTransformResult> RunAsync(
        PowerScriptInfo script,
        IReadOnlyDictionary<string, JsonElement> parameters,
        CancellationToken cancellationToken);
}
