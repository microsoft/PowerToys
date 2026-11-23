// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.PreviewPane.UITests;

internal static class ThumbnailProviderLog
{
    internal static bool HasGenerationFailure(string logText, string providerName)
    {
        var providerPrefix = $"Thumbnail provider PowerToys.{providerName}.exe";
        return logText.Contains("Failed to copy thumbnail input.", StringComparison.OrdinalIgnoreCase) ||
               logText.Contains($"{providerPrefix} did not complete.", StringComparison.OrdinalIgnoreCase) ||
               logText.Contains($"{providerPrefix} exited with code ", StringComparison.OrdinalIgnoreCase) ||
               logText.Contains("Thumbnail bitmap was not generated or could not be loaded.", StringComparison.OrdinalIgnoreCase) ||
               logText.Contains($"{providerPrefix} failed.", StringComparison.OrdinalIgnoreCase);
    }
}
