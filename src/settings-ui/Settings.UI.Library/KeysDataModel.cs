// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class KeysDataModel
    {
        [JsonPropertyName("originalKeys")]
        public string OriginalKeys { get; set; }

        [JsonPropertyName("newRemapKeys")]
        public string NewRemapKeys { get; set; }

        [JsonPropertyName("unicodeText")]
        public string NewRemapString { get; set; }

        [JsonPropertyName("runProgramFilePath")]
        public string RunProgramFilePath { get; set; }

        [JsonPropertyName("runProgramArgs")]
        public string RunProgramArgs { get; set; }

        [JsonPropertyName("isRunProgram")]
        public bool IsRunProgram { get; set; }

        private static List<string> MapKeys(string stringOfKeys)
        {
            if (stringOfKeys == null)
            {
                return new List<string>();
            }

            return stringOfKeys
                .Split(';')
                .Select(uint.Parse)
                .Select(Helper.GetKeyName)
                .ToList();
        }

        public List<string> GetMappedOriginalKeys()
        {
            return MapKeys(OriginalKeys);
        }

        public List<string> GetMappedNewRemapKeys()
        {
            if (IsRunProgram)
            {
                // we're going to just pretend this is a "key" if we have a RunProgramFilePath
                if (string.IsNullOrEmpty(RunProgramFilePath))
                {
                    return new List<string>();
                }
                else
                {
                    return new List<string> { FormatFakeKeyForDisplay() };
                }
            }

            return string.IsNullOrEmpty(NewRemapString) ? MapKeys(NewRemapKeys) : new List<string> { NewRemapString };
        }

        private string FormatFakeKeyForDisplay()
        {
            // was going to use this:
            // var fakeKey = Path.GetFileName(RunProgramFilePath);
            // but I like this better:
            var fakeKey = RunProgramFilePath;

            if (fakeKey.Length > 15)
            {
                fakeKey = $"...{fakeKey.Substring(fakeKey.Length - 12)}";
            }

            if (!string.IsNullOrEmpty(RunProgramArgs))
            {
                if (RunProgramArgs.Length > 10)
                {
                    fakeKey = $"{fakeKey} {RunProgramArgs.Substring(0, 7)}...";
                }
                else
                {
                    fakeKey = $"{fakeKey} {RunProgramArgs}";
                }
            }

            return fakeKey;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
