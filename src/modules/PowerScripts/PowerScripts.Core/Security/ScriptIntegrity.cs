// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;
using System.Text;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Security;

/// <summary>
/// Computes a stable content fingerprint for a script. The fingerprint covers both the executable
/// body and the parts of the manifest that define what the script is allowed to do, so that editing
/// the script <em>or</em> escalating its declared capabilities invalidates any prior user trust and
/// forces a fresh consent prompt (trust-on-first-use).
/// </summary>
public static class ScriptIntegrity
{
    /// <summary>
    /// Returns the lowercase hex SHA-256 of the script's entry-file bytes combined with its declared
    /// <c>kind</c> and (sorted) <c>capabilities</c>. Returns an empty string if the entry file is
    /// missing (an untrusted state that will never match a stored trust record).
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

        var capabilities = manifest.Capabilities
            .Select(c => c.Trim().ToLowerInvariant())
            .Where(c => c.Length > 0)
            .OrderBy(c => c, StringComparer.Ordinal);

        var declaration = $"\nkind={manifest.Kind}\ncapabilities={string.Join(',', capabilities)}\n";

        using var sha = SHA256.Create();
        sha.TransformBlock(body, 0, body.Length, null, 0);
        var declarationBytes = Encoding.UTF8.GetBytes(declaration);
        sha.TransformFinalBlock(declarationBytes, 0, declarationBytes.Length);

        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }
}
