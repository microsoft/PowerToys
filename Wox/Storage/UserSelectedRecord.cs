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
            var key = result.ToString();
            if (records.TryGetValue(key, out int value))
            {
                records[key] = value + 1;
            }
            else
            {
                records.Add(key, 1);

            }
        }

        public int GetSelectedCount(Result result)
        {
            if (records.TryGetValue(result.ToString(), out int value))
            {
                return value;
            }
            return 0;
        }
    }
}
