// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerAccent.Core.Tools
{
    public class CharactersUsageInfo
    {
        private Dictionary<string, uint> _characterUsageCounters = new Dictionary<string, uint>();
        private Dictionary<string, long> _characterUsageTimestamp = new Dictionary<string, long>();

        public bool Empty()
        {
            return _characterUsageCounters.Count == 0;
        }

        public void Clear()
        {
            _characterUsageCounters.Clear();
            _characterUsageTimestamp.Clear();
        }

        public uint GetUsageFrequency(string character)
        {
            _characterUsageCounters.TryGetValue(character, out uint frequency);

            return frequency;
        }

        public long GetLastUsageTimestamp(string character)
        {
            _characterUsageTimestamp.TryGetValue(character, out long timestamp);

            return timestamp;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1854:Prefer the 'IDictionary.TryGetValue(TKey, out TValue)' method", Justification = "False positive: https://github.com/dotnet/roslyn-analyzers/issues/6390")]
        public void IncrementUsageFrequency(string character)
        {
            if (_characterUsageCounters.ContainsKey(character))
            {
                _characterUsageCounters[character]++;
            }
            else
            {
                _characterUsageCounters.Add(character, 1);
            }

            _characterUsageTimestamp[character] = DateTimeOffset.Now.ToUnixTimeSeconds();
        }
    }
}
