// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Represents a TextBox in the UI test environment.
    /// TextBox represents a control that can be used to display and edit plain text (single or multi-line).
    /// </summary>
    public class TextBox : Element
    {
        private static readonly string ExpectedControlType = "ControlType.Edit";

        /// <summary>
        /// Initializes a new instance of the <see cref="TextBox"/> class.
        /// </summary>
        public TextBox()
        {
            this.TargetControlType = TextBox.ExpectedControlType;
        }

        /// <summary>
        /// Sets the text of the textbox.
        /// </summary>
        /// <param name="value">The text to set.</param>
        /// <param name="clearText">A value indicating whether to clear the text before setting it. Default value is true</param>
        /// <returns>The current TextBox instance.</returns>
        public TextBox SetText(string value, bool clearText = true, bool slowlyInput = false)
        {
            if (clearText)
            {
                PerformAction((actions, windowElement) =>
                {
                    // select all text and delete it
                    windowElement.SendKeys(OpenQA.Selenium.Keys.Control + "a");
                    windowElement.SendKeys(OpenQA.Selenium.Keys.Delete);
                });
                Task.Delay(500).Wait();
            }

            if (slowlyInput)
            {
                // split input by blanks
                var splitedValues = value.Split(' ');
                int count = 0;

                // If slowlyInput is true, we will send the keys one by one with a delay
                foreach (var str in splitedValues)
                {
                    PerformAction((actions, windowElement) =>
                    {
                        windowElement.SendKeys(str);
                        if (count != splitedValues.Length - 1)
                        {
                            windowElement.SendKeys(" ");
                        }
                    });
                    count++;
                }

                return this;
            }
            else
            {
                PerformAction((actions, windowElement) =>
                {
                    windowElement.SendKeys(value);
                });
            }

            return this;
        }
    }
}
