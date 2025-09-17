// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
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
        [Description("Get top N foreground app usage entries recorded by Awake. Reads usage.sqlite if present (preferred) else legacy usage.json. Parameters: top (default 10), days (default 7). Returns JSON array.")]
        public static string GetAwakeUsageSummary(int top = 10, int days = 7)
        {
            try
            {
                SettingsUtils utils = new();
                string settingsPath = utils.GetSettingsFilePath("Awake");
                string directory = Path.GetDirectoryName(settingsPath)!;
                string sqlitePath = Path.Combine(directory, "usage.sqlite");
                string legacyJson = Path.Combine(directory, "usage.json");

                if (File.Exists(sqlitePath))
                {
                    return QuerySqlite(sqlitePath, top, days);
                }

                // Fallback to legacy JSON if DB not yet created (tracking not enabled or not flushed).
                if (File.Exists(legacyJson))
                {
                    return QueryLegacyJson(legacyJson, top, days, note: "legacy-json");
                }

                return JsonSerializer.Serialize(new { error = "No usage data found", sqlite = sqlitePath, legacy = legacyJson });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private static string QuerySqlite(string dbPath, int top, int days)
        {
            try
            {
                int safeDays = Math.Max(1, days);
                using SqliteConnection conn = new(new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
                conn.Open();
                using SqliteCommand cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT process_name, SUM(total_seconds) AS total_seconds, MIN(first_seen_utc) AS first_seen_utc, MAX(last_updated_utc) AS last_updated_utc
FROM process_usage
WHERE day_utc >= date('now', @cutoff)
GROUP BY process_name
ORDER BY total_seconds DESC
LIMIT @top;";
                cmd.Parameters.AddWithValue("@cutoff", $"-{safeDays} days");
                cmd.Parameters.AddWithValue("@top", top);

                var list = cmd.ExecuteReader()
                    .Cast<System.Data.Common.DbDataRecord>()
                    .Select(r => new AppUsageRecord
                    {
                        ProcessName = r.GetString(0),
                        TotalSeconds = r.GetDouble(1),
                        FirstSeenUtc = DateTime.Parse(r.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                        LastUpdatedUtc = DateTime.Parse(r.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    })
                    .OrderByDescending(r => r.TotalSeconds)
                    .Select(r => new
                    {
                        process = r.ProcessName,
                        totalSeconds = Math.Round(r.TotalSeconds, 1),
                        totalHours = Math.Round(r.TotalSeconds / 3600.0, 2),
                        firstSeenUtc = r.FirstSeenUtc,
                        lastUpdatedUtc = r.LastUpdatedUtc,
                        source = "sqlite",
                    });

                return JsonSerializer.Serialize(list);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = "sqlite query failed", message = ex.Message, path = dbPath });
            }
        }

        private static string QueryLegacyJson(string usageFile, int top, int days, string? note = null)
        {
            try
            {
                string json = File.ReadAllText(usageFile);
                using JsonDocument doc = JsonDocument.Parse(json);
                DateTime cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, days));
                var result = doc.RootElement
                    .EnumerateArray()
                    .Select(e => new
                    {
                        process = e.GetPropertyOrDefault("process", string.Empty),
                        totalSeconds = e.GetPropertyOrDefault("totalSeconds", 0.0),
                        lastUpdatedUtc = e.GetPropertyOrDefaultDateTime("lastUpdatedUtc"),
                        firstSeenUtc = e.GetPropertyOrDefaultDateTime("firstSeenUtc"),
                    })
                    .Where(r => r.lastUpdatedUtc >= cutoff)
                    .OrderByDescending(r => r.totalSeconds)
                    .Take(top)
                    .Select(r => new
                    {
                        r.process,
                        totalSeconds = Math.Round(r.totalSeconds, 1),
                        totalHours = Math.Round(r.totalSeconds / 3600.0, 2),
                        r.firstSeenUtc,
                        r.lastUpdatedUtc,
                        source = note ?? "json",
                    });
                return JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = "legacy json read failed", message = ex.Message, path = usageFile });
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
