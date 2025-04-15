// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.FilePreviewCommon.Monaco.Formatters;

namespace Microsoft.PowerToys.FilePreviewCommon
{
    public static class MonacoHelper
    {
        /// <summary>
        /// Name of the virtual host
        /// </summary>
        public const string VirtualHostName = "PowerToysLocalMonaco";

        /// <summary>
        /// Formatters applied before rendering the preview
        /// </summary>
        public static readonly IReadOnlyCollection<IFormatter> Formatters = new List<IFormatter>
        {
            new JsonFormatter(),
            new XmlFormatter(),
        }.AsReadOnly();

        private static readonly Lazy<string> _monacoDirectory = new(GetRuntimeMonacoDirectory);

        /// <summary>
        /// Gets the path of the Monaco assets folder.
        /// </summary>
        public static string MonacoDirectory => _monacoDirectory.Value;

        private static string GetRuntimeMonacoDirectory()
        {
            string baseDirectory = AppContext.BaseDirectory ?? string.Empty;

            // AppContext.BaseDirectory returns a stray \\ so we want to remove that.
            baseDirectory = Path.TrimEndingDirectorySeparator(baseDirectory);

            // If the executable is within "WinUI3Apps", correct the path first.
            // The idea of GetFileName here is getting the last directory in the path.
            if (Path.GetFileName(baseDirectory) == "WinUI3Apps")
            {
                baseDirectory = Path.Combine(baseDirectory, "..");
            }

            string monacoPath = Path.Combine(baseDirectory, "Assets", "Monaco");

            return Directory.Exists(monacoPath) ?
                monacoPath :
                throw new DirectoryNotFoundException($"Monaco assets directory not found at {monacoPath}");
        }

        public static JsonDocument GetLanguages()
        {
            JsonDocument languageListDocument;
            using (StreamReader jsonFileReader = new StreamReader(new FileStream(MonacoDirectory + "\\monaco_languages.json", FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                languageListDocument = JsonDocument.Parse(jsonFileReader.ReadToEnd());
                jsonFileReader.Close();
            }

            return languageListDocument;
        }

        /// <summary>
        /// Converts a file extension to a language monaco id.
        /// </summary>
        /// <param name="fileExtension">The extension of the file (without the dot).</param>
        /// <returns>The monaco language id</returns>
        public static string GetLanguage(string fileExtension)
        {
            fileExtension = fileExtension.ToLower(CultureInfo.CurrentCulture);
            try
            {
                JsonDocument languageListDocument = GetLanguages();
                JsonElement languageList = languageListDocument.RootElement.GetProperty("list");
                foreach (JsonElement e in languageList.EnumerateArray())
                {
                    if (e.TryGetProperty("extensions", out var extensions))
                    {
                        for (int j = 0; j < extensions.GetArrayLength(); j++)
                        {
                            if (extensions[j].ToString() == fileExtension)
                            {
                                return e.GetProperty("id").ToString();
                            }
                        }
                    }
                }

                return "plaintext";
            }
            catch (Exception)
            {
                return "plaintext";
            }
        }

        public static string ReadIndexHtml()
        {
            string html;

            using (StreamReader htmlFileReader = new StreamReader(new FileStream(MonacoDirectory + "\\index.html", FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                html = htmlFileReader.ReadToEnd();
                htmlFileReader.Close();
            }

            return html;
        }
    }
}
