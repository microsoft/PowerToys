// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerAccent.Core.Models;
using PowerAccent.Core.SerializationContext;

namespace PowerAccent.Core.Tools
{
    public class CharactersUsageInfo
    {
        private readonly string _filePath;
        private readonly Dictionary<string, uint> _characterUsageCounters;
        private readonly Dictionary<string, long> _characterUsageTimestamp;

        public CharactersUsageInfo()
        {
            _filePath = new SettingsUtils().GetSettingsFilePath(PowerAccentSettings.ModuleName, "UsageInfo.json");
            var data = GetUsageInfoData();
            _characterUsageCounters = data.CharacterUsageCounters;
            _characterUsageTimestamp = data.CharacterUsageTimestamp;
        }

        public bool Empty()
        {
            return _characterUsageCounters.Count == 0;
        }

        public void Clear()
        {
            _characterUsageCounters.Clear();
            _characterUsageTimestamp.Clear();
            Delete();
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

        public void IncrementUsageFrequency(string character)
        {
            if (_characterUsageCounters.TryGetValue(character, out uint currentCount))
            {
                _characterUsageCounters[character] = currentCount + 1;
            }
            else
            {
                _characterUsageCounters[character] = 1;
            }

            _characterUsageTimestamp[character] = DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        public void Save()
        {
            var data = new UsageInfoData
            {
                CharacterUsageCounters = _characterUsageCounters,
                CharacterUsageTimestamp = _characterUsageTimestamp,
            };

            var json = JsonSerializer.Serialize(data, SourceGenerationContext.Default.UsageInfoData);
            File.WriteAllText(_filePath, json);
        }

        public void Delete()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        private UsageInfoData GetUsageInfoData()
        {
            if (!File.Exists(_filePath))
            {
                return new UsageInfoData();
            }

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize(json, SourceGenerationContext.Default.UsageInfoData);
        }
    }
}
