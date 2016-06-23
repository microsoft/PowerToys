using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Wox.Plugin;

namespace Wox.Storage
{
    public class History
    {
        public List<HistoryItem> Items { get; set; } = new List<HistoryItem>();

        private int _maxHistory = 300;

        public void Add(string query)
        {
            if (string.IsNullOrEmpty(query)) return;
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
                    ExecutedDateTime = DateTime.Now
                });
            }
        }
    }
}
