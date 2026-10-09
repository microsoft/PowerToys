// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;

namespace Microsoft.PowerToys.UITest
{
    public class Thumb : Element
    {
        private static readonly string ExpectedControlType = "ControlType.Thumb";

        /// <summary>
        /// Initializes a new instance of the <see cref="Thumb"/> class.
        /// </summary>
        public Thumb()
        {
            this.TargetControlType = Thumb.ExpectedControlType;
        }

        /// <summary>
        /// Drag element move offset.
        /// </summary>
        /// <param name="offsetX">The offsetX to move.</param>
        /// <param name="offsetY">The offsetY to move.</param>
        public void Drag(int offsetX, int offsetY)
        {
            PerformAction((actions, windowElement) =>
            {
                actions.MoveToElement(windowElement).MoveByOffset(10, 10).ClickAndHold(windowElement).MoveByOffset(offsetX, offsetY).Release();
                actions.Build().Perform();
            });
        }
    }
}
