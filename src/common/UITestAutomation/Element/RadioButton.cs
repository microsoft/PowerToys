// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Represents a radio button UI element in the application.
    /// </summary>
    public class RadioButton : Element
    {
        private static readonly string ExpectedControlType = "ControlType.RadioButton";

        /// <summary>
        /// Initializes a new instance of the <see cref="RadioButton"/> class.
        /// </summary>
        public RadioButton()
        {
            this.TargetControlType = RadioButton.ExpectedControlType;
        }

        /// <summary>
        /// Gets a value indicating whether the RadioButton is selected.
        /// </summary>
        public bool IsSelected => this.Selected;

        /// <summary>
        /// Select the RadioButton.
        /// </summary>
        public void Select()
        {
            if (!this.IsSelected)
            {
                this.Click();
            }
        }
    }
}
