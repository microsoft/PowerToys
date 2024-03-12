// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class SndFileActionsMenuSettings
    {
        [JsonPropertyName("FileActionsMenu")]
        public FileActionsMenuSettings FileActionsMenu { get; set; }

        public SndFileActionsMenuSettings()
        {
        }

        public SndFileActionsMenuSettings(FileActionsMenuSettings settings)
        {
            FileActionsMenu = settings;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
