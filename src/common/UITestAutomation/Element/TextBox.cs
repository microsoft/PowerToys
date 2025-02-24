// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using OpenQA.Selenium;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Represents a textbox in the UI test environment.
    /// </summary>
    public class TextBox : Element
    {
        /// <summary>
        /// Sets the text of the textbox.
        /// </summary>
        /// <param name="value">The text to set.</param>
        /// <param name="clearText">A value indicating whether to clear the text before setting it. Default value is true</param>
        /// <returns>The current TextBox instance.</returns>
        public TextBox SetText(string value, bool clearText = true)
        {
            if (clearText)
            {
                PerformAction((actions, windowElement) =>
                {
                    // select all text and delete it
                    windowElement.SendKeys(Keys.Control + "a");
                    windowElement.SendKeys(Keys.Delete);
                });
            }

            PerformAction((actions, windowElement) =>
            {
                windowElement.SendKeys(value);
            });

            return this;
        }
    }
}
