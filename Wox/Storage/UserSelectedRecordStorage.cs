using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;
using Wox.Plugin;

namespace Wox.Storage
{
    public class UserSelectedRecordStorage : JsonStrorage<UserSelectedRecordStorage>
    {
        [JsonProperty]
        private Dictionary<string, int> records = new Dictionary<string, int>();

        protected override string FileName { get; } = "UserSelectedRecords";

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
            Save();
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
