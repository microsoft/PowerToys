// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

#pragma warning disable SA1649 // File name should match first type name

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    /// <summary>
    /// Represents a powertoys module settings setnt to the runner.
    /// </summary>
    /// <typeparam name="T">A powertoys module settings class.</typeparam>
    public class SndModuleSettings<T>
    {
        public T powertoys { get; set; }

        public SndModuleSettings(T settings)
        {
            this.powertoys = settings;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
