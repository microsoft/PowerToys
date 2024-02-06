// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace PowerLauncher.Storage
{
    public class QueryHistory
    {
        [JsonInclude]
        public List<HistoryItem> Items { get; private set; } = new List<HistoryItem>();

        private readonly int _maxHistory = 300;

        public void Add(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return;
            }

            if (Items.Count > _maxHistory)
            {
                Items.RemoveAt(0);
            }

            if (Items.Count > 0 && Items.Last().Query == query)
            {
                Items.Last().ExecutedDateTime = DateTime.Now;
            }
            else
            {
                Items.Add(new HistoryItem
                {
                    Query = query,
                    ExecutedDateTime = DateTime.Now,
                });
            }
        }

        public void Update()
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrEmpty(Items[i].Query))
                {
                    Items.RemoveAt(i);
                }
                else
                {
                    if (Items[i].ExecutedDateTime == DateTime.MinValue)
                    {
                        Items[i].ExecutedDateTime = DateTime.Now;
                    }
                }
            }
        }
    }
}
