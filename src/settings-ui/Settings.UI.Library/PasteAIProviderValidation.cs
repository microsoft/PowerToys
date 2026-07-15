// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.Library;

/// <summary>
/// Validation helpers for Paste AI provider settings.
/// </summary>
public static class PasteAIProviderValidation
{
    /// <summary>
    /// Validates an OpenAI-compatible model name.
    /// </summary>
    public static bool IsValidOpenAICompatibleModelName(string modelName)
    {
        return !string.IsNullOrWhiteSpace(modelName);
    }

    /// <summary>
    /// Validates and parses an OpenAI-compatible HTTP or HTTPS endpoint.
    /// </summary>
    public static bool TryGetOpenAICompatibleEndpoint(string endpoint, out Uri endpointUri)
    {
        return Uri.TryCreate(endpoint?.Trim(), UriKind.Absolute, out endpointUri)
            && (endpointUri.Scheme == Uri.UriSchemeHttp || endpointUri.Scheme == Uri.UriSchemeHttps);
    }
}
