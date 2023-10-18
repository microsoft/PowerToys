// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Common.UI;
using ManagedCommon;

namespace Peek.FilePreviewer.Previewers
{
    public class MonacoHelper
    {
        public static readonly HashSet<string> SupportedMonacoFileTypes = GetExtensions();

        public static HashSet<string> GetExtensions()
        {
            HashSet<string> set = new HashSet<string>();
            try
            {
                JsonDocument languageListDocument = Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.GetLanguages();
                JsonElement languageList = languageListDocument.RootElement.GetProperty("list");
                foreach (JsonElement e in languageList.EnumerateArray())
            {
                if (e.TryGetProperty("extensions", out var extensions))
                {
                    for (int j = 0; j < extensions.GetArrayLength(); j++)
                    {
                            set.Add(extensions[j].ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to get monaco extensions: " + ex.Message);
            }

            return set;
        }

        /// <summary>
        /// Prepares temp html for the previewing
        /// </summary>
        public static string PreviewTempFile(string fileText, string extension, string tempFolder)
        {
            // TODO: check if file is too big, add MaxFileSize to settings
            return InitializeIndexFileAndSelectedFile(fileText, extension, tempFolder);
        }

        private static string InitializeIndexFileAndSelectedFile(string fileContent, string extension, string tempFolder)
        {
            string vsCodeLangSet = Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.GetLanguage(extension);
            string base64FileCode = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fileContent));
            string theme = ThemeManager.GetWindowsBaseColor().ToLowerInvariant();

            // prepping index html to load in
            string html = Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.ReadIndexHtml();

            html = html.Replace("[[PT_LANG]]", vsCodeLangSet, StringComparison.InvariantCulture);
            html = html.Replace("[[PT_WRAP]]", "1", StringComparison.InvariantCulture); // TODO: add to settings
            html = html.Replace("[[PT_THEME]]", theme, StringComparison.InvariantCulture);
            html = html.Replace("[[PT_CODE]]", base64FileCode, StringComparison.InvariantCulture);
            html = html.Replace("[[PT_URL]]", Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.VirtualHostName, StringComparison.InvariantCulture);

            string filename = tempFolder + "\\" + Guid.NewGuid().ToString() + ".html";
            File.WriteAllText(filename, html);
            return filename;
        }
    }
}
