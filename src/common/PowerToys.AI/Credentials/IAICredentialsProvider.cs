// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.AI.Credentials;

/// <summary>
/// Reads and writes AI provider API keys from the Windows Credential Vault.
/// </summary>
public interface IAICredentialsProvider
{
    bool IsConfigured();

    string GetKey();

    string GetKey(AIServiceType serviceType, string providerId);

    bool Refresh();
}
