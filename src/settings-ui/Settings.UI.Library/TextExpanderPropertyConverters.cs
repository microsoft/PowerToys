// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    // Match the Text Expander engine's accepted settings shapes while writing the Settings schema.
    internal static class TextExpanderPropertyConverters
    {
        internal sealed class StringValue : JsonConverter<StringProperty>
        {
            public override bool HandleNull => true;

            public override StringProperty Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using var document = JsonDocument.ParseValue(ref reader);
                return new StringProperty(ReadString(Unwrap(document.RootElement)));
            }

            public override void Write(Utf8JsonWriter writer, StringProperty value, JsonSerializerOptions options)
                => JsonSerializer.Serialize(writer, value, SettingsSerializationContext.Default.StringProperty);
        }

        internal sealed class IntValue : JsonConverter<IntProperty>
        {
            public override bool HandleNull => true;

            public override IntProperty Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using var document = JsonDocument.ParseValue(ref reader);
                JsonElement value = Unwrap(document.RootElement);
                return value.ValueKind switch
                {
                    JsonValueKind.Number when value.TryGetInt32(out int integer) => new IntProperty(integer),
                    JsonValueKind.String when int.TryParse(ReadString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer) => new IntProperty(integer),
                    _ => throw new JsonException("Expected a Text Expander integer setting."),
                };
            }

            public override void Write(Utf8JsonWriter writer, IntProperty value, JsonSerializerOptions options)
                => JsonSerializer.Serialize(writer, value, SettingsSerializationContext.Default.IntProperty);
        }

        internal sealed class BoolValue : JsonConverter<bool>
        {
            public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using var document = JsonDocument.ParseValue(ref reader);
                return Unwrap(document.RootElement).ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => throw new JsonException("Expected a Text Expander Boolean setting."),
                };
            }

            public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
                => writer.WriteBooleanValue(value);
        }

        private static JsonElement Unwrap(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return element;
            }

            return element.TryGetProperty("value", out JsonElement value)
                ? value
                : throw new JsonException("A Text Expander setting wrapper must contain a value.");
        }

        private static string ReadString(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("Expected a Text Expander string setting.");
            }

            try
            {
                return value.GetString();
            }
            catch (InvalidOperationException ex)
            {
                throw new JsonException("A Text Expander setting contains an invalid string.", ex);
            }
        }
    }
}
