// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.Text;
using Wox.Plugin.Common.Win32;
using Wox.Plugin.Logger;

namespace Wox.Plugin.Common
{
    /// <summary>
    /// Contains information (e.g. path to executable, name...) about the default browser.
    /// </summary>
    public static class DefaultBrowserInfo
    {
        private static readonly object _updateLock = new object();
        private static int _lastUpdateTickCount = -1;

        public static readonly string MSEdgePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Microsoft\Edge\Application\msedge.exe";
        public const string MSEdgeName = "Microsoft Edge";

        private static readonly IPath FilePath = new FileSystem().Path;

        /// <summary>Gets the path to default browser's executable.</summary>
        public static string Path { get; private set; }

        /// <summary>Gets <see cref="Path" /> since the icon is embedded in the executable.</summary>
        public static string IconPath { get => Path; }

        public static string Name { get; private set; }

        /// <summary>Gets a value indicating whether the browser supports querying a web search via command line argument (`? <term>`).</summary>
        public static bool SupportsWebSearchByCmdLineArgument { get => SearchEngineUrl is null; }

        public static bool IsDefaultBrowserSet { get => !string.IsNullOrEmpty(Path); }

        /// <summary>
        /// Gets the browser's default search engine's url ready to be formatted with a single value (like `https://www.bing.com/search?q={0}`)
        /// </summary>
#pragma warning disable CA1056 // URI-like properties should not be strings
        public static string SearchEngineUrl { get; private set; }
#pragma warning restore CA1056 // URI-like properties should not be strings

        /// <summary>
        /// Updates only if at least more than 300ms has passed since the last update, to avoid multiple calls to <see cref="Update"/>.
        /// (because of multiple plugins calling update at the same time.)
        /// </summary>
        /// <param name="defaultToEdgeOnFail">If true, If <see cref="Update"/> fails, for any reason, the browser will be set to Microsoft Edge.</param>
        public static void UpdateIfTimePassed()
        {
            int curTickCount = Environment.TickCount;
            if (curTickCount - _lastUpdateTickCount > 300)
            {
                _lastUpdateTickCount = curTickCount;
                Update();
            }
        }

        /// <summary>
        /// Consider using <see cref="UpdateIfTimePassed"/> to avoid updating multiple times.
        /// (because of multiple plugins calling update at the same time.)
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We want to keep the process alive but will log the exception")]
        public static void Update()
        {
            lock (_updateLock)
            {
                try
                {
                    string progId = GetRegistryValue(
                        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice",
                        "ProgId");

                    // The `?` argument doesn't work on opera, so we get the user's default search engine:
                    if (progId.StartsWith("Opera", StringComparison.OrdinalIgnoreCase))
                    {
                        // Opera user preferences file:
                        string prefFile;

                        if (progId.Contains("GX", StringComparison.OrdinalIgnoreCase))
                        {
                            Name = "Opera GX";
                            prefFile = System.IO.File.ReadAllText($"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\Opera Software\\Opera GX Stable\\Preferences");
                        }
                        else
                        {
                            Name = "Opera";
                            prefFile = System.IO.File.ReadAllText($"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\Opera Software\\Opera Stable\\Preferences");
                        }

                        // "default_search_provider_data" doesn't exist if the user hasn't searched for the first time,
                        // therefore we set `url` to opera's default search engine:
                        string url = "https://www.google.com/search?client=opera&q={0}&sourceid=opera";

                        using (System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(prefFile))
                        {
                            if (doc.RootElement.TryGetProperty("default_search_provider_data", out var element))
                            {
                                if (element.TryGetProperty("template_url_data", out element))
                                {
                                    if (element.TryGetProperty("url", out element))
                                    {
                                        url = element.GetString();
                                    }
                                }
                            }
                        }

                        url = url
                            .Replace("{searchTerms}", "{0}", StringComparison.Ordinal)
                            .Replace("{inputEncoding}", "UTF-8", StringComparison.Ordinal)
                            .Replace("{outputEncoding}", "UTF-8", StringComparison.Ordinal);

                        int startIndex = url.IndexOf('}', StringComparison.Ordinal) + 1;

                        // In case there are other url parameters (e.g. `&foo={bar}`), remove them:
                        for (int i = url.IndexOf("}", startIndex, StringComparison.Ordinal);
                                i != -1;
                                i = url.IndexOf("}", startIndex, StringComparison.Ordinal))
                        {
                            for (int j = i - 1; j > 0; --j)
                            {
                                if (url[j] == '&')
                                {
                                    url = url.Remove(j, i - j + 1);
                                    break;
                                }
                            }
                        }

                        SearchEngineUrl = url;
                    }
                    else
                    {
                        string appName = GetRegistryValue($@"HKEY_CLASSES_ROOT\{progId}\Application", "ApplicationName")
                            ?? GetRegistryValue($@"HKEY_CLASSES_ROOT\{progId}", "FriendlyTypeName");

                        if (appName != null)
                        {
                            // Handle indirect strings:
                            if (appName.StartsWith("@", StringComparison.Ordinal))
                            {
                                appName = GetIndirectString(appName);
                            }

                            appName = appName
                                .Replace("URL", null, StringComparison.OrdinalIgnoreCase)
                                .Replace("HTML", null, StringComparison.OrdinalIgnoreCase)
                                .Replace("Document", null, StringComparison.OrdinalIgnoreCase)
                                .TrimEnd();
                        }

                        Name = appName;

                        SearchEngineUrl = null;
                    }

                    string programLocation =

                        // Resolve App Icon (UWP)
                        GetRegistryValue(
                            $@"HKEY_CLASSES_ROOT\{progId}\Application",
                            "ApplicationIcon")

                        // Resolves default  file association icon (UWP + Normal)
                        ?? GetRegistryValue($@"HKEY_CLASSES_ROOT\{progId}\DefaultIcon", null);

                    // "Handles 'Indirect Strings' (UWP programs)"
                    // Using Ordinal since this is internal and used with a symbol
                    if (programLocation.StartsWith("@", StringComparison.Ordinal))
                    {
                        string directProgramLocation = GetIndirectString(programLocation);
                        Path = string.Equals(FilePath.GetExtension(directProgramLocation), ".exe", StringComparison.Ordinal)
                            ? directProgramLocation
                            : null;
                    }
                    else
                    {
                        // Using Ordinal since this is internal and used with a symbol
                        var indexOfComma = programLocation.IndexOf(',', StringComparison.Ordinal);
                        Path = indexOfComma > 0
                            ? programLocation.Substring(0, indexOfComma)
                            : programLocation;
                    }

                    if (string.IsNullOrEmpty(Path))
                    {
                        throw new Exception("Browser path is null or empty.");
                    }
                }
                catch (Exception e)
                {
                    Path = null;
                    Name = null;
                    SearchEngineUrl = null;

                    Log.Exception("Exception when retrieving browser path/name. Path and Name are null.", e, typeof(DefaultBrowserInfo));
                }

                string GetRegistryValue(string registryLocation, string valueName)
                {
                    return Microsoft.Win32.Registry.GetValue(registryLocation, valueName, null) as string;
                }

                string GetIndirectString(string str)
                {
                    var stringBuilder = new StringBuilder(128);
                    if (NativeMethods.SHLoadIndirectString(
                            str,
                            stringBuilder,
                            (uint)stringBuilder.Capacity,
                            IntPtr.Zero)
                        == HRESULT.S_OK)
                    {
                        return stringBuilder.ToString();
                    }

                    throw new Exception("Could not load indirect string.");
                }
            }
        }
    }
}
