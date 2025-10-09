// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public static class AIServiceTypeExtensions
    {
        /// <summary>
        /// Convert a persisted string value into an <see cref="AIServiceType"/>.
        /// Supports historical casing and aliases.
        /// </summary>
        public static AIServiceType ToAIServiceType(this string serviceType)
        {
            if (string.IsNullOrWhiteSpace(serviceType))
            {
                return AIServiceType.OpenAI;
            }

            var normalized = serviceType.Trim().ToLowerInvariant();
            return normalized switch
            {
                "openai" => AIServiceType.OpenAI,
                "azureopenai" or "azure" => AIServiceType.AzureOpenAI,
                "onnx" => AIServiceType.Onnx,
                _ => AIServiceType.Unknown,
            };
        }

        /// <summary>
        /// Convert an <see cref="AIServiceType"/> to the canonical string used for persistence.
        /// </summary>
        public static string ToConfigurationString(this AIServiceType serviceType)
        {
            return serviceType switch
            {
                AIServiceType.OpenAI => "OpenAI",
                AIServiceType.AzureOpenAI => "AzureOpenAI",
                AIServiceType.Onnx => "Onnx",
                AIServiceType.Unknown => string.Empty,
                _ => throw new ArgumentOutOfRangeException(nameof(serviceType), serviceType, "Unsupported AI service type."),
            };
        }

        /// <summary>
        /// Convert an <see cref="AIServiceType"/> into the normalized key used internally.
        /// </summary>
        public static string ToNormalizedKey(this AIServiceType serviceType)
        {
            return serviceType switch
            {
                AIServiceType.OpenAI => "openai",
                AIServiceType.AzureOpenAI => "azureopenai",
                AIServiceType.Onnx => "onnx",
                _ => string.Empty,
            };
        }
    }
}
