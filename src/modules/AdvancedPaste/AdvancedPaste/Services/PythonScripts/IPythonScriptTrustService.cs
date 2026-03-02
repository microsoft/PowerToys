// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace AdvancedPaste.Services.PythonScripts;

public interface IPythonScriptTrustService
{
    /// <summary>
    /// Returns true if the script at <paramref name="scriptPath"/> is currently trusted (hash matches stored value).
    /// </summary>
    bool IsTrusted(string scriptPath);

    /// <summary>
    /// Shows a UI confirmation dialog for the script. Returns true if the user approved execution.
    /// </summary>
    Task<bool> RequestTrustAsync(string scriptPath, string hash);

    /// <summary>
    /// Persists the trust entry for <paramref name="scriptPath"/> with the given <paramref name="hash"/>.
    /// </summary>
    void StoreTrust(string scriptPath, string hash);

    /// <summary>
    /// Computes the SHA-256 hash of the script file and returns the hex string.
    /// </summary>
    string ComputeHash(string scriptPath);
}
