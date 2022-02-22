// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
            try
            {
                JsonDocument languageListDocument = JsonDocument.Parse(File.ReadAllText(Settings.AssemblyDirectory + "\\monaco_languages.json"));
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
