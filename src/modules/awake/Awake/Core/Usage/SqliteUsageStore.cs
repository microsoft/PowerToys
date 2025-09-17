// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1516, SA1210, SA1636

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Awake.Core.Usage.Models;
using ManagedCommon;
using Microsoft.Data.Sqlite;

namespace Awake.Core.Usage
{
    internal sealed class SqliteUsageStore : IUsageStore
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public SqliteUsageStore(string dbPath)
        {
            _dbPath = dbPath;
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
            }.ToString();
            Initialize();
        }

        private void Initialize()
        {
            using SqliteConnection conn = new(_connectionString);
            conn.Open();
            using SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS process_usage (
                process_name TEXT NOT NULL,
                day_utc TEXT NOT NULL,
                total_seconds REAL NOT NULL,
                first_seen_utc TEXT NOT NULL,
                last_updated_utc TEXT NOT NULL,
                PRIMARY KEY(process_name, day_utc)
            );";
            cmd.ExecuteNonQuery();
        }

        public void AddSpan(string processName, double seconds, DateTime firstSeenUtc, DateTime lastUpdatedUtc, int retentionDays)
        {
            if (seconds <= 0)
            {
                return;
            }

            string day = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            using SqliteConnection conn = new(_connectionString);
            conn.Open();
            using SqliteTransaction tx = conn.BeginTransaction();
            using (SqliteCommand cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO process_usage(process_name, day_utc, total_seconds, first_seen_utc, last_updated_utc)
VALUES($p,$d,$s,$f,$l)
ON CONFLICT(process_name,day_utc) DO UPDATE SET 
  total_seconds = total_seconds + excluded.total_seconds,
  last_updated_utc = excluded.last_updated_utc;";
                cmd.Parameters.AddWithValue("$p", processName);
                cmd.Parameters.AddWithValue("$d", day);
                cmd.Parameters.AddWithValue("$s", seconds);
                cmd.Parameters.AddWithValue("$f", firstSeenUtc.ToString("o"));
                cmd.Parameters.AddWithValue("$l", lastUpdatedUtc.ToString("o"));
                cmd.ExecuteNonQuery();
            }

            using (SqliteCommand prune = conn.CreateCommand())
            {
                prune.Transaction = tx;
                prune.CommandText = @"DELETE FROM process_usage WHERE day_utc < date('now', @retention);";
                prune.Parameters.AddWithValue("@retention", $"-{Math.Max(1, retentionDays)} days");
                prune.ExecuteNonQuery();
            }

            tx.Commit();
        }

        public IReadOnlyList<AppUsageRecord> Query(int top, int days)
        {
            List<AppUsageRecord> result = new();
            int safeDays = Math.Max(1, days);
            using SqliteConnection conn = new(_connectionString);
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
            using SqliteDataReader reader = cmd.ExecuteReader(CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                try
                {
                    string name = reader.GetString(0);
                    double secs = reader.GetDouble(1);
                    DateTime first = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind);
                    DateTime last = DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind);
                    result.Add(new AppUsageRecord
                    {
                        ProcessName = name,
                        TotalSeconds = secs,
                        FirstSeenUtc = first,
                        LastUpdatedUtc = last,
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("[AwakeUsage][SQLite] Row parse failed: " + ex.Message);
                }
            }

            return result;
        }

        public void Prune(int retentionDays)
        {
            using SqliteConnection conn = new(_connectionString);
            conn.Open();
            using SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM process_usage WHERE day_utc < date('now', @cutoff);";
            cmd.Parameters.AddWithValue("@cutoff", $"-{Math.Max(1, retentionDays)} days");
            cmd.ExecuteNonQuery();
        }

        public void Dispose()
        {
        }
    }
}

#pragma warning restore SA1516, SA1210, SA1636
