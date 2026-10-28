// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.PowerToys.TextExpander.Storage;

#nullable enable

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The resolved snippet file and whether it came from a folder setting whose two supported file
/// names must continue to be re-evaluated as another app or sync service changes files.
/// </summary>
internal readonly record struct SnippetPathResolution(string FilePath, bool ResolveFolderNames)
{
    /// <summary>
    /// Selects the snippet file for the configured snippets path, by the rule the engine and editor share:
    /// trim spaces and quotes, expand environment variables, and accept only a fully qualified path. An
    /// existing file is used whatever its name; anything else is a folder resolved by <see cref="InFolder"/>.
    /// Returns null for a blank or not fully qualified value (the processes share no working directory), so the
    /// caller uses its default folder. Exceptions propagate; each caller keeps its own side effects.
    /// </summary>
    public static SnippetPathResolution? FromConfiguredPath(string? configured, Func<string, string> expandEnvironmentVariables, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        string expanded = expandEnvironmentVariables(configured.Trim().Trim('"'));
        if (!Path.IsPathFullyQualified(expanded))
        {
            return null;
        }

        string full = Path.GetFullPath(expanded);
        return fileExists(full) ? new SnippetPathResolution(full, ResolveFolderNames: false) : InFolder(full, fileExists);
    }

    /// <summary>Selects the snippet file in <paramref name="folder"/> by <see cref="TextExpanderSnippetFileNames.ResolveInFolder"/>.</summary>
    public static SnippetPathResolution InFolder(string folder, Func<string, bool> fileExists)
        => new(TextExpanderSnippetFileNames.ResolveInFolder(folder, fileExists), ResolveFolderNames: true);
}
