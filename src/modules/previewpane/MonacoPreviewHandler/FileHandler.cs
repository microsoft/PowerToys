// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

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
                JObject a = JObject.Parse(File.ReadAllText(Settings.AssemblyDirectory + "\\languages.json"));
                for (int i = 0; i < a["list"].Count(); i++)
                {
                    for (int j = 0; j < a["list"][i]["extensions"].Count(); j++)
                    {
                        if (a["list"][i]["extensions"][j].ToString() == fileExtension)
                        {
                            return a["list"][i]["id"].ToString();
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
