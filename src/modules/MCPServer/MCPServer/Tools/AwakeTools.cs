// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library;
using ModelContextProtocol.Server;

namespace PowerToys.MCPServer.Tools
{
    [McpServerToolType]
    public static class AwakeTools
    {
        [McpServerTool]
        [Description("Echoes the message back to the client.")]
        public static string SetTimeTest(string message) => $"Hello {message}";

        private sealed class AppUsageRecord
        {
            [JsonPropertyName("process")]
            public string ProcessName { get; set; } = string.Empty;

            [JsonPropertyName("totalSeconds")]
            public double TotalSeconds { get; set; }

            [JsonPropertyName("lastUpdatedUtc")]
            public DateTime LastUpdatedUtc { get; set; }

            [JsonPropertyName("firstSeenUtc")]
            public DateTime FirstSeenUtc { get; set; }
        }

        [McpServerTool]
        [Description("Get top N foreground app usage entries recorded by Awake (reads usage.json). Parameters: top (default 10), days (default 7). Returns JSON.")]
        public static string GetAwakeUsageSummary(int top = 10, int days = 7)
        {
            try
            {
                SettingsUtils utils = new();
                string settingsPath = utils.GetSettingsFilePath("Awake");
                string directory = Path.GetDirectoryName(settingsPath)!;
                string usageFile = Path.Combine(directory, "usage.json");

                if (!File.Exists(usageFile))
                {
                    return JsonSerializer.Serialize(new { error = "usage.json not found", path = usageFile });
                }

                string json = File.ReadAllText(usageFile);
                using JsonDocument doc = JsonDocument.Parse(json);

                DateTime cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, days));

                var result = doc.RootElement
                    .EnumerateArray()
                    .Select(e => new
                    {
                        process = e.GetPropertyOrDefault("process", string.Empty),
                        totalSeconds = Math.Round(e.GetPropertyOrDefault("totalSeconds", 0.0), 1),
                        lastUpdatedUtc = e.GetPropertyOrDefaultDateTime("lastUpdatedUtc"),
                        firstSeenUtc = e.GetPropertyOrDefaultDateTime("firstSeenUtc"),
                    })
                    .Where(r => r.lastUpdatedUtc >= cutoff)
                    .OrderByDescending(r => r.totalSeconds)
                    .Take(top)
                    .Select(r => new
                    {
                        r.process,
                        r.totalSeconds,
                        totalHours = Math.Round(r.totalSeconds / 3600.0, 2),
                        r.firstSeenUtc,
                        r.lastUpdatedUtc,
                    });

                return JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private static string GetPropertyOrDefault(this JsonElement element, string name, string defaultValue)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? defaultValue;
            }

            return defaultValue;
        }

        private static double GetPropertyOrDefault(this JsonElement element, string name, double defaultValue)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double d))
            {
                return d;
            }

            return defaultValue;
        }

        private static DateTime GetPropertyOrDefaultDateTime(this JsonElement element, string name)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value))
            {
                if (value.ValueKind == JsonValueKind.String && value.TryGetDateTime(out DateTime dt))
                {
                    return dt;
                }
            }

            return DateTime.MinValue;
        }
    }
}
