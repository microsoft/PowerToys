// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerDisplay.Models
{
    /// <summary>
    /// Reads both legacy numeric ids and UUIDs without allocating an identity during deserialization.
    /// The profile store assigns and persists missing UUIDs before exposing them to callers.
    /// </summary>
    public sealed class PowerDisplayProfileJsonConverter : JsonConverter<PowerDisplayProfile>
    {
        public override PowerDisplayProfile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("A profile must be a JSON object.");
            }

            var profile = new PowerDisplayProfile();
            if (root.TryGetProperty("legacyId", out var legacyId) && legacyId.ValueKind != JsonValueKind.Null)
            {
                profile.LegacyId = legacyId.GetInt32();
            }

            if (root.TryGetProperty("id", out var id))
            {
                if (id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out var numericId))
                {
                    profile.LegacyId = numericId > 0 ? numericId : null;
                }
                else if (id.ValueKind == JsonValueKind.String && Guid.TryParse(id.GetString(), out var uuid))
                {
                    profile.Id = uuid;
                }
                else
                {
                    throw new JsonException("A profile id must be a UUID or a legacy integer.");
                }
            }

            if (root.TryGetProperty("order", out var order))
            {
                profile.Order = order.GetInt32();
            }

            if (root.TryGetProperty("name", out var name))
            {
                profile.Name = name.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("monitorSettings", out var monitorSettings))
            {
                profile.MonitorSettings = JsonSerializer.Deserialize(
                    monitorSettings,
                    ProfileSerializationContext.Default.ListProfileMonitorSetting) ?? new List<ProfileMonitorSetting>();
            }

            if (root.TryGetProperty("createdDate", out var createdDate))
            {
                profile.CreatedDate = createdDate.GetDateTime();
            }

            if (root.TryGetProperty("lastModified", out var lastModified))
            {
                profile.LastModified = lastModified.GetDateTime();
            }

            return profile;
        }

        public override void Write(Utf8JsonWriter writer, PowerDisplayProfile value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("name", value.Name);
            writer.WriteString("id", value.Id);
            writer.WriteNumber("order", value.Order);
            if (value.LegacyId.HasValue)
            {
                writer.WriteNumber("legacyId", value.LegacyId.Value);
            }

            writer.WritePropertyName("monitorSettings");
            JsonSerializer.Serialize(writer, value.MonitorSettings, ProfileSerializationContext.Default.ListProfileMonitorSetting);
            writer.WriteString("createdDate", value.CreatedDate);
            writer.WriteString("lastModified", value.LastModified);
            writer.WriteEndObject();
        }
    }
}
