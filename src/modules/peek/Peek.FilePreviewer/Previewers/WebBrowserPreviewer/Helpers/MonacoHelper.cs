// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
        public static (Uri Uri, string VsCodeLangSet, string FileContent) PreviewTempFile(string fileText, string extension, bool tryFormat)
        {
            // TODO: check if file is too big, add MaxFileSize to settings
            return InitializeIndexFileAndSelectedFile(fileText, extension, tempFolder, tryFormat, wrapText, stickyScroll, fontSize);
        }

        private static string InitializeIndexFileAndSelectedFile(string fileContent, string extension, string tempFolder, bool tryFormat, bool wrapText, bool stickyScroll, int fontSize)
        {
            string vsCodeLangSet = Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.GetLanguage(extension);

            if (tryFormat)
            {
                var formatter = Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.Formatters.SingleOrDefault(f => f.LangSet == vsCodeLangSet);
                if (formatter != null)
                {
                    try
                    {
                        fileContent = formatter.Format(fileContent);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to apply formatting", ex);
                    }
                }
            }

            return (new Uri("http://" + Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.VirtualHostName + "/index.html"), vsCodeLangSet, fileContent);
        }
    }
}
