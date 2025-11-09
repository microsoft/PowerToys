// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerToys.Settings.UI.Library;

/// <summary>
/// Centralized registry for AI service type metadata.
/// </summary>
public static class AIServiceTypeRegistry
{
    private static readonly Dictionary<AIServiceType, AIServiceTypeMetadata> MetadataMap = new()
    {
        [AIServiceType.AmazonBedrock] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.AmazonBedrock,
            DisplayName = "Amazon Bedrock",
            IsAvailableInUI = false, // Currently disabled in UI
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/Bedrock.svg",
            IsOnlineService = true,
            LegalDescription = "AdvancedPaste_AmazonBedrock_LegalDescription",
            TermsLabel = "AdvancedPaste_AmazonBedrock_TermsLabel",
            TermsUri = new Uri("https://aws.amazon.com/service-terms/"),
            PrivacyLabel = "AdvancedPaste_AmazonBedrock_PrivacyLabel",
            PrivacyUri = new Uri("https://aws.amazon.com/privacy/"),
        },
        [AIServiceType.Anthropic] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Anthropic,
            DisplayName = "Anthropic",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/Anthropic.svg",
            IsOnlineService = true,
            LegalDescription = "AdvancedPaste_Anthropic_LegalDescription",
            TermsLabel = "AdvancedPaste_Anthropic_TermsLabel",
            TermsUri = new Uri("https://privacy.claude.com/en/collections/10672567-policies-terms-of-service"),
            PrivacyLabel = "AdvancedPaste_Anthropic_PrivacyLabel",
            PrivacyUri = new Uri("https://privacy.claude.com/en/"),
        },
        [AIServiceType.AzureAIInference] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.AzureAIInference,
            DisplayName = "Azure AI Inference",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/FoundryLocal.svg", // No icon for Azure AI Inference, use Foundry Local temporarily
            IsOnlineService = true,
            LegalDescription = "AdvancedPaste_AzureAIInference_LegalDescription",
            TermsLabel = "AdvancedPaste_AzureAIInference_TermsLabel",
            TermsUri = new Uri("https://azure.microsoft.com/support/legal/"),
            PrivacyLabel = "AdvancedPaste_AzureAIInference_PrivacyLabel",
            PrivacyUri = new Uri("https://privacy.microsoft.com/privacystatement"),
        },
        [AIServiceType.AzureOpenAI] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.AzureOpenAI,
            DisplayName = "Azure OpenAI",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/AzureAI.svg",
            IsOnlineService = true,
            LegalDescription = "AdvancedPaste_AzureOpenAI_LegalDescription",
            TermsLabel = "AdvancedPaste_AzureOpenAI_TermsLabel",
            TermsUri = new Uri("https://azure.microsoft.com/support/legal/"),
            PrivacyLabel = "AdvancedPaste_AzureOpenAI_PrivacyLabel",
            PrivacyUri = new Uri("https://privacy.microsoft.com/privacystatement"),
        },
        [AIServiceType.FoundryLocal] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.FoundryLocal,
            DisplayName = "Foundry Local",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/FoundryLocal.svg",
            IsOnlineService = false,
            IsLocalModel = true,
            LegalDescription = "AdvancedPaste_FoundryLocal_LegalDescription", // Resource key for localized description
        },
        [AIServiceType.Google] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Google,
            DisplayName = "Google",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/Gemini.svg",
            IsOnlineService = true,
            LegalDescription = "AdvancedPaste_Google_LegalDescription",
            TermsLabel = "AdvancedPaste_Google_TermsLabel",
            TermsUri = new Uri("https://ai.google.dev/gemini-api/terms"),
            PrivacyLabel = "AdvancedPaste_Google_PrivacyLabel",
            PrivacyUri = new Uri("https://support.google.com/gemini/answer/13594961"),
        },
        [AIServiceType.HuggingFace] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.HuggingFace,
            DisplayName = "Hugging Face",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/HuggingFace.svg",
            IsOnlineService = true,
            IsAvailableInUI = false, // Currently disabled in UI
        },
        [AIServiceType.Mistral] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Mistral,
            DisplayName = "Mistral",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/Mistral.svg",
            IsOnlineService = true,
            LegalDescription = "AdvancedPaste_Mistral_LegalDescription",
            TermsLabel = "AdvancedPaste_Mistral_TermsLabel",
            TermsUri = new Uri("https://mistral.ai/terms-of-service/"),
            PrivacyLabel = "AdvancedPaste_Mistral_PrivacyLabel",
            PrivacyUri = new Uri("https://mistral.ai/privacy-policy/"),
        },
        [AIServiceType.ML] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.ML,
            DisplayName = "Windows ML",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/WindowsML.svg",
            LegalDescription = "AdvancedPaste_LocalModel_LegalDescription",
            IsAvailableInUI = false,
            IsOnlineService = false,
            IsLocalModel = true,
        },
        [AIServiceType.Ollama] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Ollama,
            DisplayName = "Ollama",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/Ollama.svg",

            // Ollama provide online service, but we treat it as local model at first version since it can is known for local model.
            IsOnlineService = false,
            IsLocalModel = true,
            LegalDescription = "AdvancedPaste_LocalModel_LegalDescription",
            TermsLabel = "AdvancedPaste_Ollama_TermsLabel",
            TermsUri = new Uri("https://ollama.org/terms"),
            PrivacyLabel = "AdvancedPaste_Ollama_PrivacyLabel",
            PrivacyUri = new Uri("https://ollama.org/privacy"),
        },
        [AIServiceType.Onnx] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Onnx,
            DisplayName = "ONNX",
            LegalDescription = "AdvancedPaste_LocalModel_LegalDescription",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/Onnx.svg",
            IsOnlineService = false,
            IsAvailableInUI = false,
        },
        [AIServiceType.OpenAI] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.OpenAI,
            DisplayName = "OpenAI",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/OpenAI.light.svg",
            IsOnlineService = true,
            LegalDescription = "AdvancedPaste_OpenAI_LegalDescription",
            TermsLabel = "AdvancedPaste_OpenAI_TermsLabel",
            TermsUri = new Uri("https://openai.com/terms"),
            PrivacyLabel = "AdvancedPaste_OpenAI_PrivacyLabel",
            PrivacyUri = new Uri("https://openai.com/privacy"),
        },
        [AIServiceType.Unknown] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Unknown,
            DisplayName = "Unknown",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/OpenAI.light.svg",
            IsOnlineService = false,
            IsAvailableInUI = false,
        },
    };

    /// <summary>
    /// Get metadata for a specific service type.
    /// </summary>
    public static AIServiceTypeMetadata GetMetadata(AIServiceType serviceType)
    {
        return MetadataMap.TryGetValue(serviceType, out var metadata)
            ? metadata
            : MetadataMap[AIServiceType.Unknown];
    }

    /// <summary>
    /// Get metadata for a service type from its string representation.
    /// </summary>
    public static AIServiceTypeMetadata GetMetadata(string serviceType)
    {
        var type = serviceType.ToAIServiceType();
        return GetMetadata(type);
    }

    /// <summary>
    /// Get icon path for a service type.
    /// </summary>
    public static string GetIconPath(AIServiceType serviceType)
    {
        return GetMetadata(serviceType).IconPath;
    }

    /// <summary>
    /// Get icon path for a service type from its string representation.
    /// </summary>
    public static string GetIconPath(string serviceType)
    {
        return GetMetadata(serviceType).IconPath;
    }

    /// <summary>
    /// Get all service types available in the UI.
    /// </summary>
    public static IEnumerable<AIServiceTypeMetadata> GetAvailableServiceTypes()
    {
        return MetadataMap.Values.Where(m => m.IsAvailableInUI);
    }

    /// <summary>
    /// Get all online service types available in the UI.
    /// </summary>
    public static IEnumerable<AIServiceTypeMetadata> GetOnlineServiceTypes()
    {
        return GetAvailableServiceTypes().Where(m => m.IsOnlineService);
    }

    /// <summary>
    /// Get all local service types available in the UI.
    /// </summary>
    public static IEnumerable<AIServiceTypeMetadata> GetLocalServiceTypes()
    {
        return GetAvailableServiceTypes().Where(m => m.IsLocalModel);
    }
}
