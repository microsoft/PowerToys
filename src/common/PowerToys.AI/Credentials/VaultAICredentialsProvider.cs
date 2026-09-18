// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.AI.Credentials;

/// <summary>
/// Credentials provider that resolves the active provider from a caller-supplied callback
/// and loads keys via <see cref="AICredentialVault"/>.
/// </summary>
public sealed class VaultAICredentialsProvider : IAICredentialsProvider
{
    private sealed class CredentialSlot
    {
        public AIServiceType ServiceType { get; set; } = AIServiceType.Unknown;

        public string ProviderId { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;
    }

    private readonly Func<(AIServiceType ServiceType, string ProviderId)> _resolveActiveProvider;
    private readonly CredentialSlot _slot = new();
    private readonly Lock _syncRoot = new();

    public VaultAICredentialsProvider(Func<(AIServiceType ServiceType, string ProviderId)> resolveActiveProvider)
    {
        _resolveActiveProvider = resolveActiveProvider ?? throw new ArgumentNullException(nameof(resolveActiveProvider));
        Refresh();
    }

    public string GetKey()
    {
        using (_syncRoot.EnterScope())
        {
            UpdateSlot(forceRefresh: false);
            return _slot.Key;
        }
    }

    public bool IsConfigured() => !string.IsNullOrEmpty(GetKey());

    public string GetKey(AIServiceType serviceType, string providerId)
    {
        var normalizedType = serviceType == AIServiceType.Unknown ? AIServiceType.OpenAI : serviceType;
        return AICredentialVault.LoadKey(normalizedType, providerId ?? string.Empty);
    }

    public bool Refresh()
    {
        using (_syncRoot.EnterScope())
        {
            return UpdateSlot(forceRefresh: true);
        }
    }

    private bool UpdateSlot(bool forceRefresh)
    {
        var (serviceType, providerId) = _resolveActiveProvider();
        var desiredServiceType = serviceType == AIServiceType.Unknown ? AIServiceType.OpenAI : serviceType;
        providerId ??= string.Empty;

        var hasChanged = false;

        if (_slot.ServiceType != desiredServiceType || !string.Equals(_slot.ProviderId, providerId, StringComparison.Ordinal))
        {
            _slot.ServiceType = desiredServiceType;
            _slot.ProviderId = providerId;
            forceRefresh = true;
            hasChanged = true;
        }

        if (!forceRefresh)
        {
            return hasChanged;
        }

        var newKey = AICredentialVault.LoadKey(_slot.ServiceType, _slot.ProviderId);
        if (!string.Equals(_slot.Key, newKey, StringComparison.Ordinal))
        {
            _slot.Key = newKey;
            hasChanged = true;
        }

        return hasChanged;
    }
}
