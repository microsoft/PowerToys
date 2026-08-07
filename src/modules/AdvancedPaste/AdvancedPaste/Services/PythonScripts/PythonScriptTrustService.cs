// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Settings;
using ManagedCommon;
using Microsoft.UI.Xaml.Controls;

namespace AdvancedPaste.Services.PythonScripts;

public sealed class PythonScriptTrustService(IUserSettings userSettings) : IPythonScriptTrustService
{
    private readonly IUserSettings _userSettings = userSettings;

    public bool IsTrusted(string scriptPath, string hash)
    {
        var hashes = _userSettings.TrustedScriptHashes;
        if (hashes is null || !hashes.TryGetValue(scriptPath, out var storedHash))
        {
            return false;
        }

        return string.Equals(hash, storedHash, StringComparison.OrdinalIgnoreCase);
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
                    scriptPath,
                    hash),
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
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException(null, scriptPath);
        }

        var root = Path.GetDirectoryName(scriptPath)
            ?? throw new ArgumentException("Script path must have a parent directory.", nameof(scriptPath));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (var file in Directory.EnumerateFiles(root, "*.py", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
            hash.AppendData(Encoding.UTF8.GetBytes(relativePath));
            hash.AppendData([0]);

            using var stream = File.OpenRead(file);
            hash.AppendData(SHA256.HashData(stream));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public async Task<bool> RequestInstallAsync(string scriptName, IReadOnlyList<PythonRequirement> missingPackages)
    {
        try
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            var packageList = string.Join("\n", missingPackages.Select(r =>
                string.Equals(r.ImportName, r.PipPackage, StringComparison.Ordinal)
                    ? $"  • {r.PipPackage}"
                    : $"  • {r.PipPackage}  (import: {r.ImportName})"));

            var dialog = new ContentDialog
            {
                Title = resourceLoader.GetString("PythonPackageInstallTitle"),
                Content = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    resourceLoader.GetString("PythonPackageInstallContent"),
                    scriptName,
                    packageList),
                PrimaryButtonText = resourceLoader.GetString("PythonPackageInstallConfirm"),
                CloseButtonText = resourceLoader.GetString("PythonPackageInstallCancel"),
            };

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
            Logger.LogError("Failed to show package install dialog", ex);
            return false;
        }
    }
}
