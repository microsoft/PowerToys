// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Microsoft.PowerToys.PreviewHandler.Monaco
{
    public static class FileHandler
    {
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
                JsonDocument languageListDocument;
                using (StreamReader jsonFileReader = new StreamReader(new FileStream(Settings.AssemblyDirectory + "\\monaco_languages.json", FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    languageListDocument = JsonDocument.Parse(jsonFileReader.ReadToEnd());
                    jsonFileReader.Close();
                }

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
    }
}
