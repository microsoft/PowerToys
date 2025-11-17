// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

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
        /// <param name="charDelayMS">Delay in milliseconds between each character. Default is 0 (no delay).</param>
        /// <returns>The current TextBox instance.</returns>
        public TextBox SetText(string value, bool clearText = true, int charDelayMS = 0)
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

            // TODO: CmdPal bug – when inputting text, characters are swallowed too quickly.
            // This should be fixed within CmdPal itself.
            // Temporary workaround: introduce a delay between character inputs to avoid the issue
            if (charDelayMS > 0 || EnvironmentConfig.IsInPipeline)
            {
                // Send text character by character with delay (if specified or in pipeline)
                PerformAction((actions, windowElement) =>
                {
                    foreach (char c in value)
                    {
                        windowElement.SendKeys(c.ToString());
                        if (charDelayMS > 0)
                        {
                            Task.Delay(charDelayMS).Wait();
                        }
                        else if (EnvironmentConfig.IsInPipeline)
                        {
                            Task.Delay(50).Wait();
                        }
                    }
                });
            }
            else
            {
                // No character delay - send all text at once (original behavior)
                PerformAction((actions, windowElement) =>
                {
                    windowElement.SendKeys(value);
                });
            }

            return this;
        }
    }
}
