// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.Services;

public static class AdvancedPastePolicy
{
    public static bool IsAdvancedPasteEnabled =>
        PowerToys.GPOWrapper.GPOWrapper.GetConfiguredAdvancedPasteEnabledValue() != PowerToys.GPOWrapper.GpoRuleConfigured.Disabled;

    public static bool IsOnlineAIAllowed =>
        PowerToys.GPOWrapper.GPOWrapper.GetAllowedAdvancedPasteOnlineAIModelsValue() != PowerToys.GPOWrapper.GpoRuleConfigured.Disabled;

    public static bool IsOpenAIModerationAllowed => IsOnlineAIAllowed && IsServiceAllowed(AIServiceType.OpenAI);

    public static bool IsProviderAllowed(PasteAIProviderDefinition provider)
        => IsProviderAllowed(provider, () => IsOnlineAIAllowed, IsServiceAllowed);

    internal static bool IsProviderAllowed(PasteAIProviderDefinition provider, Func<bool> isOnlineAIAllowed, Func<AIServiceType, bool> isServiceAllowed)
    {
        if (provider is null)
        {
            return false;
        }

        var serviceType = provider.ServiceType.ToAIServiceType();
        if (serviceType == AIServiceType.Unknown)
        {
            serviceType = AIServiceType.OpenAI;
        }

        var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);
        if (metadata.IsOnlineService && !isOnlineAIAllowed())
        {
            return false;
        }

        return isServiceAllowed(serviceType);
    }

    private static bool IsServiceAllowed(AIServiceType serviceType)
        => serviceType switch
        {
            AIServiceType.OpenAI => PowerToys.GPOWrapper.GPOWrapper.GetAllowedAdvancedPasteOpenAIValue() != PowerToys.GPOWrapper.GpoRuleConfigured.Disabled,
            AIServiceType.AzureOpenAI => PowerToys.GPOWrapper.GPOWrapper.GetAllowedAdvancedPasteAzureOpenAIValue() != PowerToys.GPOWrapper.GpoRuleConfigured.Disabled,
            AIServiceType.AzureAIInference => PowerToys.GPOWrapper.GPOWrapper.GetAllowedAdvancedPasteAzureAIInferenceValue() != PowerToys.GPOWrapper.GpoRuleConfigured.Disabled,
            AIServiceType.Mistral => PowerToys.GPOWrapper.GPOWrapper.GetAllowedAdvancedPasteMistralValue() != PowerToys.GPOWrapper.GpoRuleConfigured.Disabled,
            AIServiceType.Google => PowerToys.GPOWrapper.GPOWrapper.GetAllowedAdvancedPasteGoogleValue() != PowerToys.GPOWrapper.GpoRuleConfigured.Disabled,
            AIServiceType.Ollama => PowerToys.GPOWrapper.GPOWrapper.GetAllowedAdvancedPasteOllamaValue() != PowerToys.GPOWrapper.GpoRuleConfigured.Disabled,
            AIServiceType.FoundryLocal => PowerToys.GPOWrapper.GPOWrapper.GetAllowedAdvancedPasteFoundryLocalValue() != PowerToys.GPOWrapper.GpoRuleConfigured.Disabled,
            _ => true,
        };
}
