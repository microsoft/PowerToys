// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class AppSpecificKeysDataModel : KeysDataModel
    {
        [JsonPropertyName("targetApp")]
        public string TargetApp { get; set; }

        public new List<string> GetMappedOriginalKeys()
        {
            return base.GetMappedOriginalKeys();
        }

        public new List<string> GetMappedOriginalKeysWithSplitChord()
        {
            return base.GetMappedOriginalKeysWithSplitChord();
        }

        public List<string> GetMappedOriginalKeys(bool ignoreSecondKeyInChord)
        {
            return base.GetMappedOriginalKeys(ignoreSecondKeyInChord);
        }

        public List<string> GetMappedOriginalKeysWithoutChord()
        {
            return base.GetMappedOriginalKeys(true);
        }

        public new List<string> GetMappedOriginalKeysOnlyChord()
        {
            return base.GetMappedOriginalKeysOnlyChord();
        }

        public new List<string> GetMappedNewRemapKeys(int runProgramMaxLength)
        {
            return base.GetMappedNewRemapKeys(runProgramMaxLength);
        }

        public bool Compare(AppSpecificKeysDataModel arg)
        {
            ArgumentNullException.ThrowIfNull(arg);

            // Using Ordinal comparison for internal text
            return string.Equals(OriginalKeys, arg.OriginalKeys, StringComparison.Ordinal) &&
                   string.Equals(NewRemapKeys, arg.NewRemapKeys, StringComparison.Ordinal) &&
                   string.Equals(NewRemapString, arg.NewRemapString, StringComparison.Ordinal) &&
                   string.Equals(TargetApp, arg.TargetApp, StringComparison.Ordinal);
        }
    }
}
