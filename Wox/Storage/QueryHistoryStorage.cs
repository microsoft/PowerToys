using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;
using Wox.Plugin;

namespace Wox.Storage
{
    public class QueryHistoryStorage : JsonStrorage<QueryHistoryStorage>
    {
        [JsonProperty]
        private List<HistoryItem> History = new List<HistoryItem>();

        private int MaxHistory = 300;
        private int cursor;

        public static PluginMetadata MetaData { get; } = new PluginMetadata
            { ID = "Query history", Name = "Query history" };

        protected override string FileName { get; } = "QueryHistory";

        public HistoryItem Previous()
        {
            if (History.Count == 0 || cursor == 0) return null;
            return History[--cursor];
        }

        public HistoryItem Next()
        {
            if (History.Count == 0 || cursor >= History.Count - 1) return null;
            return History[++cursor];
        }

        public void Reset()
        {
            cursor = History.Count;
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
                History.Add(new HistoryItem
                {
                    Query = query,
                    ExecutedDateTime = DateTime.Now
                });
            }

            if (History.Count % 5 == 0)
            {
                Save();
            }

            Reset();
        }

        public List<HistoryItem> GetHistory()
        {
            return History.OrderByDescending(o => o.ExecutedDateTime).ToList();
        }
    }

    public class HistoryItem
    {
        public string Query { get; set; }
        public DateTime ExecutedDateTime { get; set; }

        public string GetTimeAgo()
        {
            return DateTimeAgo(ExecutedDateTime);
        }

        private string DateTimeAgo(DateTime dt)
        {
            TimeSpan span = DateTime.Now - dt;
            if (span.Days > 365)
            {
                int years = (span.Days / 365);
                if (span.Days % 365 != 0)
                    years += 1;
                return String.Format("about {0} {1} ago",
                years, years == 1 ? "year" : "years");
            }
            if (span.Days > 30)
            {
                int months = (span.Days / 30);
                if (span.Days % 31 != 0)
                    months += 1;
                return String.Format("about {0} {1} ago",
                months, months == 1 ? "month" : "months");
            }
            if (span.Days > 0)
                return String.Format("about {0} {1} ago",
                span.Days, span.Days == 1 ? "day" : "days");
            if (span.Hours > 0)
                return String.Format("about {0} {1} ago",
                span.Hours, span.Hours == 1 ? "hour" : "hours");
            if (span.Minutes > 0)
                return String.Format("about {0} {1} ago",
                span.Minutes, span.Minutes == 1 ? "minute" : "minutes");
            if (span.Seconds > 5)
                return String.Format("about {0} seconds ago", span.Seconds);
            if (span.Seconds <= 5)
                return "just now";
            return string.Empty;
        }
    }
}
