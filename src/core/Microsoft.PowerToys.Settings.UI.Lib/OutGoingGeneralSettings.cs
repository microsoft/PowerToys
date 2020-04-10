// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class OutGoingGeneralSettings
    {
        public GeneralSettings general { get; set; }

        public OutGoingGeneralSettings()
        {
            general = null;
        }

        public OutGoingGeneralSettings(GeneralSettings generalSettings)
        {
            general = generalSettings;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
