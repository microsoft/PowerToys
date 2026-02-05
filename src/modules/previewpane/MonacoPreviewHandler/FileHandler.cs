// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Microsoft.PowerToys.PreviewHandler.Monaco
{
    public static class FileHandler
    {
        /// <summary>
        /// Converts a file extension to a language monaco id.
        /// </summary>
        /// <param name="fileExtension">The extension of the file (without the dot).</param>
        /// <param name="fileName">Optional filename for matching files without extensions (e.g., "Dockerfile").</param>
        /// <returns>The monaco language id</returns>
        public static string GetLanguage(string fileExtension, string fileName = null)
        {
            fileExtension = fileExtension.ToLower(CultureInfo.CurrentCulture);
            try
            {
                var languageListDocument = FilePreviewCommon.MonacoHelper.GetLanguages();

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

                // Fallback to filename matching for files without extensions (e.g., Dockerfile)
                if (!string.IsNullOrEmpty(fileName))
                {
                    string languageByFileName = FilePreviewCommon.MonacoHelper.GetLanguageByFileName(fileName);
                    if (languageByFileName != null)
                    {
                        return languageByFileName;
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
