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
        private static readonly string ExpectedControlType = "ControlType.Button";

        /// Initializes a new instance of the <see cref="CheckBox"/> class.
        public CheckBox()
        {
            this.TargetControlType = CheckBox.ExpectedControlType;
        }

        /// <summary>
        /// Gets a value indicating whether the CheckBox is checked.
        /// </summary>
        public bool IsChecked => this.Selected;

        /// <summary>
        /// Checks the CheckBox if it is not already checked.
        /// </summary>
        public void Check()
        {
            if (!IsChecked)
            {
                this.Click();
            }
        }

        /// <summary>
        /// Unchecks the CheckBox if it is currently checked.
        /// </summary>
        public void Uncheck()
        {
            if (IsChecked)
            {
                this.Click();
            }
        }

        /// <summary>
        /// Toggles the CheckBox (check if unchecked, uncheck if checked).
        /// </summary>
        public void Toggle()
        {
            this.Click();
        }

        /// <summary>
        /// Sets the CheckBox to a specific checked state.
        /// </summary>
        /// <param name="value">True to check, false to uncheck.</param>
        public void SetChecked(bool value)
        {
            if (value)
            {
                this.Check();
            }
            else
            {
                this.Uncheck();
            }
        }
    }
}
