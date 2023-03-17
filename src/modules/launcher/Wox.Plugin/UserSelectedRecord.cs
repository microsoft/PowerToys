// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Wox.Plugin
{
    public class UserSelectedRecord
    {
        public class UserSelectedRecordItem
        {
            public int SelectedCount { get; set; }

            public DateTime LastSelected { get; set; }

            public string IconPath { get; set; }

            public string Title { get; set; }

            public string Search { get; set; }

            public int Score { get; set; }

            public string SubTitle { get; set; }

            public string PluginID { get; set; }
        }

        [JsonInclude]
        public Dictionary<string, UserSelectedRecordItem> Records { get; private set; } = new Dictionary<string, UserSelectedRecordItem>();

        public void Remove(Result result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            Records.Remove(result.ToString());
        }

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
                Records[key].IconPath = result.IcoPath;
                Records[key].Title = result.Title;
                Records[key].SubTitle = result.SubTitle;
                Records[key].Search = (result.OriginQuery.Search.Length > 0) ? result.OriginQuery.Search : Records[key].Search;

                if (Records[key].PluginID == null)
                {
                    Records[key].PluginID = result.PluginID;
                }
            }
            else
            {
                Records.Add(key, new UserSelectedRecordItem
                {
                    SelectedCount = 1,
                    LastSelected = DateTime.UtcNow,
                    Title = result.Title,
                    SubTitle = result.SubTitle,
                    IconPath = result.IcoPath,
                    PluginID = result.PluginID,
                    Search = result.OriginQuery.Search,
                });
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

        public Dictionary<string, UserSelectedRecordItem> GetGenericHistory()
        {
            /*
            var history = new List<UserSelectedRecord.UserSelectedRecordItem>();

            foreach (var record in Records)
            {
                if (record.Value.PluginID == null)
                {
                    continue;
                }

                history.Add(new UserSelectedRecordItem
                {
                    SelectedCount = record.Value.SelectedCount,
                    LastSelected = record.Value.LastSelected,
                    IconPath = record.Value.IconPath,
                    Title = record.Value.Title,
                    Score = record.Value.Score,
                    SubTitle = record.Value.SubTitle,
                    PluginID = record.Value.PluginID,
                    Search = record.Value.Search,
                });
            }

            return history;
            */

            return Records;
        }
    }
}
