// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ShortcutGuide.Models;
using YamlDotNet.Serialization;

namespace ShortcutGuide.Helpers
{
    public class ManifestInterpreter
    {
        // Todo: Get language from settings or environment variable, default to "en-US"
        public static string Language => "en-US";

        public static ShortcutFile GetShortcutsOfApplication(string applicationName)
        {
            string path = GetPathOfInterpretations();
            IEnumerable<string> files = Directory.EnumerateFiles(path, applicationName + ".*.yml") ??
                throw new FileNotFoundException($"The file for the application '{applicationName}' was not found in '{path}'.");

            IEnumerable<string> filesEnumerable = files as string[] ?? files.ToArray();
            return filesEnumerable.Any(f => f.EndsWith($".{Language}.yml", StringComparison.InvariantCulture))
                ? YamlToShortcutList(File.ReadAllText(Path.Combine(path, applicationName + $".{Language}.yml")))
                : filesEnumerable.Any(f => f.EndsWith(".en-US.yml", StringComparison.InvariantCulture))
                ? YamlToShortcutList(File.ReadAllText(filesEnumerable.First(f => f.EndsWith(".en-US.yml", StringComparison.InvariantCulture))))
                : throw new FileNotFoundException($"The file for the application '{applicationName}' was not found in '{path}' with the language '{Language}' or 'en-US'.");
        }

        public static ShortcutFile YamlToShortcutList(string content)
        {
            Deserializer deserializer = new();
            return deserializer.Deserialize<ShortcutFile>(content);
        }

        public static string GetPathOfInterpretations()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "KeyboardShortcuts");
        }

        public static IndexFile GetIndexYamlFile()
        {
            string path = GetPathOfInterpretations();
            string content = File.ReadAllText(Path.Combine(path, "index.yml"));
            Deserializer deserializer = new();
            return deserializer.Deserialize<IndexFile>(content);
        }

        public static string[] GetAllCurrentApplicationIds()
        {
            nint handle = NativeMethods.GetForegroundWindow();

            List<string> applicationIds = [];

            Process[] processes = Process.GetProcesses();

            if (NativeMethods.GetWindowThreadProcessId(handle, out uint processId) > 0)
            {
                string? name = Process.GetProcessById((int)processId).MainModule?.ModuleName;

                if (name is not null)
                {
                    try
                    {
                        foreach (var item in GetIndexYamlFile().Index.First((s) => !s.BackgroundProcess && IsMatch(name, s.WindowFilter)).Apps)
                        {
                            applicationIds.Add(item);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }

            foreach (var item in GetIndexYamlFile().Index.Where((s) => s.BackgroundProcess))
            {
                try
                {
                    if (processes.Any((p) =>
                    {
                        try
                        {
                            return IsMatch(p.MainModule!.ModuleName, item.WindowFilter);
                        }
                        catch (Win32Exception)
                        {
                            return false;
                        }
                    }))
                    {
                        foreach (var app in item.Apps)
                        {
                            applicationIds.Add(app);
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                }
            }

            return [.. applicationIds];

            static bool IsMatch(string input, string filter)
            {
                input = input.ToLower(CultureInfo.InvariantCulture);
                filter = filter.ToLower(CultureInfo.InvariantCulture);
                string regexPattern = "^" + Regex.Escape(filter).Replace("\\*", ".*") + "$";
                return Regex.IsMatch(input, regexPattern);
            }
        }
    }
}
