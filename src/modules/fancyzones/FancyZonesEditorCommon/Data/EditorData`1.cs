// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using FancyZonesEditorCommon.Utils;

namespace FancyZonesEditorCommon.Data
{
    public class EditorData<T>
    {
        public string GetDataFolder()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        protected static JsonSerializerOptions JsonOptions => FancyZonesJsonContext.Default.Options;

        protected static JsonTypeInfo<T> TypeInfo => (JsonTypeInfo<T>)FancyZonesJsonContext.Default.GetTypeInfo(typeof(T));

        public T Read(string file)
        {
            IOUtils ioUtils = new IOUtils();
            string data = ioUtils.ReadFile(file);
            return JsonSerializer.Deserialize(data, TypeInfo);
        }

        public string Serialize(T data)
        {
            return JsonSerializer.Serialize(data, TypeInfo);
        }
    }
}
