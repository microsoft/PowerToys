// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;
using System.Text;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Security;

/// <summary>
/// Computes a stable content fingerprint for a script. The fingerprint covers the executable body,
/// its descriptor, and the parts of the manifest that define what the script is allowed to do, so
/// editing any execution metadata invalidates prior user trust and forces a fresh consent prompt.
/// </summary>
public static class ScriptIntegrity
{
    /// <summary>
    /// Returns the lowercase hex SHA-256 of length-delimited entry, descriptor, resolved <c>io</c>,
    /// and capability data. Returns an empty string if the entry file is missing.
    /// </summary>
    public static string ComputeHash(PowerScriptManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var entryPath = manifest.EntryFullPath;
        if (string.IsNullOrEmpty(entryPath) || !File.Exists(entryPath))
        {
            return string.Empty;
        }

        var body = File.ReadAllBytes(entryPath);
        var descriptorPath = entryPath + ToolDescriptorParser.DescriptorSuffix;
        var descriptor = File.Exists(descriptorPath)
            ? File.ReadAllBytes(descriptorPath)
            : Array.Empty<byte>();

        var capabilities = manifest.Capabilities
            .Select(c => c.Trim().ToLowerInvariant())
            .Where(c => c.Length > 0)
            .OrderBy(c => c, StringComparer.Ordinal);

        // The I/O contract (not a separate "kind") is what defines where a script can be used, so it
        // is part of the fingerprint: changing what a script consumes/produces re-prompts for consent.
        var signature = manifest.Runtime == ScriptRuntime.Python
            ? PowerScriptPythonConvention.ParseFile(entryPath)
            : null;
        var (input, output) = ScriptIo.Resolve(manifest, signature);

        var declaration = $"\nio={ScriptIo.ToToken(input)}->{ScriptIo.ToToken(output)}\ncapabilities={string.Join(',', capabilities)}\n";

        var declarationBytes = Encoding.UTF8.GetBytes(declaration);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(sha, body);
        AppendField(sha, descriptor);
        AppendField(sha, declarationBytes);
        return Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendField(IncrementalHash hash, byte[] value)
    {
        hash.AppendData(BitConverter.GetBytes(value.Length));
        hash.AppendData(value);
    }
}
