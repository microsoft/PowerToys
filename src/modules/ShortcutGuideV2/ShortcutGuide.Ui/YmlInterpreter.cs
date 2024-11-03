// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using ShortcutGuide.Exceptions;
using ShortcutGuide.Models;
using YamlDotNet.Serialization;

namespace ShortcutGuide
{
    public class YmlInterpreter
    {
        public static ShortcutList GetShortcutsOfApplication(string applicationName)
        {
            string path = GetPathOfIntepretations();
            string content = File.ReadAllText(Path.Combine(path, applicationName + ".yml"));
            return YamlToShortcutList(content);
        }

        public static ShortcutList YamlToShortcutList(string content)
        {
            Deserializer deserializer = new();
            return deserializer.Deserialize<ShortcutList>(content);
        }

        public static string GetPathOfIntepretations()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "KeyboardShortcuts");
        }

        public static IndexFile GetIndexYamlFile()
        {
            string path = GetPathOfIntepretations();
            string content = File.ReadAllText(Path.Combine(path, "index.yml"));
            Deserializer deserializer = new();
            return deserializer.Deserialize<IndexFile>(content);
        }

        public static ShortcutList GetShortcutsOfDefaultShell()
        {
            return GetShortcutsOfApplication(GetIndexYamlFile().DefaultShellName);
        }
    }
}
