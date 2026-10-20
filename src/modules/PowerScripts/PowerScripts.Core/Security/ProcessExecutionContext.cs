// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Security;

/// <summary>Security-relevant paths and identity attached to one process launch.</summary>
public sealed class ProcessExecutionContext
{
    public string ScriptId { get; init; } = string.Empty;

    public string ScriptDirectory { get; init; } = string.Empty;

    public string ScriptsRoot { get; init; } = string.Empty;

    public IReadOnlyList<string> InputPaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RecommendedPolicies { get; init; } = MxcPolicies.All;

    public static ProcessExecutionContext FromManifest(
        PowerScriptManifest manifest,
        IEnumerable<string?>? inputPaths = null,
        IReadOnlyDictionary<string, string?>? parameters = null) =>
        new()
        {
            ScriptId = manifest.Id,
            ScriptDirectory = manifest.FolderPath,
            ScriptsRoot = string.IsNullOrWhiteSpace(manifest.ScriptsRoot) ? manifest.FolderPath : manifest.ScriptsRoot,
            InputPaths = (inputPaths ?? Array.Empty<string?>())
                .Concat(GetFileParameterPaths(manifest, parameters))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            RecommendedPolicies = manifest.Mxc.RecommendedPolicies,
        };

    private static IEnumerable<string?> GetFileParameterPaths(
        PowerScriptManifest manifest,
        IReadOnlyDictionary<string, string?>? parameters)
    {
        if (parameters is null)
        {
            return Array.Empty<string?>();
        }

        var fileParameterNames = manifest.Parameters
            .Where(parameter => parameter.IsFile)
            .Select(parameter => parameter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return parameters
            .Where(parameter => fileParameterNames.Contains(parameter.Key))
            .Select(parameter => parameter.Value);
    }
}

/// <summary>A testable abstraction over the secure OS process-launch path.</summary>
public interface IProcessLauncher
{
    ProcessRunResult Run(ProcessStartInfo startInfo, string? standardInput, int timeoutMs);
}
