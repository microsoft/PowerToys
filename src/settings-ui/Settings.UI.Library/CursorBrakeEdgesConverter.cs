// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Reads CursorBrake's <c>edges</c> property as the module reads it, so one malformed entry
    /// cannot fail the whole settings file. Each field is judged by its last copy, as the module's
    /// parser keeps it, and unknown fields are passed over. An entry whose last copy of a field has
    /// the wrong type, or whose ID is not valid UTF-16, reads as null, which
    /// <see cref="CursorBrakeEdgeModel.Sanitize"/> drops. A value that is not a list reads as no
    /// entries. Nesting past the serializer's depth limit still fails the whole file, because the
    /// serializer's reader stops before this converter can pass over it. Writes the property in the
    /// usual shape.
    /// </summary>
    public sealed class CursorBrakeEdgesConverter : JsonConverter<GenericProperty<List<CursorBrakeEdgeSetting>>>
    {
        public override GenericProperty<List<CursorBrakeEdgeSetting>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var entries = new List<CursorBrakeEdgeSetting>();
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                JsonElement? value = null;
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (IsNamed(property, "value"))
                    {
                        value = property.Value;
                    }
                }

                if (value is { ValueKind: JsonValueKind.Array } list)
                {
                    foreach (var element in list.EnumerateArray())
                    {
                        try
                        {
                            entries.Add(ReadEntry(element));
                        }
                        catch (InvalidOperationException)
                        {
                            // Text that is not valid UTF-16, such as a lone surrogate, cannot be
                            // read as a .NET string. No monitor ID carries one.
                            entries.Add(null);
                        }
                    }
                }
            }

            return new GenericProperty<List<CursorBrakeEdgeSetting>>(entries);
        }

        public override void Write(Utf8JsonWriter writer, GenericProperty<List<CursorBrakeEdgeSetting>> value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteStartObject();
            writer.WritePropertyName("value");
            if (value?.Value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartArray();
                foreach (var entry in value.Value)
                {
                    WriteEntry(writer, entry);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        // Whether a property has this name. A name that is not valid UTF-16 cannot be decoded, and
        // is no name this converter reads.
        private static bool IsNamed(JsonProperty property, string name)
        {
            try
            {
                return property.NameEquals(name);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        // An entry, or null when it is not an object or the last copy of a field has the wrong type.
        // An ID that is null stays null, for the model to drop the entry.
        private static CursorBrakeEdgeSetting ReadEntry(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            JsonElement? monitorsField = null;
            JsonElement? oneWayField = null;
            JsonElement? modeField = null;
            JsonElement? pushDistanceField = null;
            foreach (var property in element.EnumerateObject())
            {
                if (IsNamed(property, CursorBrakeEdgeSetting.MonitorsName))
                {
                    monitorsField = property.Value;
                }
                else if (IsNamed(property, CursorBrakeEdgeSetting.OneWayName))
                {
                    oneWayField = property.Value;
                }
                else if (IsNamed(property, CursorBrakeEdgeSetting.ModeName))
                {
                    modeField = property.Value;
                }
                else if (IsNamed(property, CursorBrakeEdgeSetting.PushDistanceName))
                {
                    pushDistanceField = property.Value;
                }
            }

            if (monitorsField is not { ValueKind: JsonValueKind.Array } monitorList)
            {
                return null;
            }

            var monitors = new List<string>();
            foreach (var id in monitorList.EnumerateArray())
            {
                if (id.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                {
                    return null;
                }

                monitors.Add(id.GetString());
            }

            // A whole number of any size the module can hold; 1.0 is 1, and 1.5 is no mode.
            if (modeField is not { ValueKind: JsonValueKind.Number } modeValue || !modeValue.TryGetDouble(out double mode) || mode != Math.Floor(mode) || mode < int.MinValue || mode > int.MaxValue)
            {
                return null;
            }

            bool oneWay = false;
            if (oneWayField is { } oneWayValue)
            {
                if (oneWayValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return null;
                }

                oneWay = oneWayValue.GetBoolean();
            }

            int pushDistance = CursorBrakeProperties.DefaultPushDistance;
            if (pushDistanceField is { } pushDistanceValue)
            {
                if (pushDistanceValue.ValueKind != JsonValueKind.Number || !pushDistanceValue.TryGetDouble(out double raw) || !double.IsFinite(raw))
                {
                    return null;
                }

                pushDistance = (int)Math.Clamp(raw, CursorBrakeProperties.MinPushDistance, CursorBrakeProperties.MaxPushDistance);
            }

            return new CursorBrakeEdgeSetting { Monitors = monitors, OneWay = oneWay, Mode = (CursorBrakeEdgeMode)(int)mode, PushDistance = pushDistance };
        }

        private static void WriteEntry(Utf8JsonWriter writer, CursorBrakeEdgeSetting entry)
        {
            if (entry == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();
            writer.WriteStartArray(CursorBrakeEdgeSetting.MonitorsName);
            foreach (var id in entry.Monitors ?? new List<string>())
            {
                writer.WriteStringValue(id);
            }

            writer.WriteEndArray();
            if (entry.OneWay)
            {
                writer.WriteBoolean(CursorBrakeEdgeSetting.OneWayName, true);
            }

            writer.WriteNumber(CursorBrakeEdgeSetting.ModeName, (int)entry.Mode);
            writer.WriteNumber(CursorBrakeEdgeSetting.PushDistanceName, entry.PushDistance);
            writer.WriteEndObject();
        }
    }
}
