// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Represents a button in the UI test environment.
    /// </summary>
    public class Button : Element
    {
        private static readonly string ExpectedControlType = "ControlType.Button";

        /// <summary>
        /// Initializes a new instance of the <see cref="Button"/> class.
        /// </summary>
        public Button()
        {
            this.TargetControlType = Button.ExpectedControlType;
        }
    }
}
