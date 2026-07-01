// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Peek.FilePreviewer.Previewers.SQLitePreviewer.Models;

namespace Peek.FilePreviewer.Previewers.SQLitePreviewer.Helpers
{
    internal static class SQLiteHelpers
    {
        internal static string QuoteIdentifier(string identifier)
        {
            return $"\"{identifier.Replace("\"", "\"\"")}\"";
        }

        internal static void AssignBindingKeys(List<SQLiteColumnInfo> columns)
        {
            var usedKeys = new HashSet<string>();
            foreach (var col in columns)
            {
                // Replace special characters to make it a valid PropertyPath indexer key
                string baseKey = col.Name.Replace(".", "_")
                                         .Replace("[", "_")
                                         .Replace("]", "_")
                                         .Replace("/", "_");

                if (string.IsNullOrEmpty(baseKey))
                {
                    baseKey = "Column";
                }

                string key = baseKey;
                int suffix = 2;
                while (!usedKeys.Add(key))
                {
                    key = $"{baseKey}_{suffix}";
                    suffix++;
                }

                col.BindingKey = key;
            }
        }
    }
}
