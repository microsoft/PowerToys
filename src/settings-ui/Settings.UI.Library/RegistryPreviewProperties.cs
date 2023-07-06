// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class RegistryPreviewProperties
    {
        public RegistryPreviewProperties()
        {
            DefaultRegApp = false;
        }

        [JsonPropertyName("default_reg_app")]
        public bool DefaultRegApp { get; set; }
    }
}
