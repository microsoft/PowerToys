// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Wox.Plugin;

namespace PowerLauncher.Storage
{
    public class UserSelectedRecord
    {
        public class UserSelectedRecordItem
        {
            public int SelectedCount { get; set; }

            public DateTime LastSelected { get; set; }
        }

        [JsonInclude]
        public Dictionary<string, UserSelectedRecordItem> Records { get; private set; } = new Dictionary<string, UserSelectedRecordItem>();

        public void Add(Result result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var key = result.ToString();
            if (Records.TryGetValue(key, out var value))
            {
                Records[key].SelectedCount = Records[key].SelectedCount + 1;
                Records[key].LastSelected = DateTime.UtcNow;
            }
            else
            {
                Records.Add(key, new UserSelectedRecordItem { SelectedCount = 1, LastSelected = DateTime.UtcNow });
            }
        }

        public UserSelectedRecordItem GetSelectedData(Result result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result != null && Records.TryGetValue(result.ToString(), out var value))
            {
                return value;
            }

            return new UserSelectedRecordItem { SelectedCount = 0, LastSelected = DateTime.MinValue };
        }
    }
}
