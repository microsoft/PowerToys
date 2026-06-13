// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Peek.FilePreviewer.Previewers.SQLitePreviewer.Models
{
    public class SQLiteColumnInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public bool IsPrimaryKey { get; set; }

        public bool IsNotNull { get; set; }

        public string DisplayText
        {
            get
            {
                var sb = new StringBuilder(Name);
                if (!string.IsNullOrEmpty(Type))
                {
                    sb.Append(' ');
                    sb.Append(Type);
                }

                if (IsPrimaryKey)
                {
                    sb.Append(" (PK)");
                }
                else if (IsNotNull)
                {
                    sb.Append(" NOT NULL");
                }

                return sb.ToString();
            }
        }
    }
}
