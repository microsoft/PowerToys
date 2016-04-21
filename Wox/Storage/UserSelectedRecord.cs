using System.Collections.Generic;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;
using Wox.Plugin;

namespace Wox.Storage
{
    public class UserSelectedRecord
    {
        [JsonProperty]
        private Dictionary<string, int> records = new Dictionary<string, int>();

        public void Add(Result result)
        {
            if (records.ContainsKey(result.ToString()))
            {
                records[result.ToString()] += 1;
            }
            else
            {
                records.Add(result.ToString(), 1);
            }
        }

        public int GetSelectedCount(Result result)
        {
            if (records.ContainsKey(result.ToString()))
            {
                return records[result.ToString()];
            }
            return 0;
        }
    }
}
