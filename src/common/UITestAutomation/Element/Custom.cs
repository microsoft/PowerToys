// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest
{
    public class Custom : Element
    {
        private static readonly string ExpectedControlType = "ControlType.Custom";

        /// <summary>
        /// Initializes a new instance of the <see cref="Custom"/> class.
        /// </summary>
        public Custom()
        {
            this.TargetControlType = Custom.ExpectedControlType;
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
        /// Drag element move to other element.
        /// </summary>
        /// <param name="element">Move to this element.</param>
        public void Drag(Element element)
        {
            PerformAction((actions, windowElement) =>
            {
                actions.MoveToElement(windowElement).ClickAndHold();
                Assert.IsNotNull(element.WindowsElement, "element is null");
                int dx = (element.WindowsElement.Rect.X - windowElement.Rect.X) / 10;
                int dy = (element.WindowsElement.Rect.Y - windowElement.Rect.Y) / 10;
                for (int i = 0; i < 10; i++)
                {
                    actions.MoveByOffset(dx, dy);
                }

                actions.Release();
                actions.Build().Perform();
            });
        }
    }
}
