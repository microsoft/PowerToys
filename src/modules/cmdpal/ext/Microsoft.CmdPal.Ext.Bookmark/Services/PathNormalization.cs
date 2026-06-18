// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Microsoft.CmdPal.Ext.Bookmarks.Services;

/// <summary>
/// Conservative, lossless normalization helpers for filesystem paths used by the
/// Bookmark resolver when probing on Windows.
/// </summary>
/// <remarks>
/// <para>
/// On Windows, the goal is to give file/directory probes a fair chance at hitting
/// an existing target without ever mangling the user's data. We:
/// </para>
/// <list type="bullet">
///   <item><description>Apply Unicode Normalization Form C (NFC) so visually equivalent
///     accented sequences (e.g. precomposed "É" vs. "E + COMBINING ACUTE ACCENT")
///     compare equal to the way Windows tools typically write them to NTFS.</description></item>
///   <item><description>Strip well-known long-path / NT-object prefixes (<c>\\?\</c>,
///     <c>\\?\UNC\</c>, <c>\??\</c>) before probing because <see cref="System.IO.File.Exists(string)"/>
///     and friends accept the un-prefixed form and many user-entered paths do not include the prefix.</description></item>
///   <item><description>Trim trailing ASCII whitespace from the whole path because Win32
///     ignores trailing spaces in filenames anyway and copy/paste from the web frequently
///     leaves a stray space or NBSP behind. Trailing dots are deliberately preserved
///     because removing them can be lossy.</description></item>
/// </list>
/// <para>
/// All transformations are length-preserving for the meaningful portion of the
/// string and never drop or substitute non-ASCII characters. On non-Windows
/// platforms only NFC normalization is applied.
/// </para>
/// </remarks>
internal static class PathNormalization
{
    private const string LongPathPrefix = @"\\?\";
    private const string LongUncPrefix = @"\\?\UNC\";
    private const string NtObjectPrefix = @"\??\";

    /// <summary>
    /// Returns a normalized form of <paramref name="path"/> that is safe to feed into
    /// .NET filesystem probes (<see cref="System.IO.File.Exists(string)"/>,
    /// <see cref="System.IO.Directory.Exists(string)"/>, <see cref="System.IO.Path.GetFullPath(string)"/>).
    /// The function is conservative: it never drops or substitutes non-ASCII characters
    /// and returns the input unchanged when no normalization is needed.
    /// </summary>
    public static string NormalizePathForWindows(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        // NFC is safe everywhere. It compares equal byte-for-byte to most
        // Windows-authored filenames and never loses characters.
        var normalized = path.Normalize(NormalizationForm.FormC);

        // Trim only trailing whitespace. Windows ignores trailing spaces in
        // file/dir names, and copy/paste often leaves a stray space (or NBSP)
        // behind. We do NOT trim trailing dots — that would be lossy.
        normalized = TrimTrailingWhitespace(normalized);

        if (!OperatingSystem.IsWindows())
        {
            return normalized;
        }

        // Strip long-path / NT object prefixes. .NET's filesystem APIs accept
        // both prefixed and un-prefixed forms; strings stored in a bookmark
        // typically lack the prefix so we normalize toward the bare form.
        if (normalized.StartsWith(LongUncPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + normalized[LongUncPrefix.Length..];
        }

        if (normalized.StartsWith(LongPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return normalized[LongPathPrefix.Length..];
        }

        if (normalized.StartsWith(NtObjectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return normalized[NtObjectPrefix.Length..];
        }

        return normalized;
    }

    private static string TrimTrailingWhitespace(string value)
    {
        var end = value.Length;
        while (end > 0 && char.IsWhiteSpace(value[end - 1]))
        {
            end--;
        }

        return end == value.Length ? value : value[..end];
    }
}
