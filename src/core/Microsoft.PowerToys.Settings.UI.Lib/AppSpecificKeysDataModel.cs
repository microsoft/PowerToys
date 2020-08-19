// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class AppSpecificKeysDataModel : KeysDataModel
    {
        [JsonPropertyName("targetApp")]
        public string TargetApp { get; set; }

        public new List<string> GetOriginalKeys()
        {
            return base.GetOriginalKeys();
        }

        public new List<string> GetNewRemapKeys()
        {
            return base.GetNewRemapKeys();
        }

        public bool Compare(AppSpecificKeysDataModel arg)
        {
            return OriginalKeys.Equals(arg.OriginalKeys) && NewRemapKeys.Equals(arg.NewRemapKeys) && TargetApp.Equals(arg.TargetApp);
        }
    }
}
