// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerAccent.Core.Models
{
    public class UsageInfoData
    {
        public Dictionary<string, uint> CharacterUsageCounters { get; set; } = [];

        public Dictionary<string, long> CharacterUsageTimestamp { get; set; } = [];
    }
}
