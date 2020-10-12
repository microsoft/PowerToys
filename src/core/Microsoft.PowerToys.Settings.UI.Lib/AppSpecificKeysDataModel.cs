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

        public new List<string> GetMappedNewRemapKeys()
        {
            return base.GetMappedNewRemapKeys();
        }

        public bool Compare(AppSpecificKeysDataModel arg)
        {
            if (arg == null)
            {
                throw new ArgumentNullException(nameof(arg));
            }

            // Using Ordinal comparison for internal text
            return OriginalKeys.Equals(arg.OriginalKeys, StringComparison.Ordinal) &&
                NewRemapKeys.Equals(arg.NewRemapKeys, StringComparison.Ordinal) &&
                TargetApp.Equals(arg.TargetApp, StringComparison.Ordinal);
        }
    }
}
