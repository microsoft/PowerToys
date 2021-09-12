// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using NUnit.Framework;
using Wox.Plugin;

namespace Wox.Test.Plugins
{
    [TestFixture]
    internal class ResultTest
    {
        [Test]
        public void Result_UpdatesToolTipVisibilityToVisible_WhenToolTipDataIsSet()
        {
            // Arrange
            Result res = new Result();
            string toolTipText = "ToolTipText";

            // Act
            res.ToolTipData = new ToolTipData(toolTipText, string.Empty);

            // Assert
            Assert.AreEqual(Visibility.Visible, res.ToolTipVisibility);
        }

        [Test]
        public void Result_UpdatesToolTipVisibilityToCollapsed_WhenToolTipDataIsNotSet()
        {
            // Act
            Result res = new Result();

            // Assert
            Assert.AreEqual(Visibility.Collapsed, res.ToolTipVisibility);
        }
    }
}
