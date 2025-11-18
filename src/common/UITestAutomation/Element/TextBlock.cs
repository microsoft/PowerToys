// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Represents a TextBlock in the UI test environment.
    /// TextBlock provides a lightweight control for displaying small amounts of flow content.
    /// </summary>
    public class TextBlock : Element
    {
        private static readonly string ExpectedControlType = "ControlType.Text";

        /// <summary>
        /// Initializes a new instance of the <see cref="TextBlock"/> class.
        /// </summary>
        public TextBlock()
        {
            this.TargetControlType = TextBlock.ExpectedControlType;
        }
    }
}
