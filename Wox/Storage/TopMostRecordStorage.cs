using System.Collections.Generic;
using System.Linq;
using Wox.Infrastructure.Storage;
using Wox.Plugin;

namespace Wox.Storage
{
    public class TopMostRecord
    {
        public Dictionary<string, Record> records = new Dictionary<string, Record>();

        internal bool IsTopMost(Result result)
        {
            return records.Any(o => o.Value.Title == result.Title
                && o.Value.SubTitle == result.SubTitle
                && o.Value.PluginID == result.PluginID
                && o.Key == result.OriginQuery.RawQuery);
        }

        internal void Remove(Result result)
        {
            if (records.ContainsKey(result.OriginQuery.RawQuery))
            {
                records.Remove(result.OriginQuery.RawQuery);
            }
        }

        internal void AddOrUpdate(Result result)
        {
            if (records.ContainsKey(result.OriginQuery.RawQuery))
            {
                records[result.OriginQuery.RawQuery].Title = result.Title;
                records[result.OriginQuery.RawQuery].SubTitle = result.SubTitle;
                records[result.OriginQuery.RawQuery].PluginID = result.PluginID;
            }
            else
            {
                records.Add(result.OriginQuery.RawQuery, new Record
                {
                    PluginID = result.PluginID,
                    Title = result.Title,
                    SubTitle = result.SubTitle
                });
            }
        }
    }


    public class Record
    {
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public string PluginID { get; set; }
    }
}
