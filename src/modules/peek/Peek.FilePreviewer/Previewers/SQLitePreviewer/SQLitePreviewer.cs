// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Dispatching;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Interfaces;
using Peek.FilePreviewer.Previewers.SQLitePreviewer.Models;
using Windows.Foundation;

namespace Peek.FilePreviewer.Previewers.SQLitePreviewer
{
    public partial class SQLitePreviewer : ObservableObject, ISQLitePreviewer
    {
        [ObservableProperty]
        private PreviewState _state;

        [ObservableProperty]
        private string? _tableCountText;

        public ObservableCollection<SQLiteTableInfo> Tables { get; } = new();

        private IFileSystemItem Item { get; }

        private DispatcherQueue Dispatcher { get; }

        private static readonly HashSet<string> _supportedFileTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ".db", ".sqlite", ".sqlite3",
        };

        public SQLitePreviewer(IFileSystemItem file)
        {
            Item = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public static bool IsItemSupported(IFileSystemItem item)
        {
            return _supportedFileTypes.Contains(item.Extension);
        }

        public Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            var size = new Size(800, 500);
            return Task.FromResult(new PreviewSize { MonitorSize = size, UseEffectivePixels = true });
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            State = PreviewState.Loading;

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = Item.Path,
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var tableNames = new List<string>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    tableNames.Add(reader.GetString(0));
                }
            }

            foreach (var tableName in tableNames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tableInfo = new SQLiteTableInfo { Name = tableName };

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA table_info([{tableName}]);";
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        tableInfo.Columns.Add(new SQLiteColumnInfo
                        {
                            Name = reader.GetString(1),
                            Type = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            IsNotNull = reader.GetInt32(3) == 1,
                            IsPrimaryKey = reader.GetInt32(5) > 0,
                        });
                    }
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"SELECT COUNT(*) FROM [{tableName}];";
                    tableInfo.RowCount = (long)(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"SELECT * FROM [{tableName}] LIMIT 200;";
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var row = new Dictionary<string, string?>(reader.FieldCount, StringComparer.Ordinal);
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
                        }

                        tableInfo.Rows.Add(row);
                    }
                }

                await Dispatcher.RunOnUiThread(() => Tables.Add(tableInfo));
            }

            TableCountText = string.Format(
                CultureInfo.CurrentCulture,
                ResourceLoaderInstance.ResourceLoader.GetString("SQLite_Table_Count"),
                tableNames.Count);

            State = PreviewState.Loaded;
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageItem = await Item.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
