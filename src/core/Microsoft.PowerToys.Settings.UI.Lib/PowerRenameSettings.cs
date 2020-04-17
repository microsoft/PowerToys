// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerRenameSettings : BasePTModuleSettings
    {
        public PowerRenameProperties properties { get; set; }

        public PowerRenameSettings()
        {
            properties = new PowerRenameProperties();
            version = "1";
            name = "PowerRename";
        }

        public PowerRenameSettings(PowerRenameLocalProperties localProperties)
        {
            properties = new PowerRenameProperties();
            properties.PersistState.Value = localProperties.PersistState;
            properties.MRUEnabled.Value = localProperties.MRUEnabled;
            properties.MaxMRUSize.Value = localProperties.MaxMRUSize;
            properties.ShowIcon.Value = localProperties.ShowIcon;
            properties.ExtendedContextMenuOnly.Value = localProperties.ExtendedContextMenuOnly;

            version = "1";
            name = "PowerRename";
        }

        public PowerRenameSettings(string ptName)
        {
            properties = new PowerRenameProperties();
            version = "1";
            name = ptName;
        }

        public override string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
