// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Peek.FilePreviewer.Previewers.SqlitePreviewer.Models
{
    public class SqliteTableInfo
    {
        public string Name { get; set; } = string.Empty;

        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This database snapshot DTO allows the reader to supply and replace column and row collections.")]
        public List<SqliteColumnInfo> Columns { get; set; } = new();

        public long RowCount { get; set; }

        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This database snapshot DTO allows the reader to supply and replace column and row collections.")]
        public List<Dictionary<string, string?>> Rows { get; set; } = new();

        public override string ToString() => Name;
    }
}
