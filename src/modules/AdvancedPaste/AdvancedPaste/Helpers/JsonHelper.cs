// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using ManagedCommon;
using Newtonsoft.Json;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Helpers
{
    internal static class JsonHelper
    {
        internal static string ToJsonFromXmlOrCsv(string inputText)
        {
            Logger.LogTrace();

            string jsonText = string.Empty;

            // Try convert XML
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(inputText);
                jsonText = JsonConvert.SerializeXmlNode(doc, Newtonsoft.Json.Formatting.Indented);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed parsing input as xml", ex);
            }

            // Try convert CSV
            try
            {
                if (string.IsNullOrEmpty(jsonText))
                {
                    var csv = new List<string[]>();

                    foreach (var line in inputText.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        csv.Add(line.Split(","));
                    }

                    jsonText = JsonConvert.SerializeObject(csv, Newtonsoft.Json.Formatting.Indented);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed parsing input as csv", ex);
            }

            return string.IsNullOrEmpty(jsonText) ? inputText : jsonText;
        }
    }
}
