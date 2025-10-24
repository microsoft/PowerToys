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
            IconPath = "ms-appx:///Assets/AdvancedPaste/OpenAI.light.svg",
            IsOnlineService = true,
        },
        [AIServiceType.AzureOpenAI] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.AzureOpenAI,
            DisplayName = "Azure OpenAI",
            IconPath = "ms-appx:///Assets/AdvancedPaste/AzureAI.svg",
            IsOnlineService = true,
        },
        [AIServiceType.Mistral] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Mistral,
            DisplayName = "Mistral",
            IconPath = "ms-appx:///Assets/AdvancedPaste/Mistral.svg",
            IsOnlineService = true,
        },
        [AIServiceType.Google] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Google,
            DisplayName = "Google",
            IconPath = "ms-appx:///Assets/AdvancedPaste/Gemini.svg",
            IsOnlineService = true,
        },
        [AIServiceType.AzureAIInference] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.AzureAIInference,
            DisplayName = "Azure AI Inference",
            IconPath = "ms-appx:///Assets/AdvancedPaste/AzureAI.svg",
            IsOnlineService = true,
        },
        [AIServiceType.Ollama] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Ollama,
            DisplayName = "Ollama",
            IconPath = "ms-appx:///Assets/AdvancedPaste/Ollama.svg",
            IsOnlineService = true,
            IsLocalModel = true,
        },
        [AIServiceType.Anthropic] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Anthropic,
            DisplayName = "Anthropic",
            IconPath = "ms-appx:///Assets/AdvancedPaste/Anthropic.svg",
            IsOnlineService = true,
        },
        [AIServiceType.AmazonBedrock] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.AmazonBedrock,
            DisplayName = "Amazon Bedrock",
            IconPath = "ms-appx:///Assets/AdvancedPaste/Bedrock.svg",
            IsOnlineService = true,
        },
        [AIServiceType.FoundryLocal] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.FoundryLocal,
            DisplayName = "Foundry Local",
            IconPath = "ms-appx:///Assets/AdvancedPaste/FoundryLocal.svg",
            IsOnlineService = false,
            IsLocalModel = true,
        },
        [AIServiceType.ML] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.ML,
            DisplayName = "Windows ML",
            IconPath = "ms-appx:///Assets/AdvancedPaste/WindowsML.svg",
            IsOnlineService = false,
        },
        [AIServiceType.HuggingFace] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.HuggingFace,
            DisplayName = "Hugging Face",
            IconPath = "ms-appx:///Assets/AdvancedPaste/HuggingFace.svg",
            IsOnlineService = true,
            IsAvailableInUI = false, // Currently disabled in UI
        },
        [AIServiceType.Onnx] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Onnx,
            DisplayName = "ONNX",
            IconPath = "ms-appx:///Assets/AdvancedPaste/Onnx.svg",
            IsOnlineService = false,
            IsAvailableInUI = false,
        },
        [AIServiceType.Unknown] = new AIServiceTypeMetadata
        {
            ServiceType = AIServiceType.Unknown,
            DisplayName = "Unknown",
            IconPath = "ms-appx:///Assets/AdvancedPaste/OpenAI.light.svg",
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
        return GetAvailableServiceTypes().Where(m => !m.IsOnlineService);
    }
}
