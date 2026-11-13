// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace WorkspacesEditor.Helpers
{
    internal static class WindowPlacementStorage
    {
        internal static IDictionary<string, object> Deserialize(string json)
        {
            ArgumentNullException.ThrowIfNull(json);

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Window placement storage must be a JSON object.");
            }

            var storage = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    // WinUIEx expects strings, rather than deserialized JsonElement values.
                    storage[property.Name] = property.Value.GetString()!;
                }
            }

            return storage;
        }

        internal static string Serialize(IDictionary<string, object> storage)
        {
            ArgumentNullException.ThrowIfNull(storage);

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var entry in storage)
                {
                    if (entry.Value is string value)
                    {
                        writer.WriteString(entry.Key, value);
                    }
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
