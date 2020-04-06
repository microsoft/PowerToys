// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public abstract class BasePTModuleSettings
    {
        // Gets or sets name of the powertoy module.
        public string name { get; set; }

        // Gets or sets the powertoys version.
        public string version { get; set; }

        // converts the current to a json string.
        public virtual string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
