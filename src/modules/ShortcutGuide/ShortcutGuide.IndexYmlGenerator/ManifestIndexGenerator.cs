// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ManagedCommon;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

// This class should be moved to WinGet in the future
namespace ShortcutGuide.IndexYmlGenerator
{
    public static class ManifestIndexGenerator
    {
        private const string IndexFileName = "index.yml";

        public static void CreateIndexYmlFile() => CreateIndexYmlFile(ManifestInterpreter.PathOfManifestFiles);

        public static void CreateIndexYmlFile(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            Directory.CreateDirectory(path);

            IndexFile indexFile = new() { };
            Dictionary<(string WindowFilter, bool BackgroundProcess), List<string>> processes = [];

            Deserializer deserializer = new();

            foreach (string file in Directory.EnumerateFiles(path, "*.yml"))
            {
                string filename = Path.GetFileName(file);

                // Skip index file.
                if (string.Equals(filename, IndexFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    string content = File.ReadAllText(file);
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        Logger.LogWarning($"Skipping manifest '{filename}': file is empty.");
                        continue;
                    }

                    ShortcutFile shortcutFile = deserializer.Deserialize<ShortcutFile>(content);

                    if (string.IsNullOrWhiteSpace(shortcutFile.PackageName))
                    {
                        Logger.LogWarning($"Skipping manifest '{filename}': required property 'PackageName' is missing or empty.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(shortcutFile.WindowFilter))
                    {
                        Logger.LogWarning($"Skipping manifest '{filename}': required property 'WindowFilter' is missing or empty.");
                        continue;
                    }

                    if (processes.TryGetValue(
                        (shortcutFile.WindowFilter, shortcutFile.BackgroundProcess),
                        out List<string>? apps))
                    {
                        if (!apps.Contains(shortcutFile.PackageName))
                        {
                            apps.Add(shortcutFile.PackageName);
                        }

                        continue;
                    }

                    processes[(shortcutFile.WindowFilter, shortcutFile.BackgroundProcess)] =
                        [shortcutFile.PackageName];
                }
                catch (Exception ex)
                    when (ex is IOException or UnauthorizedAccessException or YamlException or NullReferenceException)
                {
                    // Bad YAML, file access or permission issue. Log and continue with
                    // the next file.
                    Logger.LogError($"Error processing file '{filename}'.", ex);
                }
            }

            indexFile.Index = processes.Select(item => new IndexFile.IndexItem
            {
                WindowFilter = item.Key.WindowFilter,
                BackgroundProcess = item.Key.BackgroundProcess,
                Apps = [.. item.Value],
            }).ToArray();

            // Todo: Take the default shell name from the settings or environment variable, default to "+WindowsNT.Shell"
            indexFile.DefaultShellName = "+WindowsNT.Shell";

            Serializer serializer = new();
            string yamlContent = serializer.Serialize(indexFile);
            File.WriteAllText(Path.Combine(path, IndexFileName), yamlContent);
        }
    }
}
