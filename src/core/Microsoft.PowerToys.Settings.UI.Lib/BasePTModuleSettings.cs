// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public abstract class BasePTModuleSettings
    {
        /// <summary>
        /// Gets or sets name of the powertoy module.
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// Gets or sets the powertoys version.
        /// </summary>
        public string version { get; set; }

        /// <summary>
        /// converts the current to a json string.
        /// </summary>
        /// <returns>returnns a json string version of the class.</returns>
        public virtual string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
