// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ShortcutGuide.Models;
using Windows.Devices.SmartCards;
using WinUIEx;
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

        public static string[] GetAllCurrentApplicationIds()
        {
            IntPtr handle = NativeMethods.GetForegroundWindow();

            List<string> applicationIds = [];

            static bool IsMatch(string input, string filter)
            {
                string regexPattern = "^" + Regex.Escape(filter).Replace("\\*", ".*") + "$";
                return Regex.IsMatch(input, regexPattern);
            }

            var processes = Process.GetProcesses();

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

            return [.. applicationIds];
        }

        public static ShortcutList GetShortcutsOfDefaultShell()
        {
            return GetShortcutsOfApplication(GetIndexYamlFile().DefaultShellName);
        }
    }
}
