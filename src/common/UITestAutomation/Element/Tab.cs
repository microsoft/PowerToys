// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace Microsoft.PowerToys.UITest
{
    public class Tab : Element
    {
        private static readonly string ExpectedControlType = "ControlType.Tab";

        /// <summary>
        /// Initializes a new instance of the <see cref="Tab"/> class.
        /// </summary>
        public Tab()
        {
            this.TargetControlType = Tab.ExpectedControlType;
        }

        /// <summary>
        /// Simulates holding a key, clicking and dragging a UI element to the specified screen coordinates.
        /// </summary>
        /// <param name="key">The keyboard key to press and hold during the drag operation.</param>
        /// <param name="targetX">The target X-coordinate to drag the element to.</param>
        /// <param name="targetY">The target Y-coordinate to drag the element to.</param>
        public void KeyDownAndDrag(Key key, int targetX, int targetY)
        {
            HoldShiftToDrag(key, targetX, targetY);
            ReleaseAction();
            ReleaseKey(key);
        }

        /// <summary>
        /// Simulates holding a key, clicking and dragging a UI element to the specified screen coordinates.
        /// </summary>
        /// <param name="key">The keyboard key to press and hold during the drag operation.</param>
        /// <param name="targetX">The target X-coordinate to drag the element to.</param>
        /// <param name="targetY">The target Y-coordinate to drag the element to.</param>
        public void HoldShiftToDrag(Key key, int targetX, int targetY)
        {
            PerformAction((actions, windowElement) =>
            {
                KeyboardHelper.PressKey(key);

                actions.MoveToElement(WindowsElement)
                .ClickAndHold()
                .Perform();

                int dx = targetX - windowElement.Rect.X;
                int dy = targetY - windowElement.Rect.Y;

                int stepCount = 10;
                int stepX = dx / stepCount;
                int stepY = dy / stepCount;

                for (int i = 0; i < stepCount; i++)
                {
                    var stepAction = new Actions(Driver);
                    stepAction.MoveByOffset(stepX, stepY).Perform();
                }
            });
        }
    }
}
