using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Windows.Data.Json;
using MonacoPreviewHandler;
using Newtonsoft.Json.Linq;

namespace monacoPreview
{
    class FileHandler
    {
        private Settings _settings = new Settings();
        
        /// <summary>
        /// Converts a file extension to a language monaco id.
        /// </summary>
        /// <param name="fileExtension">The extension of the file (without the dot).</param>
        /// <returns>The monaco language id</returns>
        public string GetLanguage(string fileExtension)
        {
            JObject a = JObject.Parse(File.ReadAllText(_settings.AssemblyDirectory + "\\languages.json"));
            for (int i = 0; i < a["list"].Count(); i++)
            {
                for (int j = 0; j < a["list"][i]["extensions"].Count(); j++)
                {
                    if (a["list"][i]["extensions"][j].ToString() == fileExtension)
                    {
                        return a["list"][i]["aliases"][0].ToString();
                    }
                }
            }

            return "plaintext";
        }
    }
}