// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AdvancedPaste.Services;

/// <summary>
/// Provides access to AI credentials stored for Advanced Paste scenarios.
/// </summary>
public interface IAICredentialsProvider
{
    /// <summary>
    /// Gets a value indicating whether any credential is configured.
    /// </summary>
    /// <returns><see langword="true"/> when a non-empty credential exists for the active AI provider.</returns>
    bool IsConfigured();

    /// <summary>
    /// Retrieves the credential for the active AI provider.
    /// </summary>
    /// <returns>Credential string or <see cref="string.Empty"/> when missing.</returns>
    string GetKey();

    /// <summary>
    /// Refreshes the cached credential for the active AI provider.
    /// </summary>
    /// <returns><see langword="true"/> when the credential changed.</returns>
    bool Refresh();
}
