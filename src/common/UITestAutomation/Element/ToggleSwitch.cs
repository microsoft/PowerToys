// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json.Linq;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Represents a ToggleSwitch in the UI test environment.
    /// </summary>
    public class ToggleSwitch : Button
    {
        /// <summary>
        /// Gets a value indicating whether the ToggleSwitch is on.
        /// </summary>
        public bool IsOn
        {
            get
            {
                return this.Selected;
            }
        }

        /// <summary>
        /// Sets the ToggleSwitch to the specified value.
        /// </summary>
        /// <param name="value">A value indicating whether the ToggleSwitch should be active. Default is true</param>
        /// <returns>The current ToggleSwitch instance.</returns>
        public ToggleSwitch Toggle(bool value = true)
        {
            if (this.IsOn != value)
            {
                // Toggle the switch
                this.Click();
            }

            return this;
        }
    }
}
