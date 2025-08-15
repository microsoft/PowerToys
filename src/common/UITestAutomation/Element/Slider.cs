// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;

namespace Microsoft.PowerToys.UITest
{
    public class Slider : Element
    {
        private static readonly string ExpectedControlType = "ControlType.Slider";

        /// <summary>
        /// Initializes a new instance of the <see cref="Slider"/> class.
        /// </summary>
        public Slider()
        {
            this.TargetControlType = Slider.ExpectedControlType;
        }

        /// <summary>
        /// Gets the value of a Slider (WindowsElement)
        /// </summary>
        /// <returns>The integer value of the slider</returns>
        public int GetValue()
        {
            return this.Text == string.Empty ? 0 : int.Parse(this.Text);
        }

        /// <summary>
        /// Sends a combination of keys.
        /// </summary>
        /// <param name="keys">The keys to send.</param>
        public void SendKeys(params Key[] keys)
        {
            PerformAction((actions, windowElement) =>
            {
                KeyboardHelper.SendKeys(keys);
            });
        }

        /// <summary>
        /// Sets the value of a Slider (WindowsElement) to the specified integer value.
        /// Throws an exception if the value is out of the slider's valid range.
        /// </summary>
        /// <param name="targetValue">The target integer value to set</param>
        public void SetValue(int targetValue)
        {
            // Read range and current value
            int min = int.Parse(this.GetAttribute("RangeValue.Minimum"));
            int max = int.Parse(this.GetAttribute("RangeValue.Maximum"));
            int current = int.Parse(this.Text);

            // Use Assert to check if the target value is within the valid range
            Assert.IsTrue(
                targetValue >= min && targetValue <= max,
                $"Target value {targetValue} is out of range (min: {min}, max: {max}).");

            // Compute difference
            int diff = targetValue - current;
            if (diff == 0)
            {
                return;
            }

            string key = diff > 0 ? OpenQA.Selenium.Keys.Right : OpenQA.Selenium.Keys.Left;
            int steps = Math.Abs(diff);

            for (int i = 0; i < steps; i++)
            {
                this.SendKeys(key);

                // Thread.Sleep(2);
            }

            // Final check
            int finalValue = int.Parse(this.Text);
            Assert.AreEqual(
                targetValue, finalValue, $"Slider value mismatch: expected {targetValue}, but got {finalValue}.");
        }

        /// <summary>
        /// Sets the value of a Slider (WindowsElement) to the specified integer value.
        /// Throws an exception if the value is out of the slider's valid range.
        /// </summary>
        /// <param name="targetValue">The target integer value to set</param>
        public void QuickSetValue(int targetValue)
        {
            // Read range and current value
            int min = int.Parse(this.GetAttribute("RangeValue.Minimum"));
            int max = int.Parse(this.GetAttribute("RangeValue.Maximum"));
            int current = int.Parse(this.Text);

            // Use Assert to check if the target value is within the valid range
            Assert.IsTrue(
                targetValue >= min && targetValue <= max,
                $"Target value {targetValue} is out of range (min: {min}, max: {max}).");

            // Compute difference
            int diff = targetValue - current;
            if (diff == 0)
            {
                return;
            }

            string key = diff > 0 ? OpenQA.Selenium.Keys.Right : OpenQA.Selenium.Keys.Left;
            int steps = Math.Abs(diff);

            int maxKeysPerSend = 50;
            int fullChunks = steps / maxKeysPerSend;
            int remainder = steps % maxKeysPerSend;
            for (int i = 0; i < fullChunks; i++)
            {
                SendKeys(new string(key[0], maxKeysPerSend));
                Thread.Sleep(2);
            }

            if (remainder > 0)
            {
                SendKeys(new string(key[0], remainder));
                Thread.Sleep(2);
            }

            // Final check
            int finalValue = int.Parse(this.Text);
            Assert.AreEqual(
                targetValue, finalValue, $"Slider value mismatch: expected {targetValue}, but got {finalValue}.");
        }
    }
}
