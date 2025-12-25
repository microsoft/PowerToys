// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Supported AI service types for PowerToys AI experiences.
    /// </summary>
    public enum AIServiceType
    {
        Unknown = 0,
        OpenAI,
        AzureOpenAI,
        Onnx,
        ML,
        FoundryLocal,
        Mistral,
        Google,
        AzureAIInference,
        Ollama,
    }
}
