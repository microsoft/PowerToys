// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace Microsoft.PowerToys.UITest
{
    public class Pane : Element
    {
        private static readonly string ExpectedControlType = "ControlType.Pane";

        /// <summary>
        /// Initializes a new instance of the <see cref="Pane"/> class.
        /// </summary>
        public Pane()
        {
            this.TargetControlType = Pane.ExpectedControlType;
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

        /// <summary>
        /// Simulates holding when dragging to target position.
        /// </summary>
        /// <param name="offsetX">The offsetX to move.</param>
        /// <param name="offsetY">The offsetY to move.</param>
        public void DragAndHold(int offsetX, int offsetY)
        {
            PerformAction((actions, windowElement) =>
            {
                actions.MoveToElement(windowElement).MoveByOffset(10, 10).ClickAndHold(windowElement).MoveByOffset(offsetX, offsetY);
                actions.Build().Perform();
            });
        }

        public void ReleaseDrag()
        {
            var releaseAction = new Actions(this.Driver);
            releaseAction.Release().Perform();
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
