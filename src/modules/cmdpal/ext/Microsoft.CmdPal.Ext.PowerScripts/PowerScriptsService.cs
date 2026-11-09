// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts;

/// <summary>Adapts the shared PowerScripts Host to the input and output supported by Command Palette.</summary>
internal sealed class PowerScriptsService : IPowerScriptsService
{
    private readonly Func<PowerScriptsClient> _createClient;

    public PowerScriptsService()
        : this(CreateClient)
    {
    }

    internal PowerScriptsService(Func<PowerScriptsClient> createClient)
    {
        ArgumentNullException.ThrowIfNull(createClient);
        _createClient = createClient;
    }

    public async Task<PowerScriptsCatalog> DiscoverAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var client = _createClient();
            var state = await client.GetConsumerStateAsync(timeout.Token).ConfigureAwait(false);
            IReadOnlyList<PowerScriptInfo> scripts = state.IsEnabled && state.IsPowerScriptsEnabled
                ? await client.ListAsync(timeout.Token).ConfigureAwait(false)
                : [];
            return CreateCatalog(state, scripts);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(Properties.Resources.DiscoveryTimedOut, ex);
        }
    }

    public Task<PowerScriptTransformResult> RunAsync(
        PowerScriptInfo script,
        IReadOnlyDictionary<string, JsonElement> parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(parameters);
        if (!IsCompatible(script))
        {
            throw new ArgumentException("The script requires input or output that Command Palette does not support.", nameof(script));
        }

        // Keep the submitted request independent of subsequent edits or disposed JSON documents.
        var snapshot = parameters.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
        return RunCurrentAsync(script, snapshot, cancellationToken);
    }

    internal static bool IsCompatible(PowerScriptInfo script) =>
        script.Input == PowerScriptIO.None && script.Output is PowerScriptIO.None or PowerScriptIO.Text;

    internal static PowerScriptsCatalog CreateCatalog(PowerScriptConsumerState state, IReadOnlyList<PowerScriptInfo> scripts)
    {
        // A policy forcing the module on does not lock a consumer preference that the user may edit.
        var policyControlled = !state.IsPowerScriptsEnabled ? state.IsPowerScriptsPolicyControlled : state.IsPolicyControlled;
        if (!state.IsEnabled || !state.IsPowerScriptsEnabled)
        {
            var reason = state.DisabledReason ?? (!state.IsPowerScriptsEnabled ? "module-disabled" : policyControlled ? "policy-disabled" : "integration-disabled");
            return new PowerScriptsCatalog([], false, reason, policyControlled);
        }

        return new PowerScriptsCatalog(
            scripts.Where(IsCompatible)
                .OrderBy(script => script.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(script => script.Id, StringComparer.Ordinal)
                .ToArray(),
            true,
            IsPolicyControlled: policyControlled);
    }

    internal static bool HasSameParameterContract(PowerScriptInfo previous, PowerScriptInfo current)
    {
        if (previous.Parameters.Count != current.Parameters.Count)
        {
            return false;
        }

        for (var index = 0; index < previous.Parameters.Count; index++)
        {
            var left = previous.Parameters[index];
            var right = current.Parameters[index];
            if (left.Name != right.Name || left.Type != right.Type || left.IsRequired != right.IsRequired ||
                left.Default != right.Default || left.Min != right.Min || left.Max != right.Max ||
                !left.Options.SequenceEqual(right.Options, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return previous.InputSchema is { } previousSchema
            ? current.InputSchema is { } currentSchema && SameSchema(previousSchema, currentSchema)
            : current.InputSchema is null;
    }

    private static bool SameSchema(JsonElement left, JsonElement right, bool propertyMap = false)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        if (left.ValueKind == JsonValueKind.Object)
        {
            var leftProperties = left.EnumerateObject().Where(property => propertyMap || property.Name is not ("title" or "description")).ToArray();
            var rightProperties = right.EnumerateObject().Where(property => propertyMap || property.Name is not ("title" or "description")).ToArray();
            return leftProperties.Length == rightProperties.Length && leftProperties.All(property =>
                right.TryGetProperty(property.Name, out var value) && SameSchema(property.Value, value, !propertyMap && property.Name == "properties"));
        }

        if (left.ValueKind == JsonValueKind.Array)
        {
            return left.GetArrayLength() == right.GetArrayLength() &&
                left.EnumerateArray().Zip(right.EnumerateArray()).All(pair => SameSchema(pair.First, pair.Second));
        }

        return JsonElement.DeepEquals(left, right);
    }

    private async Task<PowerScriptTransformResult> RunCurrentAsync(
        PowerScriptInfo script,
        IReadOnlyDictionary<string, JsonElement> parameters,
        CancellationToken cancellationToken)
    {
        var catalog = await DiscoverAsync(cancellationToken).ConfigureAwait(false);
        var current = catalog.Scripts.FirstOrDefault(candidate => candidate.Id == script.Id);
        if (current is null)
        {
            throw new InvalidOperationException(Properties.Resources.ScriptUnavailable);
        }

        if (!HasSameParameterContract(script, current))
        {
            throw new InvalidOperationException(Properties.Resources.ScriptContractChanged);
        }

        var result = await _createClient().TransformAsync(
            current.Id,
            new PowerScriptTransformInput { Parameters = parameters.Count == 0 ? null : parameters },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.Succeeded && (result.FilePaths is not null || result.Image is not null || result.Audio is not null))
        {
            // The descriptor can still change between discovery and execution. Do not lose an
            // unsupported result or suggest that this invocation is safe to retry automatically.
            throw new InvalidDataException(Properties.Resources.UnsupportedResult);
        }

        return result;
    }

    private static PowerScriptsClient CreateClient()
    {
        // CmdPal is packaged separately from the Host. Resolve the PowerToys installation rather than
        // assuming the Host is inside the MSIX. Preserve the shared client's development override.
        var overridePath = Environment.GetEnvironmentVariable(PowerScriptsProtocol.HostPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return new PowerScriptsClient(PowerScriptConsumer.CommandPalette, hostPath: overridePath);
        }

        var installDirectory = PowerToysPathResolver.GetPowerToysInstallPath();
        if (!string.IsNullOrEmpty(installDirectory))
        {
            var hostPath = Path.Combine(installDirectory, "PowerScripts", PowerScriptsProtocol.HostExeName);
            if (File.Exists(hostPath))
            {
                return new PowerScriptsClient(PowerScriptConsumer.CommandPalette, hostPath: hostPath);
            }
        }

        return new PowerScriptsClient(PowerScriptConsumer.CommandPalette);
    }
}
