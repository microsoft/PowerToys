// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            string localizedPath = Path.Combine(path, applicationName + $".{Language}.yml");
            string fallbackPath = Path.Combine(path, applicationName + ".en-US.yml");

            if (File.Exists(localizedPath))
            {
                return YamlToShortcutList(File.ReadAllText(localizedPath));
            }

            if (File.Exists(fallbackPath))
            {
                return YamlToShortcutList(File.ReadAllText(fallbackPath));
            }

            throw new FileNotFoundException($"The file for the application '{applicationName}' was not found in '{path}' with the language '{Language}' or 'en-US'.");
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

        private static readonly object IndexLock = new();
        private static IndexFile? cachedIndexFile;
        private static DateTime cachedIndexLastWriteTimeUtc;

        /// <summary>
        /// Scans the per-user manifest folder and (re)writes <c>index.yml</c>,
        /// which maps window filters / background-process flags to the list of
        /// app manifest IDs that match. Runs in-process; safe to call from a
        /// background thread.
        /// </summary>
        public static void GenerateIndexYmlFile()
        {
            string path = PathOfManifestFiles;
            string indexPath = Path.Combine(path, "index.yml");

            if (File.Exists(indexPath))
            {
                File.Delete(indexPath);
            }

            IndexFile indexFile = default;
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

            indexFile.Index = processes.Select(item => new IndexFile.IndexItem
            {
                WindowFilter = item.Key.WindowFilter,
                BackgroundProcess = item.Key.BackgroundProcess,
                Apps = [.. item.Value],
            }).ToArray();

            // Todo: Take the default shell name from the settings or environment variable, default to "+WindowsNT.Shell"
            indexFile.DefaultShellName = "+WindowsNT.Shell";

            Serializer serializer = new();
            File.WriteAllText(indexPath, serializer.Serialize(indexFile));

            // Invalidate the cache so subsequent reads pick up the fresh index.
            lock (IndexLock)
            {
                cachedIndexFile = null;
                cachedIndexLastWriteTimeUtc = default;
            }
        }

        /// <summary>
        /// Retrieves the index YAML file that contains the list of all applications and their shortcuts from the cache.
        /// </summary>
        /// <returns>A deserialized <see cref="IndexFile"/> object.</returns>
        public static IndexFile GetCachedIndexYamlFile()
        {
            string indexPath = Path.Combine(PathOfManifestFiles, "index.yml");

            lock (IndexLock)
            {
                DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(indexPath);
                if (cachedIndexFile is not null && cachedIndexLastWriteTimeUtc == lastWriteTimeUtc)
                {
                    return cachedIndexFile.Value;
                }

                string content = File.ReadAllText(indexPath);
                Deserializer deserializer = new();

                cachedIndexFile = deserializer.Deserialize<IndexFile>(content);
                cachedIndexLastWriteTimeUtc = lastWriteTimeUtc;

                return cachedIndexFile.Value;
            }
        }

        /// <summary>
        /// Gets the path to the directory where the manifest files are stored.
        /// </summary>
        public static string PathOfManifestFiles => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "KeyboardShortcuts");

        /// <summary>
        /// Retrieves all application IDs that should be displayed, based on the foreground window and background processes.
        /// </summary>
        /// <param name="foregroundWindow">
        /// HWND of the window to treat as the active foreground app. Pass the
        /// value captured at Shortcut Guide startup so we match the user's
        /// target app and not the Shortcut Guide window itself. If <c>0</c>,
        /// the current foreground window is queried (legacy behavior).
        /// </param>
        /// <returns>
        /// A dictionary mapping each application ID to the full path of the executable
        /// that caused the match (used for icon extraction), or <c>null</c> when no
        /// specific executable is associated (for example, wildcard filters like the
        /// default shell).
        /// </returns>
        public static Dictionary<string, string?> GetAllCurrentApplicationIds(nint foregroundWindow = 0)
        {
            nint handle = foregroundWindow != 0 ? foregroundWindow : NativeMethods.GetForegroundWindow();

            Dictionary<string, string?> applicationIds = new(StringComparer.Ordinal);

            if (NativeMethods.GetWindowThreadProcessId(handle, out uint processId) > 0)
            {
                string? name = null;
                string? executablePath = null;

                try
                {
                    ProcessModule? mainModule = Process.GetProcessById((int)processId).MainModule;
                    name = mainModule?.ModuleName;
                    executablePath = mainModule?.FileName;
                }
                catch (Win32Exception)
                {
                    // Access denied for elevated processes; we cannot read the module.
                }
                catch (InvalidOperationException)
                {
                    // Process exited between enumeration and access.
                }

                if (name is not null)
                {
                    try
                    {
                        IndexFile.IndexItem match = ManifestInterpreter.GetCachedIndexYamlFile().Index.First((s) => !s.BackgroundProcess && IsMatch(name, s.WindowFilter));
                        string? pathForApp = match.WindowFilter == "*" ? null : executablePath;
                        foreach (var item in match.Apps)
                        {
                            applicationIds[item] = pathForApp;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }

            foreach (var item in GetCachedIndexYamlFile().Index.Where((s) => s.BackgroundProcess))
            {
                string filter = item.WindowFilter;

                if (filter.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase))
                {
                    filter = filter[..^4];
                }

                if (filter == "*")
                {
                    foreach (var app in item.Apps)
                    {
                        applicationIds[app] = null;
                    }

                    continue;
                }

                Process[] foundProcesses = [];

                try
                {
                    foundProcesses = Process.GetProcessesByName(filter);
                    if (foundProcesses.Length > 0)
                    {
                        foreach (var app in item.Apps)
                        {
                            applicationIds[app] = foundProcesses[0].MainModule?.FileName;
                        }
                    }
                }
                catch (Win32Exception ex)
                {
                    Trace.WriteLine($"Failed to inspect background process '{filter}': {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    Trace.WriteLine($"Failed to inspect background process '{filter}': {ex.Message}");
                }
                finally
                {
                    foreach (var process in foundProcesses)
                    {
                        process.Dispose();
                    }
                }
            }

            return applicationIds;

            static bool IsMatch(string input, string filter)
            {
                if (filter == "*")
                {
                    return true;
                }

                if (input.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    input = input[..^4];
                }

                if (filter.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    filter = filter[..^4];
                }

                return string.Equals(input, filter, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
