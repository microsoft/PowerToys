using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using Wox.Plugin;

namespace Wox.Test
{
    [TestFixture]
    class ResultTest
    {
        [Test]
        public void Result_UpdatesToolTipVisibilityToVisible_WhenToolTipTextIsNotNull()
        {
            // Arrange
            Result res = new Result();
            string toolTipText = "ToolTipText";

            // Act
            res.ToolTipText = toolTipText;

            // Assert
            Assert.AreEqual(res.ToolTipText, toolTipText);
            Assert.AreEqual(res.ToolTipVisibility, Visibility.Visible);
        }

        [Test]
        public void Result_UpdatesToolTipVisibilityToCollapsed_WhenToolTipTextIsEmpty()
        {
            // Arrange
            Result res = new Result();
            string toolTipText = "";

            // Act
            res.ToolTipText = toolTipText;

            // Assert
            Assert.AreEqual(res.ToolTipVisibility, Visibility.Collapsed);
        }

        [Test]
        public void Result_ToolTipVisibilityIsCollapsed_WhenToolTipTextIsNotSet()
        {
            // Arrange
            Result res = new Result();

            // Act

            // Assert
            Assert.AreEqual(res.ToolTipVisibility, Visibility.Collapsed);
        }
    }
}
