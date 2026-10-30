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
    /// <summary>
    /// Selects the snippets file for the configured snippets path, by the rule the engine and editor share: trim spaces
    /// and quotes, expand environment variables, and accept only a fully qualified path, which is the file, whatever its
    /// name. Returns null for a blank or not fully qualified value (the processes share no working directory), so the
    /// caller uses the default location. Exceptions propagate; each caller keeps its own side effects.
    /// </summary>
    public static SnippetPathResolution? FromConfiguredPath(string? configured, Func<string, string> expandEnvironmentVariables)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        string expanded = expandEnvironmentVariables(configured.Trim().Trim('"'));
        return Path.IsPathFullyQualified(expanded)
            ? new SnippetPathResolution(Path.GetFullPath(expanded), IsDefault: false)
            : null;
    }

    /// <summary>Gets the snippets file in <paramref name="folder"/>, the default location.</summary>
    public static SnippetPathResolution Default(string folder)
        => new(Path.Combine(folder, TextExpanderSnippetFileNames.Current), IsDefault: true);
}
