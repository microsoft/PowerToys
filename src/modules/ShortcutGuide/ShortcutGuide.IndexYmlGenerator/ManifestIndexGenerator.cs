// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ShortcutGuide.IndexYmlGenerator
{
    public static class ManifestIndexGenerator
    {
        private sealed class YamlFormatException : FormatException
        {
            public YamlFormatException(string message)
                : base(message)
            {
            }
        }

        private readonly struct ManifestHeader
        {
            public string? PackageName { get; init; }

            public string? WindowFilter { get; init; }

            public bool BackgroundProcess { get; init; }
        }

        private const int InitialIndexFileCapacity = 4096;
        private const string IndexFileName = "index.yml";
        private const string TempIndexFileName = "index.yml.tmp";

        private const string PackageNamePrefix = "PackageName:";
        private const string WindowFilterPrefix = "WindowFilter:";
        private const string BackgroundProcessPrefix = "BackgroundProcess:";
        private const string ShortcutsPrefix = "Shortcuts:";

        public static string DefaultManifestsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WinGet",
            "KeyboardShortcuts");

        public static IndexGenerationResult CreateIndexYmlFile() =>
            CreateIndexYmlFile(DefaultManifestsPath);

        /// <summary>
        /// Determines whether index.yml needs to be created or regenerated based on the
        /// existence of index.yml and the last write timestamps of all manifest files in
        /// the directory.
        /// </summary>
        /// <param name="path">The directory containing manifest files and index.yml.
        /// </param>
        /// <returns><c>true</c> if index.yml is missing or older than any manifest;
        /// otherwise <c>false</c>.</returns>
        public static bool NeedsIndexRegeneration(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            if (!Directory.Exists(path))
            {
                return true;
            }

            string indexPath = Path.Combine(path, IndexFileName);
            if (!File.Exists(indexPath))
            {
                return true;
            }

            DateTime indexWriteTimeUtc = File.GetLastWriteTimeUtc(indexPath);

            foreach (string manifestPath in Directory.EnumerateFiles(path, "*.yml"))
            {
                string fileName = Path.GetFileName(manifestPath);
                if (string.Equals(fileName, IndexFileName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, TempIndexFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (File.GetLastWriteTimeUtc(manifestPath) > indexWriteTimeUtc)
                {
                    return true;
                }
            }

            return false;
        }

        public static IndexGenerationResult CreateIndexYmlFile(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            Directory.CreateDirectory(path);

            string[] files = Directory.GetFiles(path, "*.yml");

            ConcurrentBag<ManifestHeader> parsedHeaders = [];
            ConcurrentBag<(string FileName, Exception Exception)> errors = [];
            ConcurrentBag<(string FileName, string Warning)> warnings = [];

            // Parallel I/O and parsing hide Windows Defender/NTFS file handle inspection
            // latency, especially when manifests have just been copied and scanned.
            Parallel.ForEach(files, file =>
            {
                string filename = Path.GetFileName(file);
                if (string.Equals(filename, IndexFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                try
                {
                    // ReadAllText() is a single I/O system call and faster than
                    // StreamReader for small files.
                    string content = File.ReadAllText(file);

                    if (TryParseManifestHeader(
                        content, filename, out ManifestHeader manifestHeader, out string? warning))
                    {
                        parsedHeaders.Add(manifestHeader);
                    }
                    else if (warning != null)
                    {
                        warnings.Add((filename, warning));
                    }
                }
                catch (Exception ex)
                    when (ex is IOException or UnauthorizedAccessException or YamlFormatException)
                {
                    errors.Add((filename, ex));
                }
            });

            // Reduce the parsed headers into the grouped and sorted structure for the
            // index file.
            Dictionary<(string WindowFilter, bool BackgroundProcess), HashSet<string>> processes =
                new(parsedHeaders.Count);

            foreach (var header in parsedHeaders)
            {
                var key = (header.WindowFilter!, header.BackgroundProcess);
                if (!processes.TryGetValue(key, out HashSet<string>? apps))
                {
                    apps = new HashSet<string>(StringComparer.Ordinal);
                    processes[key] = apps;
                }

                apps.Add(header.PackageName!);
            }

            // Build the index file content in memory and write it to disk.
            var sb = new StringBuilder(InitialIndexFileCapacity);
            sb.AppendLine("DefaultShellName: +WindowsNT.Shell");
            sb.AppendLine("Index:");

            List<(string WindowFilter, bool BackgroundProcess)> sortedKeys = new(processes.Keys);
            sortedKeys.Sort(static (a, b) =>
            {
                int cmp = string.Compare(a.WindowFilter, b.WindowFilter, StringComparison.Ordinal);
                return cmp != 0 ? cmp : a.BackgroundProcess.CompareTo(b.BackgroundProcess);
            });

            foreach (var key in sortedKeys)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- WindowFilter: {FormatYamlScalar(key.WindowFilter)}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"  BackgroundProcess: {(key.BackgroundProcess ? "true" : "false")}");
                sb.AppendLine("  Apps:");

                List<string> apps = new(processes[key]);
                apps.Sort(StringComparer.Ordinal);
                foreach (string app in apps)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"  - {FormatYamlScalar(app)}");
                }
            }

            // Write to a temp file and rename into place rather than writing index.yml
            // directly, so a concurrent reader (e.g. the Shortcut Guide UI process)
            // never observes a partially-written file. File.Move on the same volume
            // is effectively atomic from a reader's point of view.
            string indexPath = Path.Combine(path, IndexFileName);
            string tempPath = Path.Combine(path, TempIndexFileName);
            File.WriteAllText(tempPath, sb.ToString());
            File.Move(tempPath, indexPath, overwrite: true);

            return new IndexGenerationResult(
                TotalFiles: files.Length,
                IndexedFiles: parsedHeaders.Count,
                Errors: errors.ToArray(),
                Warnings: warnings.ToArray());
        }

        // We always single-quote rather than trying to detect which values need quoting.
        // Single-quoted scalars need no escaping beyond doubling embedded single quotes,
        // which sidesteps having to track every YAML plain-scalar restriction (reserved
        // words, leading indicators, colon-space, numeric-looking values, etc.).
        private static string FormatYamlScalar(string value) =>
            value.Contains('\'', StringComparison.Ordinal)
                ? $"'{value.Replace("'", "''", StringComparison.Ordinal)}'"
                : $"'{value}'";

        private static bool TryParseManifestHeader(
            string content,
            string filename,
            out ManifestHeader header,
            out string? warning)
        {
            warning = null;
            string? packageName = null;
            string? windowFilter = null;
            bool backgroundProcess = false;
            bool hasBackgroundProcess = false;
            bool hasAnyNonEmptyContent = false;

            // EnumerateLines runs over the single string in memory without allocating.
            foreach (ReadOnlySpan<char> rawLine in content.AsSpan().EnumerateLines())
            {
                ReadOnlySpan<char> trimmed = rawLine.TrimStart();
                if (trimmed.IsEmpty || trimmed[0] == '#')
                {
                    continue;
                }

                hasAnyNonEmptyContent = true;

                if (char.IsWhiteSpace(rawLine[0]))
                {
                    continue;
                }

                if (rawLine.StartsWith(PackageNamePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    packageName = ExtractStringScalar(rawLine[PackageNamePrefix.Length..], filename, "PackageName");
                }
                else if (rawLine.StartsWith(WindowFilterPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    windowFilter = ExtractStringScalar(rawLine[WindowFilterPrefix.Length..], filename, "WindowFilter");
                }
                else if (rawLine.StartsWith(BackgroundProcessPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    backgroundProcess = ExtractBoolScalar(rawLine[BackgroundProcessPrefix.Length..], filename, "BackgroundProcess");
                    hasBackgroundProcess = true;
                }
                else if (rawLine.StartsWith(ShortcutsPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    // Only short-circuit if all top-level header fields have already been parsed.
                    // BackgroundProcess may appear before or after Shortcuts.
                    if (packageName != null && windowFilter != null && hasBackgroundProcess)
                    {
                        break;
                    }
                }
            }

            if (!hasAnyNonEmptyContent)
            {
                warning = "file is empty.";
                header = default;
                return false;
            }

            if (string.IsNullOrWhiteSpace(packageName))
            {
                warning = "required property 'PackageName' is missing or empty.";
                header = default;
                return false;
            }

            if (string.IsNullOrWhiteSpace(windowFilter))
            {
                warning = "required property 'WindowFilter' is missing or empty.";
                header = default;
                return false;
            }

            header = new ManifestHeader
            {
                PackageName = packageName,
                WindowFilter = windowFilter,
                BackgroundProcess = backgroundProcess,
            };

            return true;
        }

        private static string ExtractStringScalar(ReadOnlySpan<char> span, string filename, string propertyName)
        {
            span = CleanScalarSpan(span, filename, propertyName);
            return span.IsEmpty ? string.Empty : span.ToString();
        }

        private static bool ExtractBoolScalar(ReadOnlySpan<char> span, string filename, string propertyName)
        {
            span = CleanScalarSpan(span, filename, propertyName);
            return span.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        // Note: this does not decode escape sequences inside double-quoted scalars
        // (e.g. \" or \\). Manifest values are contributor-authored, not user input,
        // so this is treated as an acceptable limitation rather than a bug - but a
        // manifest containing PackageName: "Foo\"Bar" will round-trip the backslash
        // literally rather than unescaping it.
        private static ReadOnlySpan<char> CleanScalarSpan(
            ReadOnlySpan<char> span, string filename, string propertyName)
        {
            span = span.Trim();

            int commentIdx = span.IndexOf('#');
            if (commentIdx >= 0)
            {
                span = span[..commentIdx].Trim();
            }

            if (span.IsEmpty)
            {
                return span;
            }

            if (span[0] == '[' || span[0] == '{')
            {
                throw new YamlFormatException($"Invalid scalar format for '{propertyName}' in file '{filename}'.");
            }

            if (span.Length >= 2 && ((span[0] == '"' && span[^1] == '"') || (span[0] == '\'' && span[^1] == '\'')))
            {
                span = span[1..^1];
            }

            return span;
        }
    }
}
