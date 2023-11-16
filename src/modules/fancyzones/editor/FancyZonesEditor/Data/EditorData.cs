// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;

namespace FancyZonesEditor.Data
{
    public class EditorData
    {
        protected string File { get; set; }

        protected JsonSerializerOptions JsonOptions
        {
            get
            {
                return new JsonSerializerOptions
                {
                    PropertyNamingPolicy = new DashCaseNamingPolicy(),
                    WriteIndented = true,
                };
            }
        }

        public string GetDataFolder()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
    }
}
