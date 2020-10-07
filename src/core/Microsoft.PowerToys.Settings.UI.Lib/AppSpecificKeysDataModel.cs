// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
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

            // Using InvariantCulture for internal text
            return OriginalKeys.Equals(arg.OriginalKeys, System.StringComparison.InvariantCulture) &&
                NewRemapKeys.Equals(arg.NewRemapKeys, System.StringComparison.InvariantCulture) &&
                TargetApp.Equals(arg.TargetApp, System.StringComparison.InvariantCulture);
        }
    }
}
