// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
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

        /// <summary>
        /// Gets the path of the current assembly.
        /// </summary>
        /// <remarks>
        /// Source: https://stackoverflow.com/a/283917/14774889
        /// </remarks>
        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().Location;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        public static string MonacoDirectory
        {
            get
            {
                // TODO: common monaco folder
                string codeBase = Assembly.GetExecutingAssembly().Location;
                string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(codeBase), "..", "FileExplorerPreview"));
                return path;
            }
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
                    for (int j = 0; j < e.GetProperty("extensions").GetArrayLength(); j++)
                    {
                        if (e.GetProperty("extensions")[j].ToString() == fileExtension)
                        {
                            return e.GetProperty("id").ToString();
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
