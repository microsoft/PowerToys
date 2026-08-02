// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Builds the <c>fallback</c> content emitted alongside Command Palette's custom card inputs.
/// A host that does not recognize those element types renders the fallback instead of dropping
/// the setting silently.
/// </summary>
internal static class SettingFallback
{
    private static readonly CompositeFormat _unsupportedFormat =
        CompositeFormat.Parse(Properties.Resources.Setting_UnsupportedByHost);

    /// <summary>
    /// A read-only notice naming the setting. It is a <c>TextBlock</c> rather than an input, so a
    /// host that renders it submits nothing for this key and cannot overwrite the stored value.
    /// </summary>
    public static Dictionary<string, object> Notice(string label) => new()
    {
        { "type", "TextBlock" },
        {
            "text",
            string.IsNullOrEmpty(label)
                ? Properties.Resources.Setting_UnsupportedByHostNoLabel
                : string.Format(CultureInfo.CurrentCulture, _unsupportedFormat, label)
        },
        { "wrap", true },
    };
}
