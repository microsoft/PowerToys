// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Peek.FilePreviewer.Previewers.SQLitePreviewer.Models
{
    public class SQLiteTableInfo
    {
        public string Name { get; set; } = string.Empty;

        public List<SQLiteColumnInfo> Columns { get; set; } = new();

        public long RowCount { get; set; }

        public List<Dictionary<string, string?>> Rows { get; set; } = new();

        public override string ToString() => Name;
    }
}
