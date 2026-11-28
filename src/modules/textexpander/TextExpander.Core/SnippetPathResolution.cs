// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.TextExpander.Storage;

#nullable enable

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The snippets file, and whether it's in the default location: this PC's own folder, which is created when it's
/// missing, as is the file, with the starter. A file the user chose is read where it is, and nothing is ever created
/// there: one that's gone, or on a drive or in a sync service that isn't ready yet, is reported instead.
/// </summary>
internal readonly record struct SnippetPathResolution(string FilePath, bool IsDefault)
{
    // BugReportTool's TextExpanderPrivacy.h recognizes the same encoding.
    private const string LiteralPathPrefix = "literal:\"";

    /// <summary>Encodes an absolute file picker path when a pair of percent signs could be expanded on read.</summary>
    public static string EncodePickedPath(string filePath)
    {
        int openingPercent = filePath.IndexOf('%');
        return openingPercent >= 0 && filePath.IndexOf('%', openingPercent + 1) >= 0
            ? $"{LiteralPathPrefix}{filePath}\""
            : filePath;
    }

    /// <summary>
    /// Selects the snippets file for the configured snippets path. Decode literal selections as written. Otherwise, trim
    /// spaces and quotes and expand environment variables. Returns null for a blank or not fully qualified value, so the
    /// caller uses the default location. Exceptions propagate; each caller keeps its own side effects.
    /// </summary>
    public static SnippetPathResolution? FromConfiguredPath(string? configured, Func<string, string> expandEnvironmentVariables)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        string value = configured.Trim();
        string filePath = value.StartsWith(LiteralPathPrefix, StringComparison.Ordinal)
            && value.Length > LiteralPathPrefix.Length
            && value[^1] == '"'
            ? value[LiteralPathPrefix.Length..^1]
            : expandEnvironmentVariables(value.Trim('"'));
        return Path.IsPathFullyQualified(filePath)
            ? new SnippetPathResolution(Path.GetFullPath(filePath), IsDefault: false)
            : null;
    }

    /// <summary>Gets the snippets file in <paramref name="folder"/>, the default location.</summary>
    public static SnippetPathResolution Default(string folder)
        => new(Path.Combine(folder, TextExpanderSnippetFileNames.Current), IsDefault: true);
}
