// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.Text;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.WebSearch
{
    public class BrowserInfo
    {
        public static readonly string MSEdgePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Microsoft\Edge\Application\msedge.exe";
        public const string MSEdgeName = "Microsoft Edge";

        private static readonly IPath FilePath = new FileSystem().Path;

        public string Path { get; private set; }

        public string Name { get; private set; } = Properties.Resources.plugin_browser;

        public bool UseCmdLineArgumentForWebSearch { get => SearchEngineUrl is null; }

#pragma warning disable CA1056 // Justification: We want to format the string with the search term.
        public string SearchEngineUrl { get; private set; }
#pragma warning restore CA1056 // URI-like properties should not be strings

        public BrowserInfo()
        {
            Update();
        }

        /// <param name="defaultToEdgeOnFail">If true, If this function fails, for any reason, the browser will be set to Microsoft Edge.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We want to keep the process alive but will log the exception")]
        public void Update(bool defaultToEdgeOnFail = true)
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

                    if (appName is null)
                    {
                        appName = Properties.Resources.plugin_browser;
                    }
                    else
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

                var programLocation =

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
                if (defaultToEdgeOnFail)
                {
                    Path = MSEdgePath;
                    Name = MSEdgeName;
                }
                else
                {
                    Path = null;
                    Name = null;
                }

                SearchEngineUrl = null;

                Log.Exception("Exception when retrieving browser path/name" + (defaultToEdgeOnFail ? "; Browser set to microsoft edge" : null), e, GetType());
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
                    == NativeMethods.Hresult.Ok)
                {
                    return stringBuilder.ToString();
                }

                throw new Exception("Could not load indirect string.");
            }
        }
    }
}
