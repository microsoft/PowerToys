// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>What reading the snippets file found.</summary>
internal enum SnippetLibraryStatus
{
    Ready,

    /// <summary>The file has no snippets, or Text Expander hasn't created its default file yet.</summary>
    NoSnippetsYet,

    /// <summary>The file the user chose for their snippets isn't there.</summary>
    FileNotFound,

    /// <summary>The file is there but couldn't be read, such as on a drive that isn't ready.</summary>
    Unreadable,

    /// <summary>The file isn't JSON, or has no snippets list.</summary>
    NotSnippetsFile,
}

/// <summary>The snippets file as read at one moment. It doesn't change: a new read makes a new snapshot.</summary>
internal sealed class SnippetLibrarySnapshot
{
    public static SnippetLibrarySnapshot Unavailable { get; } = new(SnippetLibraryStatus.NoSnippetsYet, string.Empty, []);

    public SnippetLibrarySnapshot(SnippetLibraryStatus status, string filePath, IReadOnlyList<Snippet> snippets, long? errorLine = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(snippets);

        Status = status;
        FilePath = filePath;
        Snippets = snippets;
        ErrorLine = errorLine;
    }

    public SnippetLibraryStatus Status { get; }

    public string FilePath { get; }

    /// <summary>Gets the snippets Text Expander would expand, in file order. Empty unless <see cref="Status"/> is Ready.</summary>
    public IReadOnlyList<Snippet> Snippets { get; }

    /// <summary>Gets the line, counting from 1, where a file that isn't JSON goes wrong.</summary>
    public long? ErrorLine { get; }
}
