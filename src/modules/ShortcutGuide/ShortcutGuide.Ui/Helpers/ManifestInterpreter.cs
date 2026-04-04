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
    /// <summary>
    /// Helps to interpret the manifest files for the Shortcut Guide.
    /// </summary>
    public class ManifestInterpreter
    {
        // Todo: Get language from settings or environment variable, default to "en-US"

        /// <summary>
        /// Gets the language used for the manifest files.
        /// </summary>
        public static string Language => "en-US";

        /// <summary>
        /// Returns the shortcuts for a specific application.
        /// </summary>
        /// <remarks>
        /// The method should only be called if the application is known to have a shortcuts file.
        /// </remarks>
        /// <param name="applicationName">The manifest id.</param>
        /// <returns>The deserialized shortcuts file.</returns>
        /// <exception cref="FileNotFoundException">The requested file was not found.</exception>
        public static ShortcutFile GetShortcutsOfApplication(string applicationName)
        {
            string path = PathOfManifestFiles;
            IEnumerable<string> files = Directory.EnumerateFiles(path, applicationName + ".*.yml") ??
                throw new FileNotFoundException($"The file for the application '{applicationName}' was not found in '{path}'.");

            IEnumerable<string> filesEnumerable = files as string[] ?? [.. files];
            return filesEnumerable.Any(f => f.EndsWith($".{Language}.yml", StringComparison.InvariantCulture))
                ? YamlToShortcutList(File.ReadAllText(Path.Combine(path, applicationName + $".{Language}.yml")))
                : filesEnumerable.Any(f => f.EndsWith(".en-US.yml", StringComparison.InvariantCulture))
                ? YamlToShortcutList(File.ReadAllText(filesEnumerable.First(f => f.EndsWith(".en-US.yml", StringComparison.InvariantCulture))))
                : throw new FileNotFoundException($"The file for the application '{applicationName}' was not found in '{path}' with the language '{Language}' or 'en-US'.");
        }

        /// <summary>
        /// Deserializes the content of a YAML file to a <see cref="ShortcutFile"/>.
        /// </summary>
        /// <param name="content">The content of the YAML file.</param>
        /// <returns>A deserialized <see cref="ShortcutFile"/> object.</returns>
        private static ShortcutFile YamlToShortcutList(string content)
        {
            Deserializer deserializer = new();
            return deserializer.Deserialize<ShortcutFile>(content);
        }

        /// <summary>
        /// Gets the path to the directory where the manifest files are stored.
        /// </summary>
        public static string PathOfManifestFiles => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "KeyboardShortcuts");

        /// <summary>
        /// Retrieves the index YAML file that contains the list of all applications and their shortcuts.
        /// </summary>
        /// <returns>A deserialized <see cref="IndexFile"/> object.</returns>
        public static IndexFile GetIndexYamlFile()
        {
            string path = PathOfManifestFiles;
            string content = File.ReadAllText(Path.Combine(path, "index.yml"));
            Deserializer deserializer = new();
            return deserializer.Deserialize<IndexFile>(content);
        }

        /// <summary>
        /// Retrieves all application IDs that should be displayed, based on the foreground window and background processes.
        /// </summary>
        /// <returns>An array of all application IDs.</returns>
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
