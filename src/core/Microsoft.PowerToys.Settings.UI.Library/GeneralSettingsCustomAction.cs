// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class GeneralSettingsCustomAction
    {
        [JsonPropertyName("action")]
        public OutGoingGeneralSettings GeneralSettingsAction { get; set; }

        public GeneralSettingsCustomAction()
        {
        }

        public GeneralSettingsCustomAction(OutGoingGeneralSettings action)
        {
            GeneralSettingsAction = action;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
