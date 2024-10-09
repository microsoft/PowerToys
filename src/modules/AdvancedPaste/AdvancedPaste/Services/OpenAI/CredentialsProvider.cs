// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Security.Credentials;

namespace AdvancedPaste.Services.OpenAI;

public sealed class CredentialsProvider : IAICredentialsProvider
{
    public CredentialsProvider() => Refresh();

    public string Key { get; private set; }

    public bool IsConfigured => !string.IsNullOrEmpty(Key);

    public bool Refresh()
    {
        var oldKey = Key;
        Key = LoadKey();
        return oldKey != Key;
    }

    private static string LoadKey()
    {
        try
        {
            var cred = new PasswordVault().Retrieve("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey");
            return cred?.Password ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
