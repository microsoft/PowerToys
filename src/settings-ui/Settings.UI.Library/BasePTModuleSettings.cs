// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public abstract class BasePTModuleSettings
    {
        // Gets or sets name of the powertoy module.
        [JsonPropertyName("name")]
        public string Name { get; set; }

        // Gets or sets the powertoys version.
        [JsonPropertyName("version")]
        public string Version { get; set; }

        // converts the current to a json string.
        public virtual string ToJsonString()
        {
            // By default JsonSerializer will only serialize the properties in the base class. This can be avoided by passing the object type (more details at https://stackoverflow.com/a/62498888)
            return JsonSerializer.Serialize(this, GetType());
        }

        public override int GetHashCode()
        {
            return ToJsonString().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var settings = obj as BasePTModuleSettings;
            return settings?.ToJsonString() == ToJsonString();
        }
    }
}
