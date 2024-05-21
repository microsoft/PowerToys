// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using ProjectsEditor.Utils;

namespace Projects.Data
{
    public class ProjectsEditorData<T>
    {
        // Note: the same path should be used in SnapshotTool and Launcher
        public string GetDataFolder()
        {
            // return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        public string GetTempDataFolder()
        {
            return Path.GetTempPath();
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
