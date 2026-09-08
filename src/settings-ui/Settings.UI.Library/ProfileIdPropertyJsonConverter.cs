// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public sealed class ProfileIdPropertyJsonConverter : JsonConverter<ProfileIdProperty>
    {
        public override ProfileIdProperty Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected a profile reference object.");
            }

            var reference = new ProfileIdProperty();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return reference;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected a profile reference property.");
                }

                var isValue = reader.ValueTextEquals("value");
                if (!reader.Read())
                {
                    throw new JsonException("Missing profile reference value.");
                }

                if (!isValue)
                {
                    reader.Skip();
                    continue;
                }

                switch (reader.TokenType)
                {
                    case JsonTokenType.Number:
                        var legacyId = reader.GetInt32();
                        if (legacyId < 0)
                        {
                            throw new JsonException("A legacy profile id cannot be negative.");
                        }

                        reference.Value = Guid.Empty;
                        reference.LegacyId = legacyId > 0 ? legacyId : null;
                        break;
                    case JsonTokenType.String:
                        var value = reader.GetString();
                        var id = Guid.Empty;
                        if (!string.IsNullOrEmpty(value) && !Guid.TryParse(value, out id))
                        {
                            throw new JsonException("Expected a profile UUID.");
                        }

                        reference.Value = id;
                        reference.LegacyId = null;
                        break;
                    default:
                        throw new JsonException("Expected a profile UUID or legacy numeric id.");
                }
            }

            throw new JsonException("Incomplete profile reference object.");
        }

        public override void Write(Utf8JsonWriter writer, ProfileIdProperty value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            if (value.Value == Guid.Empty && value.LegacyId is > 0)
            {
                // An unrelated settings save must not discard a reference before profiles.json
                // has supplied its persisted legacy-id-to-UUID mapping.
                writer.WriteNumber("value", value.LegacyId.Value);
            }
            else
            {
                writer.WriteString("value", value.Value);
            }

            writer.WriteEndObject();
        }
    }
}
