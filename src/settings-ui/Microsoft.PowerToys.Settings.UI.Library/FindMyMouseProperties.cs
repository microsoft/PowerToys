// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class FindMyMouseProperties
    {
        [JsonPropertyName("do_not_activate_on_game_mode")]
        public BoolProperty DoNotActivateOnGameMode { get; set; }

        public FindMyMouseProperties()
        {
            DoNotActivateOnGameMode = new BoolProperty(true);
        }
    }
}
