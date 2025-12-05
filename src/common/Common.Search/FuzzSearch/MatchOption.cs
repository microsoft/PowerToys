// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Common.Search.FuzzSearch;

public class MatchOption
{
    public bool IgnoreCase { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to support Chinese PinYin.
    /// Defaults to true when the system UI culture is Simplified Chinese.
    /// </summary>
    public bool ChinesePinYinSupport { get; set; } = IsSimplifiedChinese();

    private static bool IsSimplifiedChinese()
    {
        var culture = CultureInfo.CurrentUICulture;
        // Detect Simplified Chinese: zh-CN, zh-Hans, zh-Hans-*
        return culture.Name.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase)
            || culture.Name.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase);
    }
}
