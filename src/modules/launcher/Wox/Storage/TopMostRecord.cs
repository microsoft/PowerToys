using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Wox.Plugin;

namespace Wox.Storage
{
    // todo this class is not thread safe.... but used from multiple threads.
    public class TopMostRecord
    {
        [JsonProperty]
        private Dictionary<string, Record> records = new Dictionary<string, Record>();

        internal bool IsTopMost(Result result)
        {
            if (records.Count == 0)
            {
                return false;
            }

            // since this dictionary should be very small (or empty) going over it should be pretty fast. 
            return records.Any(o => o.Value.Title == result.Title
                                     && o.Value.SubTitle == result.SubTitle
                                     && o.Value.PluginID == result.PluginID
                                     && o.Key == result.OriginQuery.RawQuery);
        }

        internal void Remove(Result result)
        {
            records.Remove(result.OriginQuery.RawQuery);
        }

        internal void AddOrUpdate(Result result)
        {
            var record = new Record
            {
                PluginID = result.PluginID,
                Title = result.Title,
                SubTitle = result.SubTitle
            };
            records[result.OriginQuery.RawQuery] = record;

        }

        public void Load(Dictionary<string, Record> dictionary)
        {
            records = dictionary;
        }
    }


    public class Record
    {
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public string PluginID { get; set; }
    }
}
