// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wox.Plugin;

namespace Wox.Test.Plugins
{
    [TestClass]
    public class ResultTest
    {
        [TestMethod]
        public void ResultUpdatesToolTipVisibilityToVisibleWhenToolTipDataIsSet()
        {
            // Arrange
            Result res = new Result();
            string toolTipText = "ToolTipText";

            // Act
            res.ToolTipData = new ToolTipData(toolTipText, string.Empty);

            // Assert
            Assert.AreEqual(Visibility.Visible, res.ToolTipVisibility);
        }

        [TestMethod]
        public void ResultUpdatesToolTipVisibilityToCollapsedWhenToolTipDataIsNotSet()
        {
            // Act
            Result res = new Result();

            // Assert
            Assert.AreEqual(Visibility.Collapsed, res.ToolTipVisibility);
        }
    }
}
