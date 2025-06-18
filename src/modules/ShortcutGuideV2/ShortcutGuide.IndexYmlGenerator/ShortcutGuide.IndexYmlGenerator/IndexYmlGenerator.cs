// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;
using YamlDotNet.Serialization;

// This should all be outsourced to WinGet in the future
namespace ShortcutGuide.IndexYmlGenerator
{
    public class IndexYmlGenerator
    {
        public static void Main()
        {
            CreateIndexYmlFile();
        }

        // Todo: Exception handling
        public static void CreateIndexYmlFile()
        {
            string path = ManifestInterpreter.GetPathOfInterpretations();
            if (File.Exists(Path.Combine(path, "index.yml")))
            {
                File.Delete(Path.Combine(path, "index.yml"));
            }

            IndexFile indexFile = new() { };
            Dictionary<(string WindowFilter, bool BackgroundProcess), List<string>> processes = [];

            foreach (string file in Directory.EnumerateFiles(path, "*.yml"))
            {
                string content = File.ReadAllText(file);
                Deserializer deserializer = new();
                ShortcutFile shortcutFile = deserializer.Deserialize<ShortcutFile>(content);
                if (processes.TryGetValue((shortcutFile.WindowFilter, shortcutFile.BackgroundProcess), out List<string>? apps))
                {
                    if (apps.Contains(shortcutFile.PackageName))
                    {
                        continue;
                    }

                    apps.Add(shortcutFile.PackageName);
                    continue;
                }

                processes[(shortcutFile.WindowFilter, shortcutFile.BackgroundProcess)] = [shortcutFile.PackageName];
            }

            indexFile.Index = [];

            foreach (var item in processes)
            {
                indexFile.Index =
                [
                    .. indexFile.Index,
                    new IndexFile.IndexItem
                    {
                        WindowFilter = item.Key.WindowFilter,
                        BackgroundProcess = item.Key.BackgroundProcess,
                        Apps = [.. item.Value],
                    },
                ];
            }

            // Todo: Take the default shell name from the settings or environment variable, default to "+WindowsNT.Shell"
            indexFile.DefaultShellName = "+WindowsNT.Shell";

            Serializer serializer = new();
            string yamlContent = serializer.Serialize(indexFile);
            File.WriteAllText(Path.Combine(path, "index.yml"), yamlContent);
        }
    }
}
