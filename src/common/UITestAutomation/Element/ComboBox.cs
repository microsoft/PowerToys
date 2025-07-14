﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest
{
    public class ComboBox : Element
    {
        private static readonly string ExpectedControlType = "ControlType.ComboBox";

        /// <summary>
        /// Initializes a new instance of the <see cref="ComboBox"/> class.
        /// </summary>
        public ComboBox()
        {
            this.TargetControlType = ComboBox.ExpectedControlType;
        }

        /// <summary>
        /// Select the item of the ComboBox.
        /// </summary>
        /// <param name="value">The text to select from the list view.</param>
        public void Select(string value)
        {
            this.Find<NavigationViewItem>(value).Click();
        }
    }
}
