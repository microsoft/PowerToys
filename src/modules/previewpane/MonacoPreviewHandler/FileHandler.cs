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
                JsonDocument a = JsonDocument.Parse(File.ReadAllText(Settings.AssemblyDirectory + "\\languages.json"));
                JsonElement list = a.RootElement.GetProperty("list");
                for (int i = 0; i < list.GetArrayLength(); i++)
                {
                    for (int j = 0; j < list[i].GetProperty("extensions").GetArrayLength(); j++)
                    {
                        if (list[i].GetProperty("extensions")[j].ToString() == fileExtension)
                        {
                            return list[i].GetProperty("id").ToString();
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
