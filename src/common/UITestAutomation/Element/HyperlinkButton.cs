// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Represents a HyperLinkButton in the UI test environment.
    /// HyperLinkButton represents a button control that functions as a hyperlink.
    /// </summary>
    public class HyperlinkButton : Button
    {
        private static readonly string ExpectedControlType = "ControlType.HyperLink";

        /// <summary>
        /// Initializes a new instance of the <see cref="HyperlinkButton"/> class.
        /// </summary>
        public HyperlinkButton()
        {
            this.TargetControlType = HyperlinkButton.ExpectedControlType;
        }
    }
}
