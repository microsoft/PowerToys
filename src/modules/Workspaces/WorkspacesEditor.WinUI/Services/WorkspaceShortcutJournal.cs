// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ManagedCommon;

namespace WorkspacesEditor.Services
{
    internal sealed partial class WorkspaceShortcutJournal
    {
        private const string Suffix = ".links.json";
        private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars();
        private readonly string _path;
        private HashSet<string> _names;
        private bool _hasRecord;

        private WorkspaceShortcutJournal(string path, string projectId, HashSet<string> names, bool pendingRemoval, bool hasRecord)
        {
            _path = path;
            ProjectId = projectId;
            _names = names;
            PendingRemoval = pendingRemoval;
            _hasRecord = hasRecord;
        }

        internal string ProjectId { get; }

        // Callers can edit their snapshot before Save without changing what the journal has committed.
        internal HashSet<string> Names => new(_names, StringComparer.Ordinal);

        internal bool PendingRemoval { get; private set; }

        internal static WorkspaceShortcutJournal Read(string iconFolder, Guid workspaceId, string projectId)
        {
            string path = Path.Combine(iconFolder, $"{workspaceId:B}{Suffix}");
            string fallbackId = IsProjectId(projectId, workspaceId) ? projectId : workspaceId.ToString("B");
            string backup = path + ".corrupt";
            Entry entry;
            try
            {
                using var stream = File.OpenRead(path);
                entry = ReadEntry(stream, workspaceId);
            }
            catch (JsonException)
            {
                // An invalid record cannot establish ownership of a previous name. Preserve its
                // bytes separately and let the service rebuild from independently verified links.
                entry = null;
                try
                {
                    string destination = HasBackup(path) ? $"{backup}.{Guid.NewGuid():N}" : backup;
                    WorkspaceShortcutFile.MoveIf(path, destination, current =>
                    {
                        try
                        {
                            entry = ReadEntry(current, workspaceId);
                            return false;
                        }
                        catch (JsonException)
                        {
                            return true;
                        }
                    });
                    if (entry == null)
                    {
                        Logger.LogWarning($"Preserved an invalid workspace shortcut record as {Path.GetFileName(destination)}");
                    }
                }
                catch (FileNotFoundException)
                {
                    // Another recovery already moved the record.
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException || (ex is DirectoryNotFoundException && !File.Exists(iconFolder)))
            {
                entry = null;
            }

            return new WorkspaceShortcutJournal(
                path,
                entry?.ProjectId ?? fallbackId,
                new HashSet<string>(entry?.Names ?? [], StringComparer.Ordinal),
                pendingRemoval: entry?.PendingRemoval == true,
                hasRecord: entry != null);
        }

        private static Entry ReadEntry(Stream stream, Guid workspaceId)
        {
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            Entry entry;
            if (root.ValueKind == JsonValueKind.Array)
            {
                entry = new Entry
                {
                    Names = root.Deserialize<string[]>(),
                    PendingRemoval = false,
                };
            }
            else
            {
                entry = root.Deserialize<Entry>();
                if (entry == null || !IsProjectId(entry.ProjectId, workspaceId))
                {
                    throw new JsonException("The workspace shortcut record has an invalid workspace ID.");
                }
            }

            if (entry.Names == null || !entry.Names.All(IsValidName))
            {
                throw new JsonException("The workspace shortcut record contains an invalid name list.");
            }

            return entry;
        }

        internal static IEnumerable<Guid> EnumerateIds(string folder)
        {
            string[] paths;
            try
            {
                paths = Directory.GetFiles(folder, "*" + Suffix);
            }
            catch (DirectoryNotFoundException)
            {
                return Array.Empty<Guid>();
            }

            var ids = new List<Guid>(paths.Length);
            foreach (string path in paths)
            {
                string name = Path.GetFileName(path);
                const int BracedGuidLength = 38;
                if (name.Length == BracedGuidLength + Suffix.Length &&
                    name.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase) &&
                    Guid.TryParseExact(name.AsSpan(0, BracedGuidLength), "B", out Guid id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        // A preserved corrupt record may have described another name still using this ICO.
        // Only a link whose name and ownership can be checked may be repaired or removed.
        internal bool HasUnrecoverableNames => HasBackup(_path);

        private static bool HasBackup(string journalPath)
        {
            try
            {
                return (File.GetAttributes(journalPath + ".corrupt") & FileAttributes.Directory) == 0;
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return false;
            }
        }

        internal void Save(IEnumerable<string> names, bool pendingRemoval)
        {
            var nextNames = new HashSet<string>(names.Where(IsValidName), StringComparer.Ordinal);
            if (nextNames.Count == 0 && !pendingRemoval)
            {
                if (!_hasRecord)
                {
                    return;
                }

                File.Delete(_path);
                _hasRecord = false;
            }
            else
            {
                if (PendingRemoval == pendingRemoval && _names.SetEquals(nextNames))
                {
                    return;
                }

                string directory = Path.GetDirectoryName(_path);
                Directory.CreateDirectory(directory);
                string temporary = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
                var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                try
                {
                    using (stream)
                    {
                        var entry = new Entry
                        {
                            ProjectId = ProjectId,
                            Names = nextNames.Order(StringComparer.Ordinal).ToArray(),
                            PendingRemoval = pendingRemoval,
                        };
                        JsonSerializer.Serialize(stream, entry);
                        stream.Flush(flushToDisk: true);
                    }

                    File.Move(temporary, _path, overwrite: _hasRecord);
                }
                catch
                {
                    File.Delete(temporary);
                    throw;
                }

                _hasRecord = true;
            }

            _names = nextNames;
            PendingRemoval = pendingRemoval;
        }

        internal static bool IsValidName(string name) =>
            !string.IsNullOrWhiteSpace(name) && name is not "." and not ".." &&
            name.IndexOfAny(InvalidNameChars) < 0 && !ReservedDeviceName().IsMatch(name);

        private static bool IsProjectId(string value, Guid workspaceId) =>
            IsValidName(value) && Guid.TryParse(value, out Guid id) && id == workspaceId;

        [GeneratedRegex(@"^(CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|(?:COM|LPT)[1-9¹²³]) *(?:[.]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex ReservedDeviceName();

        private sealed class Entry
        {
            public string ProjectId { get; init; }

            public required string[] Names { get; init; }

            public required bool PendingRemoval { get; init; }
        }
    }
}
