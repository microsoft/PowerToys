// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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

        // =============================
        // HTTP client (Awake remote control)
        // =============================
        private static readonly HttpClient _http = new HttpClient();

        // Base URL for Awake HTTP server. Default matches Awake --http-port default (8080).
        // Allow override through environment variable POWERTOYS_AWAKE_HTTP (e.g. http://localhost:9090/)
        private static string BaseUrl => (Environment.GetEnvironmentVariable("POWERTOYS_AWAKE_HTTP") ?? "http://localhost:8080/").TrimEnd('/') + "/";

        private static string JsonError(string msg, int? status = null) => JsonSerializer.Serialize(new { success = false, error = msg, status });

        private static string JsonOk(object payload) => JsonSerializer.Serialize(payload);

        private static string SendAwakeRequest(string method, string relativePath, object? body = null)
        {
            try
            {
                using var req = new HttpRequestMessage(new HttpMethod(method), BaseUrl + relativePath.TrimStart('/'));
                if (body != null)
                {
                    string json = JsonSerializer.Serialize(body);
                    req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                using var resp = _http.Send(req);
                string respText = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode)
                {
                    return JsonError($"HTTP {(int)resp.StatusCode} {resp.StatusCode}", (int)resp.StatusCode) + "\n" + respText;
                }

                return respText;
            }
            catch (HttpRequestException ex)
            {
                return JsonError($"Connection failed: {ex.Message}. Ensure Awake is running with --http-server.");
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message);
            }
        }

        [McpServerTool]
        [Description("Get Awake HTTP status (GET /awake/status). Requires Awake launched with --http-server.")]
        public static string AwakeHttpStatus() => SendAwakeRequest("GET", "awake/status");

        [McpServerTool]
        [Description("Set indefinite keep-awake via HTTP. Params: keepDisplayOn=true|false, processId=0")]
        public static string AwakeHttpIndefinite(bool keepDisplayOn = true, int processId = 0)
            => SendAwakeRequest("POST", "awake/indefinite", new { keepDisplayOn, processId });

        [McpServerTool]
        [Description("Set timed keep-awake via HTTP. Params: seconds (>0), keepDisplayOn=true|false")]
        public static string AwakeHttpTimed(uint seconds, bool keepDisplayOn = true)
        {
            if (seconds == 0)
            {
                return JsonError("seconds must be > 0");
            }

            return SendAwakeRequest("POST", "awake/timed", new { seconds, keepDisplayOn });
        }

        [McpServerTool]
        [Description("Set expirable keep-awake via HTTP. Params: expireAt (ISO 8601), keepDisplayOn=true|false")]
        public static string AwakeHttpExpirable(string expireAt, bool keepDisplayOn = true)
        {
            if (string.IsNullOrWhiteSpace(expireAt))
            {
                return JsonError("expireAt required (ISO 8601)");
            }

            return SendAwakeRequest("POST", "awake/expirable", new { expireAt, keepDisplayOn });
        }

        [McpServerTool]
        [Description("Keep PC awake during CPU-intensive tasks like building, compiling, downloading, or processing. Monitors system activity and prevents sleep while CPU/memory/network usage is above thresholds. Perfect for long-running operations. Params: cpuThresholdPercent (0-100), memThresholdPercent (0-100), netThresholdKBps (KB/s), sampleIntervalSeconds (>0), inactivityTimeoutSeconds (>0), keepDisplayOn=true|false")]
        public static string AwakeHttpActivityBased(uint cpuThresholdPercent = 50, uint memThresholdPercent = 50, uint netThresholdKBps = 10, uint sampleIntervalSeconds = 30, uint inactivityTimeoutSeconds = 300, bool keepDisplayOn = true)
        {
            if (cpuThresholdPercent > 100)
            {
                return JsonError("cpuThresholdPercent must be 0-100");
            }

            if (memThresholdPercent > 100)
            {
                return JsonError("memThresholdPercent must be 0-100");
            }

            if (sampleIntervalSeconds == 0)
            {
                return JsonError("sampleIntervalSeconds must be > 0");
            }

            if (inactivityTimeoutSeconds == 0)
            {
                return JsonError("inactivityTimeoutSeconds must be > 0");
            }

            return SendAwakeRequest("POST", "awake/activity", new
            {
                cpuThresholdPercent,
                memThresholdPercent,
                netThresholdKBps,
                sampleIntervalSeconds,
                inactivityTimeoutSeconds,
                keepDisplayOn,
            });
        }

        [McpServerTool]
        [Description("Set passive mode via HTTP (POST /awake/passive).")]
        public static string AwakeHttpPassive() => SendAwakeRequest("POST", "awake/passive");

        [McpServerTool]
        [Description("Toggle display keep-on via HTTP (POST /awake/display/toggle).")]
        public static string AwakeHttpToggleDisplay() => SendAwakeRequest("POST", "awake/display/toggle");

        [McpServerTool]
        [Description("Get Awake settings via HTTP (GET /awake/settings).")]
        public static string AwakeHttpSettings() => SendAwakeRequest("GET", "awake/settings");

        [McpServerTool]
        [Description("Check current PowerToys Awake mode and configuration. Returns active mode (indefinite, timed, activity-based, or passive), remaining time, thresholds, and display settings. Use to verify if system is being kept awake and what settings are active.")]
        public static string AwakeHttpConfig() => SendAwakeRequest("GET", "awake/config");

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
