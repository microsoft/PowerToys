// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Wox.Plugin;

namespace PowerLauncher.Storage
{
    public class UserSelectedRecord
    {
        [JsonInclude]
        public Dictionary<string, int> Records { get; private set; } = new Dictionary<string, int>();

        public void Add(Result result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var key = result.ToString();
            if (Records.TryGetValue(key, out int value))
            {
                Records[key] = value + 1;
            }
            else
            {
                Records.Add(key, 1);
            }
        }

        public int GetSelectedCount(Result result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result != null && Records.TryGetValue(result.ToString(), out int value))
            {
                return value;
            }

            return 0;
        }
    }
}
