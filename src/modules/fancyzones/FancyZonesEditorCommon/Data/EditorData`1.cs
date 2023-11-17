// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using FancyZonesEditorCommon.Utils;

namespace FancyZonesEditorCommon.Data
{
    public class EditorData<T>
    {
        public string GetDataFolder()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

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

        public T Read(string file)
        {
            IOUtils ioUtils = new IOUtils();
            string data = ioUtils.ReadFile(file);
            return JsonSerializer.Deserialize<T>(data, JsonOptions);
        }

        public string Serialize(T data)
        {
            return JsonSerializer.Serialize(data, JsonOptions);
        }
    }
}
