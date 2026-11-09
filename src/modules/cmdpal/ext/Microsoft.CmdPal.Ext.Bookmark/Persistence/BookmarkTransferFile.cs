// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Microsoft.CmdPal.Ext.Bookmarks.Persistence;

internal static class BookmarkTransferFile
{
    private const int MaximumFileBytes = 10 * 1024 * 1024;
    private const int MaximumBookmarks = 10000;

    public static string Read(string path)
    {
        IsJson(Path.GetExtension(path));
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumFileBytes)
        {
            throw new FormatException(Resources.bookmarks_file_too_large);
        }

        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException ex)
        {
            throw new FormatException(Resources.bookmarks_file_encoding, ex);
        }
    }

    public static List<BookmarkTransferFileEntry> Parse(string content, string extension)
    {
        if (Encoding.UTF8.GetByteCount(content) > MaximumFileBytes)
        {
            throw new FormatException(Resources.bookmarks_file_too_large);
        }

        if (content.StartsWith('\uFEFF'))
        {
            content = content[1..];
        }

        return IsJson(extension) ? JsonFormat.Parse(content) : TsvFormat.Parse(content);
    }

    public static string Serialize(IEnumerable<BookmarkData> bookmarks, string extension)
    {
        var entries = bookmarks.Select(bookmark => new BookmarkTransferFileEntry { Name = bookmark.Name, Path = bookmark.Bookmark }).ToList();
        return IsJson(extension) ? JsonFormat.Serialize(entries) : TsvFormat.Serialize(entries);
    }

    public static void Export(IReadOnlyCollection<BookmarkData> bookmarks, string path, string? storagePath = null)
    {
        if (storagePath is not null && Path.GetFullPath(path).TrimEnd(' ', '.').Equals(Path.GetFullPath(storagePath).TrimEnd(' ', '.'), StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException(Resources.bookmarks_export_active_store);
        }

        FileBookmarkDataSource.WriteAtomically(path, Serialize(bookmarks, Path.GetExtension(path)), storagePath);
    }

    private static bool IsJson(string extension) => extension.ToLowerInvariant() switch
    {
        ".json" => true,
        ".tsv" or ".txt" => false,
        _ => throw new FormatException(Resources.bookmarks_file_extension),
    };

    private static void CheckEntryCount(int count)
    {
        if (count >= MaximumBookmarks)
        {
            throw new FormatException(Resources.bookmarks_file_too_many);
        }
    }

    private static class JsonFormat
    {
        private static readonly BookmarkSerializationContext ExportContext = new(new JsonSerializerOptions(BookmarkSerializationContext.Default.Options)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        private static readonly CompositeFormat EntryErrorFormat = CompositeFormat.Parse(Resources.bookmarks_file_entry_error);

        public static List<BookmarkTransferFileEntry> Parse(string content)
        {
            try
            {
                using var document = JsonDocument.Parse(content, new JsonDocumentOptions { AllowTrailingCommas = true, MaxDepth = 16 });
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    throw new FormatException(Resources.bookmarks_file_invalid);
                }

                var entries = new List<BookmarkTransferFileEntry>();
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    CheckEntryCount(entries.Count);
                    string? path = null;
                    var name = string.Empty;
                    if (element.ValueKind != JsonValueKind.Object)
                    {
                        throw EntryError(entries.Count, Resources.bookmarks_file_invalid);
                    }

                    try
                    {
                        var properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var property in element.EnumerateObject())
                        {
                            if (!properties.Add(property.Name))
                            {
                                throw EntryError(entries.Count, Resources.bookmarks_file_duplicate_property);
                            }

                            if (property.Name.Equals("Path", StringComparison.OrdinalIgnoreCase) || property.Name.Equals("Name", StringComparison.OrdinalIgnoreCase))
                            {
                                if (property.Value.ValueKind != JsonValueKind.String)
                                {
                                    throw EntryError(entries.Count, Resources.bookmarks_file_string_fields);
                                }

                                if (property.Name.Equals("Path", StringComparison.OrdinalIgnoreCase))
                                {
                                    path = property.Value.GetString();
                                }
                                else
                                {
                                    name = property.Value.GetString()!;
                                }
                            }
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw EntryError(entries.Count, ex.Message);
                    }

                    if (string.IsNullOrWhiteSpace(path))
                    {
                        throw EntryError(entries.Count, Resources.bookmarks_file_empty_path);
                    }

                    entries.Add(new BookmarkTransferFileEntry { Name = name, Path = path });
                }

                return entries;
            }
            catch (JsonException ex)
            {
                throw new FormatException(ex.Message, ex);
            }
        }

        public static string Serialize(List<BookmarkTransferFileEntry> entries)
        {
            return JsonSerializer.Serialize(entries, ExportContext.BookmarkTransferFileEntries);
        }

        private static FormatException EntryError(int index, string reason)
        {
            return new FormatException(string.Format(CultureInfo.CurrentCulture, EntryErrorFormat, index + 1, reason));
        }
    }

    private static class TsvFormat
    {
        private static readonly CompositeFormat LineErrorFormat = CompositeFormat.Parse(Resources.bookmarks_file_line_error);

        public static List<BookmarkTransferFileEntry> Parse(string content)
        {
            var entries = new List<BookmarkTransferFileEntry>();
            var position = 0;
            var firstRecord = true;
            while (position < content.Length)
            {
                var lineLength = content.AsSpan(position).IndexOfAny('\r', '\n');
                if (lineLength < 0)
                {
                    lineLength = content.Length - position;
                }

                // Comments and blank lines are skipped only between records, never inside quoted fields.
                var line = content.AsSpan(position, lineLength).TrimStart();
                if (line.IsEmpty || line[0] == '#')
                {
                    position += lineLength;
                    if (position < content.Length)
                    {
                        position++;
                    }

                    continue;
                }

                var recordStart = position;
                var path = ReadField(content, ref position);
                var name = string.Empty;
                if (position < content.Length && content[position] == '\t')
                {
                    position++;
                    name = ReadField(content, ref position);
                }

                if (position < content.Length && content[position] is not ('\r' or '\n'))
                {
                    throw Error(content, recordStart, Resources.bookmarks_file_columns);
                }

                var record = content.AsSpan(recordStart, position - recordStart);
                var unquotedFirstRecord = firstRecord && !record.Contains('"');
                var pathColumn = path.AsSpan().Trim();
                var nameColumn = name.AsSpan().Trim();
                if (unquotedFirstRecord && pathColumn.Equals("Name", StringComparison.OrdinalIgnoreCase) && nameColumn.Equals("Path", StringComparison.OrdinalIgnoreCase))
                {
                    throw Error(content, recordStart, Resources.bookmarks_file_reversed_header);
                }

                var isHeader = unquotedFirstRecord && pathColumn.Equals("Path", StringComparison.OrdinalIgnoreCase)
                    && (nameColumn.IsEmpty || nameColumn.Equals("Name", StringComparison.OrdinalIgnoreCase));
                if (!isHeader)
                {
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        throw Error(content, recordStart, Resources.bookmarks_file_empty_path);
                    }

                    CheckEntryCount(entries.Count);
                    entries.Add(new BookmarkTransferFileEntry { Name = name, Path = path });
                }

                firstRecord = false;
            }

            return entries;
        }

        public static string Serialize(List<BookmarkTransferFileEntry> entries)
        {
            var text = new StringBuilder("Path\tName\r\n");
            foreach (var entry in entries)
            {
                text.Append(Quote(entry.Path));
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    text.Append('\t').Append(Quote(entry.Name));
                }

                text.Append("\r\n");
            }

            return text.ToString();
        }

        private static string ReadField(string content, ref int position)
        {
            var fieldStart = position;
            if (position == content.Length || content[position] != '"')
            {
                var start = position;
                while (position < content.Length && content[position] is not ('\t' or '\r' or '\n'))
                {
                    if (content[position] == '"')
                    {
                        throw Error(content, position, Resources.bookmarks_file_quotes);
                    }

                    position++;
                }

                return content[start..position];
            }

            position++;
            var field = new StringBuilder();
            while (position < content.Length)
            {
                var character = content[position++];
                if (character != '"')
                {
                    field.Append(character);
                }
                else if (position < content.Length && content[position] == '"')
                {
                    field.Append('"');
                    position++;
                }
                else
                {
                    return field.ToString();
                }
            }

            throw Error(content, fieldStart, Resources.bookmarks_file_quotes);
        }

        private static FormatException Error(string content, int position, string reason)
        {
            var line = 1;
            for (var index = 0; index < position; index++)
            {
                if (content[index] == '\r' || (content[index] == '\n' && (index == 0 || content[index - 1] != '\r')))
                {
                    line++;
                }
            }

            return new FormatException(string.Format(CultureInfo.CurrentCulture, LineErrorFormat, line, reason));
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
