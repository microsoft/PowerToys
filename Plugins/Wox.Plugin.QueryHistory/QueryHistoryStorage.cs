using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Wox.Core.Exception;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.QueryHistory
{
    public class QueryHistoryStorage : JsonStrorage<QueryHistoryStorage>
    {
        [JsonProperty]
        private List<HistoryItem> History = new List<HistoryItem>();

        private int MaxHistory = 300;
        private int cursor = 0;

        protected override string ConfigFolder
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
        }

        protected override string ConfigName
        {
            get { return "QueryHistory"; }
        }

        public HistoryItem Pop()
        {
            if (History.Count == 0) return null;

            if (cursor > History.Count - 1)
            {
                cursor = History.Count - 1;
            }
            if (cursor < 0)
            {
                cursor = 0;
            }

            return History[cursor--];
        }

        public void Reset()
        {
            cursor = History.Count - 1;
        }

        public void Add(string query)
        {
            if (string.IsNullOrEmpty(query)) return;
            if (History.Count > MaxHistory)
            {
                History.RemoveAt(0);
            }

            if (History.Count > 0 && History.Last().Query == query)
            {
                History.Last().ExecutedDateTime = DateTime.Now;
            }
            else
            {
                History.Add(new HistoryItem()
                {
                    Query = query,
                    ExecutedDateTime = DateTime.Now
                });
            }

            if (History.Count % 5 == 0)
            {
                Save();
            }
        }

        public List<HistoryItem> GetHistory()
        {
            return History.OrderByDescending(o => o.ExecutedDateTime).ToList();
        }
    }
}
