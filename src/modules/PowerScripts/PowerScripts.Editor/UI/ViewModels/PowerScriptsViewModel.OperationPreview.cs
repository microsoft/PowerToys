// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PowerScripts.Client;

namespace PowerScripts.UI.ViewModels;

public partial class PowerScriptsViewModel
{
    /// <summary>Tests a draft through ordinary discovery, trust, consumer policy and execution.</summary>
    public Task<PowerScriptTransformResult> TestOperationAsync(
        PowerScriptOperationTemplate template,
        JsonObject operation,
        JsonArray parameters,
        IReadOnlyDictionary<string, JsonElement> parameterValues,
        CancellationToken cancellationToken = default)
    {
        var preview = new PowerScriptOperationPreview(
            new OperationPreviewBackend(),
            Path.Combine(Path.GetTempPath(), "PowerToys", "PowerScripts", "Preview"),
            exception => ManagedCommon.Logger.LogError("Unable to clean up a PowerToys operation test run.", exception));
        return preview.RunAsync(template, operation, parameters, parameterValues, cancellationToken);
    }

    private sealed class OperationPreviewBackend : IPowerScriptOperationPreviewBackend
    {
        public async Task<string> CreateAsync(string root, JsonObject request, CancellationToken cancellationToken)
        {
            var result = await InvokeHostCommandAsync(
                new[] { "template", "create", "--configure", "--root", root },
                request,
                cancellationToken: cancellationToken);
            if (result.ExitCode != PowerScriptsProtocol.ExitCode.Ok)
            {
                throw HostFailure(result);
            }

            return JsonNode.Parse(result.StdOut)?["entryPath"]?.GetValue<string>()
                ?? throw new InvalidDataException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
        }

        public async Task<PowerScriptTransformResult> RunAsync(
            string root,
            string id,
            PowerScriptTransformInput input,
            CancellationToken cancellationToken)
        {
            var hostPath = await Task.Run(ResolveHostPath, cancellationToken);
            if (string.IsNullOrEmpty(hostPath))
            {
                throw new FileNotFoundException(GetResourceString("PowerScripts_MxcSupportHostMissing"));
            }

            var client = new PowerScriptsClient(PowerScriptConsumer.Editor, hostPath: hostPath, scriptsRoot: root);
            return await client.TransformAsync(id, input, noConsent: false, cancellationToken);
        }

        public async Task RevokeAsync(string root, string id, CancellationToken cancellationToken)
        {
            var result = await InvokeHostCommandAsync(
                new[] { "trust", "revoke", id, "--root", root },
                cancellationToken: cancellationToken);

            // A declined/cancelled prompt has no approval to revoke (ordinary Host exit 1).
            if (result.ExitCode is not (PowerScriptsProtocol.ExitCode.Ok or PowerScriptsProtocol.ExitCode.Usage))
            {
                throw HostFailure(result);
            }
        }

        private static InvalidOperationException HostFailure(PowerScriptRunResult result) =>
            new(string.IsNullOrWhiteSpace(result.StdErr)
                ? GetResourceString("PowerScripts_SettingsHostFailed")
                : result.StdErr.Trim());
    }
}
