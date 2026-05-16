// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;

namespace Microsoft.CmdPal.UI.ViewModels.Helpers;

/// <summary>
/// Provides helper methods for sanitizing text before passing it to the WinUI MarkdownTextBlock
/// control, which wraps RichTextBlock internally. Certain control characters can trigger a native
/// crash (access violation) in RichTextBlock's text-selection code path when the user double-clicks
/// to select a word.
/// </summary>
/// <remarks>
/// See <see href="https://github.com/microsoft/microsoft-ui-xaml/issues/7299"/> for the upstream
/// WinUI bug. This is a best-effort, client-side mitigation.
/// </remarks>
internal static partial class MarkdownTextHelper
{
    // Matches characters that are known to destabilize WinUI's RichTextBlock:
    //   - C0 control codes (U+0000–U+001F) except TAB (U+0009), LF (U+000A), CR (U+000D)
    //   - DEL (U+007F)
    //   - C1 control codes (U+0080–U+009F)
    [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F\x80-\x9F]", RegexOptions.CultureInvariant)]
    private static partial Regex ProblematicControlCharsRegex();

    /// <summary>
    /// Removes control characters from <paramref name="text"/> that are known to cause crashes in
    /// the WinUI <c>RichTextBlock</c> control's double-tap word-selection code path.
    /// Standard whitespace (TAB, LF, CR) is preserved because Markdown relies on it.
    /// </summary>
    /// <param name="text">The raw markdown string, possibly <see langword="null"/>.</param>
    /// <returns>The sanitized string, or <see cref="string.Empty"/> if <paramref name="text"/> is null.</returns>
    internal static string SanitizeMarkdown(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        return ProblematicControlCharsRegex().Replace(text, string.Empty);
    }
}
