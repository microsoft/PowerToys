// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest
{
    public class CheckBox : Element
    {
        private static readonly string ExpectedControlType = "ControlType.CheckBox";

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckBox"/> class.
        /// </summary>
        public CheckBox()
        {
            this.TargetControlType = CheckBox.ExpectedControlType;
        }

        /// <summary>
        /// Select the item of the ComboBox.
        /// </summary>
        /// <param name="value">The text to select from the list view.</param>
        public void Select(string value)
        {
            this.Find<NavigationViewItem>(value).Click();
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
