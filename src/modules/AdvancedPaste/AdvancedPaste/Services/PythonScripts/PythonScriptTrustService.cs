// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Settings;
using ManagedCommon;
using Microsoft.UI.Xaml.Controls;

namespace AdvancedPaste.Services.PythonScripts;

public sealed class PythonScriptTrustService(IUserSettings userSettings) : IPythonScriptTrustService
{
    private readonly IUserSettings _userSettings = userSettings;

    public bool IsTrusted(string scriptPath)
    {
        var hashes = _userSettings.TrustedScriptHashes;
        if (hashes is null || !hashes.TryGetValue(scriptPath, out var storedHash))
        {
            return false;
        }

        try
        {
            var currentHash = ComputeHash(scriptPath);
            return string.Equals(currentHash, storedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to compute hash for {scriptPath}", ex);
            return false;
        }
    }

    public async Task<bool> RequestTrustAsync(string scriptPath, string hash)
    {
        try
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            var dialog = new ContentDialog
            {
                Title = resourceLoader.GetString("PythonScriptTrustTitle"),
                Content = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    resourceLoader.GetString("PythonScriptTrustContent"),
                    scriptPath),
                PrimaryButtonText = resourceLoader.GetString("PythonScriptTrustConfirm"),
                CloseButtonText = resourceLoader.GetString("PythonScriptTrustCancel"),
            };

            // XamlRoot must be set for ContentDialog to function.
            var mainWindow = (Microsoft.UI.Xaml.Application.Current as AdvancedPaste.App)?.GetMainWindow();
            if (mainWindow?.Content?.XamlRoot is { } xamlRoot)
            {
                dialog.XamlRoot = xamlRoot;
            }

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to show trust dialog", ex);
            return false;
        }
    }

    public void StoreTrust(string scriptPath, string hash)
    {
        _userSettings.StoreTrustedScriptHash(scriptPath, hash);
    }

    public string ComputeHash(string scriptPath)
    {
        using var stream = File.OpenRead(scriptPath);
        var hashBytes = SHA256.HashData(stream);
        return Convert.ToHexStringLower(hashBytes);
    }
}
