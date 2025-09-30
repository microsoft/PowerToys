// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using AdvancedPaste.Settings;
using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.Services;

/// <summary>
/// Dynamic credentials provider that resolves credentials based on the current Advanced AI configuration.
/// </summary>
public sealed class DynamicAdvancedAICredentialsProvider : IAdvancedAICredentialsProvider
{
    private readonly IUserSettings _userSettings;
    private readonly object _syncLock = new();

    private IAICredentialsProvider _currentProvider;
    private string _currentServiceType;
    private bool _useSharedCredentials;

    public DynamicAdvancedAICredentialsProvider(IUserSettings userSettings)
    {
        _userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));
        UpdateProvider(force: true);
    }

    public string Key
    {
        get
        {
            lock (_syncLock)
            {
                UpdateProvider();
                return _currentProvider?.Key ?? string.Empty;
            }
        }
    }

    public bool IsConfigured
    {
        get
        {
            lock (_syncLock)
            {
                UpdateProvider();
                return _currentProvider?.IsConfigured ?? false;
            }
        }
    }

    public bool Refresh()
    {
        lock (_syncLock)
        {
            bool providerChanged = UpdateProvider();

            if (_currentProvider is null)
            {
                return providerChanged;
            }

            return providerChanged || _currentProvider.Refresh();
        }
    }

    private bool UpdateProvider(bool force = false)
    {
        var advancedConfig = _userSettings?.AdvancedAIConfiguration ?? new AdvancedAIConfiguration();
        string normalizedServiceType = NormalizeServiceType(advancedConfig.ServiceType);
        bool useShared = advancedConfig.UseSharedCredentials;

        if (!force && _currentProvider != null &&
            string.Equals(normalizedServiceType, _currentServiceType, StringComparison.OrdinalIgnoreCase) &&
            useShared == _useSharedCredentials)
        {
            return false;
        }

        _currentProvider = CreateProvider(advancedConfig, normalizedServiceType, useShared);
        _currentServiceType = normalizedServiceType;
        _useSharedCredentials = useShared;
        return true;
    }

    private static IAICredentialsProvider CreateProvider(AdvancedAIConfiguration config, string normalizedServiceType, bool useShared)
    {
        if (useShared && normalizedServiceType == "openai")
        {
            return new OpenAI.VaultCredentialsProvider();
        }

        return new EnhancedVaultCredentialsProvider(config.ServiceType, "AdvancedAI");
    }

    private static string NormalizeServiceType(string serviceType)
    {
        if (string.IsNullOrWhiteSpace(serviceType))
        {
            return "openai";
        }

        var normalized = serviceType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "azure" => "azureopenai",
            _ => normalized,
        };
    }
}
