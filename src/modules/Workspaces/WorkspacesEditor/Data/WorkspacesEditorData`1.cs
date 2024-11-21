// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using WorkspacesEditor.Utils;

namespace WorkspacesEditor.Data
{
    public class WorkspacesEditorData<T>
    {
        protected JsonSerializerOptions JsonOptions
        {
            get => new()
            {
                PropertyNamingPolicy = new DashCaseNamingPolicy(),
                WriteIndented = true,
            };
        }

        public T Read(string file)
        {
            IOUtils ioUtils = new();
            string data = ioUtils.ReadFile(file);
            return JsonSerializer.Deserialize<T>(data, JsonOptions);
        }

        public string Serialize(T data)
        {
            return JsonSerializer.Serialize(data, JsonOptions);
        }
    }
}
