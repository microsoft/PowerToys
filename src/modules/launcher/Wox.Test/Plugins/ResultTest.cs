using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using Wox.Plugin;

namespace Wox.Test.Plugins
{
    [TestFixture]
    class ResultTest
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
            Assert.AreEqual(res.ToolTipVisibility, Visibility.Visible);
        }

        [Test]
        public void Result_UpdatesToolTipVisibilityToCollapsed_WhenToolTipDataIsNotSet()
        {
            // Act
            Result res = new Result();

            // Assert
            Assert.AreEqual(res.ToolTipVisibility, Visibility.Collapsed);
        }
    }
}
