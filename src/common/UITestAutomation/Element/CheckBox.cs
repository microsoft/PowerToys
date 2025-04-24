// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Represents a CheckBox in the UI test environment.
    /// </summary>
    public class CheckBox : Element
    {
        private static readonly string ExpectedControlType = "ControlType.CheckBox";

        /// Initializes a new instance of the <see cref="CheckBox"/> class.
        public CheckBox()
        {
            this.TargetControlType = ExpectedControlType;
        }

        /// <summary>
        /// Gets a value indicating whether the CheckBox is checked.
        /// </summary>
        public bool IsChecked => this.Selected;

        public CheckBox SetCheck(bool value = true, int msPreAction = 500, int msPostAction = 500)
        {
            if (this.IsChecked != value)
            {
                if (msPreAction > 0)
                {
                    Task.Delay(msPreAction).Wait();
                }

                // Toggle the switch
                this.Click();
                if (msPostAction > 0)
                {
                    Task.Delay(msPostAction).Wait();
                }
            }

            return this;
        }
    }
}
