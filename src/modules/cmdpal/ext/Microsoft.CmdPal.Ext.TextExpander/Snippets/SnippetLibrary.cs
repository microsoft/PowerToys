// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Threading;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>
/// Reads Text Expander's snippets file with <see cref="SnippetFile"/>, the reader Text Expander itself uses, and keeps
/// the result until the file changes. It never creates or writes the file.
/// </summary>
internal sealed class SnippetLibrary
{
    private readonly Lock _lock = new();
    private Loaded? _loaded;

    /// <summary>
    /// Gets the snippets in <paramref name="filePath"/>, reading the file again only when its size or write time
    /// changed. <paramref name="isDefault"/> says it's Text Expander's own file, which it creates when it first runs:
    /// until then there are no snippets yet, rather than a missing file.
    /// </summary>
    public SnippetLibrarySnapshot Load(string filePath, bool isDefault, bool caseSensitive)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var stamp = FileStamp.Of(filePath);
        lock (_lock)
        {
            if (_loaded is { } loaded
                && loaded.Stamp == stamp
                && loaded.IsDefault == isDefault
                && loaded.CaseSensitive == caseSensitive
                && string.Equals(loaded.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                return loaded.Snapshot;
            }
        }

        var snapshot = Read(filePath, isDefault, caseSensitive, stamp);

        // A file that couldn't be read is tried again next time: a save in progress may have locked it.
        if (snapshot.Status != SnippetLibraryStatus.Unreadable)
        {
            lock (_lock)
            {
                _loaded = new Loaded(filePath, isDefault, caseSensitive, stamp, snapshot);
            }
        }

        return snapshot;
    }

    /// <summary>
    /// Keeps the snippets Text Expander would expand, in file order: the last of two with the same trigger wins, as in
    /// Text Expander, and a trigger that can't be typed is left out.
    /// </summary>
    internal static IReadOnlyList<Snippet> Effective(IReadOnlyList<SnippetFile.Entry> entries, bool caseSensitive)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var lastIndex = new Dictionary<string, int>(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < entries.Count; i++)
        {
            if (SnippetFile.IsValidTrigger(entries[i].Trigger, out _))
            {
                lastIndex[entries[i].Trigger] = i;
            }
        }

        var snippets = new List<Snippet>(lastIndex.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (lastIndex.TryGetValue(entry.Trigger, out var last) && last == i)
            {
                snippets.Add(new Snippet(entry.Trigger, entry.Replacement));
            }
        }

        return snippets;
    }

    private static SnippetLibrarySnapshot Read(string filePath, bool isDefault, bool caseSensitive, FileStamp stamp)
    {
        if (stamp.Unreadable)
        {
            return new SnippetLibrarySnapshot(SnippetLibraryStatus.Unreadable, filePath, []);
        }

        if (!stamp.Exists)
        {
            return Missing(filePath, isDefault);
        }

        byte[] bytes;
        try
        {
            // Shared so that the snippet editor, a sync service or a text editor can save while it's read.
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            bytes = memory.ToArray();
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Missing(filePath, isDefault);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or ArgumentException)
        {
            return new SnippetLibrarySnapshot(SnippetLibraryStatus.Unreadable, filePath, []);
        }

        SnippetFile file;
        try
        {
            file = SnippetFile.Read(bytes);
        }
        catch (SnippetFileFormatException ex)
        {
            return new SnippetLibrarySnapshot(SnippetLibraryStatus.NotSnippetsFile, filePath, [], ex.LineNumber);
        }

        var snippets = Effective(file.Snippets, caseSensitive);
        return new SnippetLibrarySnapshot(snippets.Count > 0 ? SnippetLibraryStatus.Ready : SnippetLibraryStatus.NoSnippetsYet, filePath, snippets);
    }

    private static SnippetLibrarySnapshot Missing(string filePath, bool isDefault)
        => new(isDefault ? SnippetLibraryStatus.NoSnippetsYet : SnippetLibraryStatus.FileNotFound, filePath, []);

    private sealed record Loaded(string FilePath, bool IsDefault, bool CaseSensitive, FileStamp Stamp, SnippetLibrarySnapshot Snapshot);

    /// <summary>What says the file changed, short of reading it.</summary>
    private readonly record struct FileStamp(bool Exists, long Length, DateTime LastWriteTimeUtc, bool Unreadable)
    {
        public static FileStamp Of(string path)
        {
            try
            {
                var info = new FileInfo(path);
                return info.Exists
                    ? new FileStamp(true, info.Length, info.LastWriteTimeUtc, false)
                    : default;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or ArgumentException)
            {
                return new FileStamp(false, 0, default, true);
            }
        }
    }
}
