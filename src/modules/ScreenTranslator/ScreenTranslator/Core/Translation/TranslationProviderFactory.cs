// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Library;

namespace ScreenTranslator.Core.Translation;

/// <summary>
/// Factory to instantiate the configured translation provider based on user settings.
/// </summary>
public static class TranslationProviderFactory
{
    public static ITranslationProvider Create(ScreenTranslatorSettings? settings)
    {
        if (settings?.Properties == null)
        {
            return new PassthroughTranslationProvider();
        }

        var providerType = settings.Properties.SelectedProvider ?? "Passthrough";
        var cloudConsent = settings.Properties.EnableCloudConsent;

        switch (providerType)
        {
            case "AzureTranslator":
                var azureEndpoint = settings.Properties.AzureEndpoint ?? "https://api.cognitive.microsofttranslator.com";
                var azureRegion = settings.Properties.AzureRegion ?? string.Empty;
                var azureApiKey = ScreenTranslatorCredentialsVault.GetAzureApiKey();
                return new AzureTranslatorProvider(azureEndpoint, azureRegion, azureApiKey, cloudConsent);

            case "LibreTranslate":
                var libreEndpoint = settings.Properties.LibreTranslateEndpoint ?? "http://localhost:5000";
                var libreApiKey = ScreenTranslatorCredentialsVault.GetLibreTranslateApiKey();
                return new LibreTranslateProvider(libreEndpoint, libreApiKey, cloudConsent);

            case "Passthrough":
            default:
                return new PassthroughTranslationProvider();
        }
    }
}
