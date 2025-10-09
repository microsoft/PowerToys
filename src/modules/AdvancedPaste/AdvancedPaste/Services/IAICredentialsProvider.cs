// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AdvancedPaste.Services;

/// <summary>
/// Represents the scope a credential lookup is targeting.
/// </summary>
public enum AICredentialScope
{
    PasteAI,
    AdvancedAI,
}

/// <summary>
/// Provides access to AI credentials stored for Advanced Paste scenarios.
/// </summary>
public interface IAICredentialsProvider
{
    /// <summary>
    /// Gets a value indicating whether the specified scope has a configured credential.
    /// </summary>
    /// <param name="scope">Scope to evaluate.</param>
    /// <returns><see langword="true"/> when a non-empty credential exists for the scope.</returns>
    bool IsConfigured(AICredentialScope scope);

    /// <summary>
    /// Retrieves the credential for the requested scope.
    /// </summary>
    /// <param name="scope">Scope to evaluate.</param>
    /// <returns>Credential string or <see cref="string.Empty"/> when missing.</returns>
    string GetKey(AICredentialScope scope);

    /// <summary>
    /// Refreshes the cached credential for the provided scope.
    /// </summary>
    /// <param name="scope">Scope to refresh.</param>
    /// <returns><see langword="true"/> when the credential changed.</returns>
    bool Refresh(AICredentialScope scope);
}
