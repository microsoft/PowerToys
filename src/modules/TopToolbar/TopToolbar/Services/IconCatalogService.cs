// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.UI.Xaml.Controls;
using TopToolbar.Models;

namespace TopToolbar.Services
{
    internal static class IconCatalogService
    {
        public const string CatalogScheme = "catalog";

        private static readonly IReadOnlyList<IconCatalogEntry> Catalog;
        private static readonly IReadOnlyDictionary<string, IconCatalogEntry> CatalogMap;

        static IconCatalogService()
        {
            var list = new List<IconCatalogEntry>
            {
                CreateSvg("display", "Display", "System", "display", "monitor", "screen", "desktop"),
                CreateSvg("speaker", "Audio", "System", "speaker", "sound", "volume", "music"),
                CreateSvg("wifi", "Network", "Connectivity", "wifi", "wireless", "internet", "connection"),
                CreateSvg("bolt", "Lightning", "Quick launch", "bolt", "power", "flash", "script"),
                CreateSvg("check-circle", "Check", "Monitoring", "check-circle", "confirm", "status", "success"),
                CreateSvg("cloud", "Cloud", "Web", "cloud", "web", "internet", "sync"),
                CreateSvg("terminal", "Terminal", "Developer", "terminal", "console", "cli", "shell"),
                CreateSvg("calendar", "Calendar", "Time", "calendar", "schedule", "events", "date"),
                CreateSvg("clipboard", "Clipboard", "Utilities", "clipboard", "notes", "copy", "task"),
                CreateSvg("grid", "Grid", "Productivity", "grid", "layout", "apps", "matrix"),
                CreateSvg("heart", "Favorite", "Personal", "heart", "like", "love", "pin"),
                CreateSvg("play", "Play", "Media", "play", "run", "start", "begin"),
                CreateSvg("workspace", "Workspace", "Layouts", "workspace", "snap", "arrange", "desktop"),
                CreateSvg("tasks", "Tasks", "Productivity", "tasks", "todo", "list", "organize"),
                CreateSvg("rocket", "Rocket", "Automation", "rocket", "deploy", "launch", "boost"),
            };

            foreach (var glyphEntry in BuildGlyphCatalogEntries())
            {
                if (!list.Any(existing => string.Equals(existing.Id, glyphEntry.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add(glyphEntry);
                }
            }

            Catalog = new ReadOnlyCollection<IconCatalogEntry>(list);
            CatalogMap = Catalog.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
        }

        public static IconCatalogEntry GetDefault()
        {
            return Catalog.Count > 0 ? Catalog[0] : null;
        }

        public static IReadOnlyList<IconCatalogEntry> GetAll()
        {
            return Catalog;
        }

        public static bool TryGetById(string id, out IconCatalogEntry entry)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                entry = null;
                return false;
            }

            return CatalogMap.TryGetValue(id.Trim(), out entry);
        }

        public static IconCatalogEntry ResolveFromPath(string iconPath)
        {
            return TryParseCatalogId(iconPath, out var id) && CatalogMap.TryGetValue(id, out var entry) ? entry : null;
        }

        public static string BuildCatalogPath(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Icon id cannot be null or whitespace.", nameof(id));
            }

            return string.Concat(CatalogScheme, ":", id.Trim());
        }

        public static bool TryParseCatalogId(string iconPath, out string id)
        {
            id = string.Empty;
            if (string.IsNullOrWhiteSpace(iconPath))
            {
                return false;
            }

            var trimmed = iconPath.Trim();
            string candidateId = string.Empty;

            if (trimmed.StartsWith(CatalogScheme + ":", StringComparison.OrdinalIgnoreCase))
            {
                candidateId = trimmed.Substring(CatalogScheme.Length + 1);
            }
            else if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                if (string.Equals(uri.Scheme, CatalogScheme, StringComparison.OrdinalIgnoreCase))
                {
                    candidateId = uri.AbsolutePath.Trim('/');
                }
                else
                {
                    var fromPath = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
                    candidateId = fromPath ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(candidateId))
                {
                    candidateId = Path.GetFileNameWithoutExtension(candidateId);
                }
            }
            else
            {
                candidateId = Path.GetFileNameWithoutExtension(trimmed);
            }

            if (string.IsNullOrWhiteSpace(candidateId))
            {
                id = string.Empty;
                return false;
            }

            candidateId = candidateId.Trim();
            if (CatalogMap.ContainsKey(candidateId))
            {
                id = candidateId;
                return true;
            }

            id = string.Empty;
            return false;
        }

        private static IconCatalogEntry CreateSvg(string id, string name, string category, string fileName, params string[] keywords)
        {
            var uri = new Uri($"ms-appx:///Assets/Icons/{fileName}.svg", UriKind.Absolute);
            return new IconCatalogEntry(id, name, category, uri, keywords ?? Array.Empty<string>());
        }

        private static IEnumerable<IconCatalogEntry> BuildGlyphCatalogEntries()
        {
            foreach (Symbol symbol in Enum.GetValues(typeof(Symbol)))
            {
                var codepoint = (int)symbol;
                if (codepoint <= 0)
                {
                    continue;
                }

                string glyph;
                try
                {
                    glyph = char.ConvertFromUtf32(codepoint);
                }
                catch
                {
                    continue;
                }

                var displayName = ToFriendlyName(symbol.ToString());
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = symbol.ToString();
                }

                var keywords = new List<string>
                {
                    displayName,
                    displayName.Replace(" ", string.Empty),
                    FormatCodepoint(codepoint),
                    glyph,
                    symbol.ToString(),
                };

                yield return new IconCatalogEntry(
                    id: $"glyph-{codepoint:X4}",
                    displayName: displayName,
                    category: "Segoe Fluent Icons",
                    resourceUri: null,
                    keywords: keywords,
                    glyph: glyph,
                    fontFamily: "Segoe Fluent Icons,Segoe MDL2 Assets");
            }
        }

        private static string FormatCodepoint(int codepoint)
        {
            return string.Concat("U+", codepoint.ToString("X4", CultureInfo.InvariantCulture));
        }

        private static string ToFriendlyName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length + 8);
            builder.Append(value[0]);

            for (int i = 1; i < value.Length; i++)
            {
                var current = value[i];
                var previous = value[i - 1];

                if (char.IsUpper(current) && !char.IsUpper(previous))
                {
                    builder.Append(' ');
                }
                else if (char.IsDigit(current) && !char.IsDigit(previous))
                {
                    builder.Append(' ');
                }

                builder.Append(current);
            }

            return builder.ToString();
        }
    }
}
