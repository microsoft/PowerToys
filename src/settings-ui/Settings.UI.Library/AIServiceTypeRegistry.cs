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
        [AIServiceType.OpenAI] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.OpenAI,
            DisplayName = "OpenAI",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/OpenAI.light.svg",
            IsOnlineService = true,
            LegalDescription = "Your API key connects directly to OpenAI services. By configuring this provider you agree to follow OpenAI usage policies and data handling rules.",
            TermsLabel = "Terms of Use",
            TermsUri = new Uri("https://openai.com/terms"),
            PrivacyLabel = "Privacy Policy",
            PrivacyUri = new Uri("https://openai.com/privacy"),
        },
        [AIServiceType.AzureOpenAI] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.AzureOpenAI,
            DisplayName = "Azure OpenAI",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/AzureAI.svg",
            IsOnlineService = true,
            LegalDescription = "This connector routes requests to OpenAI models hosted on Microsoft Azure. Saving this configuration means you accept Microsoft terms and data protections.",
            TermsLabel = "Microsoft Azure Terms of Service",
            TermsUri = new Uri("https://azure.microsoft.com/support/legal/"),
            PrivacyLabel = "Microsoft Privacy Statement",
            PrivacyUri = new Uri("https://privacy.microsoft.com/privacystatement"),
        },
        [AIServiceType.Mistral] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Mistral,
            DisplayName = "Mistral",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/Mistral.svg",
            IsOnlineService = true,
            LegalDescription = "You can connect with a personal Mistral API key. Configuring this provider indicates you accept Mistral's published legal terms.",
            TermsLabel = "Mistral Terms of Use",
            TermsUri = new Uri("https://mistral.ai/terms-of-service/"),
            PrivacyLabel = "Mistral Privacy Policy",
            PrivacyUri = new Uri("https://mistral.ai/privacy-policy/"),
        },
        [AIServiceType.Google] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Google,
            DisplayName = "Google",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/Gemini.svg",
            IsOnlineService = true,
            LegalDescription = "Connecting to Gemini requires a Google API key. Using this integration means you agree to Google's general terms and privacy policies.",
            TermsLabel = "Google Terms of Service",
            TermsUri = new Uri("https://policies.google.com/terms"),
            PrivacyLabel = "Google Privacy Policy",
            PrivacyUri = new Uri("https://policies.google.com/privacy"),
        },
        [AIServiceType.AzureAIInference] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.AzureAIInference,
            DisplayName = "Azure AI Inference",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/AzureAI.svg",
            IsOnlineService = true,
            LegalDescription = "Azure AI Inference is governed by Microsoft service and privacy commitments. Continuing indicates you accept Microsoft terms for this offering.",
            TermsLabel = "Microsoft Azure Terms of Service",
            TermsUri = new Uri("https://azure.microsoft.com/support/legal/"),
            PrivacyLabel = "Microsoft Privacy Statement",
            PrivacyUri = new Uri("https://privacy.microsoft.com/privacystatement"),
        },
        [AIServiceType.Ollama] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Ollama,
            DisplayName = "Ollama",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/Ollama.svg",

            // Olllama provide online service, but we treat it as local model at first version since it can is known for local model.
            IsOnlineService = false,
            IsLocalModel = true,
            LegalDescription = "Ollama usage, local or remote, is bound by its license and usage policies. Continuing means you accept Ollama's terms and privacy commitments.",
            TermsLabel = "Ollama Terms of Service",
            TermsUri = new Uri("https://ollama.com/terms"),
            PrivacyLabel = "Ollama Privacy Policy",
            PrivacyUri = new Uri("https://ollama.com/privacy"),
        },
        [AIServiceType.Anthropic] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Anthropic,
            DisplayName = "Anthropic",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/Anthropic.svg",
            IsOnlineService = true,
            LegalDescription = "This integration accesses Anthropic Claude models. You are responsible for complying with Anthropic policies whenever you use this provider.",
            TermsLabel = "Anthropic Terms of Service",
            TermsUri = new Uri("https://www.anthropic.com/legal/terms-of-service"),
            PrivacyLabel = "Anthropic Privacy Policy",
            PrivacyUri = new Uri("https://www.anthropic.com/legal/privacy"),
        },
        [AIServiceType.AmazonBedrock] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.AmazonBedrock,
            DisplayName = "Amazon Bedrock",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/Bedrock.svg",
            IsOnlineService = true,
            LegalDescription = "AWS credentials let you invoke Amazon Bedrock models. Saving this setup confirms you will follow AWS service terms and privacy commitments.",
            TermsLabel = "AWS Service Terms",
            TermsUri = new Uri("https://aws.amazon.com/service-terms/"),
            PrivacyLabel = "AWS Privacy Notice",
            PrivacyUri = new Uri("https://aws.amazon.com/privacy/"),
        },
        [AIServiceType.FoundryLocal] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.FoundryLocal,
            DisplayName = "Foundry Local",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/FoundryLocal.svg",
            IsOnlineService = false,
            IsLocalModel = true,
        },
        [AIServiceType.ML] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.ML,
            DisplayName = "Windows ML",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/WindowsML.svg",
            IsAvailableInUI = false,
            IsOnlineService = false,
            IsLocalModel = true,
        },
        [AIServiceType.HuggingFace] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.HuggingFace,
            DisplayName = "Hugging Face",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/HuggingFace.svg",
            IsOnlineService = true,
            IsAvailableInUI = false, // Currently disabled in UI
        },
        [AIServiceType.Onnx] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Onnx,
            DisplayName = "ONNX",
            IconPath = "ms-appx:///Assets/Settings/Icons/Models/Onnx.svg",
            IsOnlineService = false,
            IsAvailableInUI = false,
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
