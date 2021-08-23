// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImageResizer.Properties
{
    public class WrappedJsonConverter<T> : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            while (reader.Read())
            {
                if (reader.GetString() != "value")
                {
                    continue;
                }

                var result = (T)JsonSerializer.Deserialize(ref reader, typeof(T), options);
                reader.Read();
                return result;
            }

            return default;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (writer == default)
            {
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName("value");

            JsonSerializer.Serialize(writer, value, typeof(T), options);

            writer.WriteEndObject();
        }
    }
}
