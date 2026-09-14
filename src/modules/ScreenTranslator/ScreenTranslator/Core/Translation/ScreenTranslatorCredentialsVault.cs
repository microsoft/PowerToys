// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Windows.Security.Credentials;

namespace ScreenTranslator.Core.Translation;

/// <summary>
/// Secure credential accessor using Windows PasswordVault for Screen Translator API keys.
/// </summary>
public static class ScreenTranslatorCredentialsVault
{
    public const string AzureCredentialResource = "https://api.cognitive.microsofttranslator.com";
    public const string AzureCredentialUsername = "PowerToys_ScreenTranslator_AzureTranslator";

    public const string LibreTranslateCredentialResource = "https://libretranslate.com";
    public const string LibreTranslateCredentialUsername = "PowerToys_ScreenTranslator_LibreTranslate";

    public static string GetAzureApiKey()
    {
        return RetrieveKey(AzureCredentialResource, AzureCredentialUsername);
    }

    public static string GetLibreTranslateApiKey()
    {
        return RetrieveKey(LibreTranslateCredentialResource, LibreTranslateCredentialUsername);
    }

    public static void SaveAzureApiKey(string apiKey)
    {
        SaveKey(AzureCredentialResource, AzureCredentialUsername, apiKey);
    }

    public static void SaveLibreTranslateApiKey(string apiKey)
    {
        SaveKey(LibreTranslateCredentialResource, LibreTranslateCredentialUsername, apiKey);
    }

    public static void RemoveAzureApiKey()
    {
        RemoveKey(AzureCredentialResource, AzureCredentialUsername);
    }

    public static void RemoveLibreTranslateApiKey()
    {
        RemoveKey(LibreTranslateCredentialResource, LibreTranslateCredentialUsername);
    }

    private static string RetrieveKey(string resource, string username)
    {
        try
        {
            var vault = new PasswordVault();
            var cred = vault.Retrieve(resource, username);
            cred?.RetrievePassword();
            return cred?.Password?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void SaveKey(string resource, string username, string secret)
    {
        try
        {
            var vault = new PasswordVault();
            RemoveKey(resource, username);
            if (!string.IsNullOrWhiteSpace(secret))
            {
                var cred = new PasswordCredential(resource, username, secret.Trim());
                vault.Add(cred);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to store credential in Windows Credential Vault: {ex.Message}");
        }
    }

    private static void RemoveKey(string resource, string username)
    {
        try
        {
            var vault = new PasswordVault();
            var cred = vault.Retrieve(resource, username);
            if (cred != null)
            {
                vault.Remove(cred);
            }
        }
        catch
        {
            // Credential doesn't exist
        }
    }
}
