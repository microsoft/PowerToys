// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace PowerToys.TryRun.Core;

public static class WorkspaceSnapshot
{
    public const int PreviewBytes = 8192;

    public static IReadOnlyList<FileChange> Compare(IReadOnlyDictionary<string, WorkspaceFile> before, IReadOnlyDictionary<string, WorkspaceFile> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var original = Normalize(before);
        var result = Normalize(after);
        return original.Keys.Union(result.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).Select(path =>
        {
            original.TryGetValue(path, out var left);
            result.TryGetValue(path, out var right);
            var kind = left is null ? FileChangeKind.Added : right is null ? FileChangeKind.Deleted : left.Hash == right.Hash && left.Size == right.Size ? FileChangeKind.Unchanged : FileChangeKind.Modified;
            return new FileChange(path, kind, left, right);
        }).ToArray();
    }

    public static string FormatPreview(ReadOnlySpan<byte> bytes, bool truncated)
    {
        truncated |= bytes.Length > PreviewBytes;
        bytes = bytes[..Math.Min(bytes.Length, PreviewBytes)];
        try
        {
            Encoding encoding = new UTF8Encoding(false, true);
            if (bytes.StartsWith(new byte[] { 0xff, 0xfe }))
            {
                encoding = new UnicodeEncoding(false, true, true);
                bytes = bytes[2..];
            }
            else if (bytes.StartsWith(new byte[] { 0xfe, 0xff }))
            {
                encoding = new UnicodeEncoding(true, true, true);
                bytes = bytes[2..];
            }
            else if (bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }))
            {
                bytes = bytes[3..];
            }

            // A bounded preview can end in the middle of a code point.
            var characters = new char[PreviewBytes];
            var count = encoding.GetDecoder().GetChars(bytes, characters, flush: !truncated);
            var text = new string(characters, 0, count);
            if (text.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t'))
            {
                return "Binary file — text preview unavailable.";
            }

            return text + (truncated ? "\n[Preview limited to the first 8 KiB.]" : string.Empty);
        }
        catch (DecoderFallbackException)
        {
            return "Binary file or unsupported encoding — text preview unavailable.";
        }
    }

    private static Dictionary<string, WorkspaceFile> Normalize(IReadOnlyDictionary<string, WorkspaceFile> files)
    {
        if (files.Count > WorkspacePath.MaximumEntries)
        {
            throw new ArgumentException("A workspace may contain at most 1,000 files and folders.");
        }

        var result = new Dictionary<string, WorkspaceFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in files)
        {
            result.Add(WorkspacePath.ValidateRelative(pair.Key), pair.Value);
        }

        return result;
    }
}
