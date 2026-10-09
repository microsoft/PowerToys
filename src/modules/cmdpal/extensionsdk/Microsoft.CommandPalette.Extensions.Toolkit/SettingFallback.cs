// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Builds the <c>fallback</c> content emitted alongside Command Palette's custom card inputs, so
/// a host that does not recognize those element types can render something instead of dropping the
/// setting silently.
/// </summary>
/// <remarks>
/// AdaptiveCards.Rendering.WinUI3 2.2.4-beta does not act on this: XamlBuilder::RenderFallback
/// resolves the substitute control and then returns a default-constructed result, so the caller
/// drops it. The content is emitted anyway because it is correct per the Adaptive Cards schema and
/// costs nothing until the renderer is fixed, at which point already-published cards start
/// degrading properly with no change on either side.
/// </remarks>
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
        { "text", NoticeText(label) },
        { "wrap", true },
    };

    private static string NoticeText(string label) =>
        string.IsNullOrEmpty(label)
            ? Properties.Resources.Setting_UnsupportedByHostNoLabel
            : string.Format(CultureInfo.CurrentCulture, _unsupportedFormat, label);
}
