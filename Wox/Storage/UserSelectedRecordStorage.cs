using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using System.IO;

namespace Wox.Storage
{
    public class UserSelectedRecordStorage : JsonStrorage<UserSelectedRecordStorage>
    {
        [JsonProperty]
        private Dictionary<string, int> records = new Dictionary<string, int>();

        protected override string ConfigFolder
        {
            get
            {
                string userProfilePath = Environment.GetEnvironmentVariable("USERPROFILE");
                if (userProfilePath == null)
                {
                    throw new ArgumentException("Environment variable USERPROFILE is empty");
                }
                return Path.Combine(Path.Combine(userProfilePath, ".Wox"), "Config");
            }
        }

        protected override string ConfigName
        {
            get { return "UserSelectedRecords"; }
        }

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
