// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace LanguageModelProvider;

internal static class AppUtils
{
    public static string GetThemeAssetSuffix()
    {
        // Default suffix for assets that are theme-agnostic today.
        return string.Empty;
    }
}
