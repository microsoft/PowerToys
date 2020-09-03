// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Wox.Plugin;

namespace PowerLauncher.Storage
{
    public class UserSelectedRecord
    {
        [JsonProperty]
        private readonly Dictionary<string, int> records = new Dictionary<string, int>();

        public void Add(Result result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

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
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result != null && records.TryGetValue(result.ToString(), out int value))
            {
                return value;
            }

            return 0;
        }
    }
}
