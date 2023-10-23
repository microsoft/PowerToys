// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Settings.UI.Library.Enumerations;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class EnvironmentVariablesProperties
    {
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool LaunchAdministrator { get; set; }

        public EnvironmentVariablesProperties()
        {
            LaunchAdministrator = true;
        }
    }
}
