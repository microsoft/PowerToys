// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    // Reads either { "value": "left" | "right" } (legacy string form, pre-v3.0) or { "value": 0 | 1 } (current int form).
    // Always writes the int form so saved settings.json migrates forward on first save.
    public sealed class ShortcutGuideWindowPositionConverter : JsonConverter<IntProperty>
    {
        public override IntProperty Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            int value = (int)ShortcutGuideWindowPosition.Left;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return new IntProperty(value);
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                bool isValueProp = string.Equals(reader.GetString(), "value", StringComparison.Ordinal);
                reader.Read();

                if (!isValueProp)
                {
                    reader.Skip();
                    continue;
                }

                switch (reader.TokenType)
                {
                    case JsonTokenType.Number:
                        value = reader.GetInt32();
                        break;
                    case JsonTokenType.String:
                        value = string.Equals(reader.GetString(), "right", StringComparison.OrdinalIgnoreCase)
                            ? (int)ShortcutGuideWindowPosition.Right
                            : (int)ShortcutGuideWindowPosition.Left;
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return new IntProperty(value);
        }

        public override void Write(Utf8JsonWriter writer, IntProperty value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("value", value.Value);
            writer.WriteEndObject();
        }
    }
}
