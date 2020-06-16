using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace Wox.Test.Plugins
{
    class PluginHelperTest
    {
        [Test]
        public void RemoveNewLineFromString_ShouldReplaceLineFeedWithSpace_WhenLineFeedIsPresent()
        {
            // Arrange
            string source = "Line1\nLine2";
            string target = "Line1 Line2";

            // Act 
            string replacment = Plugin.SharedCommands.Helper.RemoveNewLineFromString(source);
             
            // Assert
            Assert.IsTrue(replacment.Equals(target));
        }

        [Test]
        public void RemoveNewLineFromString_ShouldReplaceCarriageReturnWithSpace_WhencarriageReturnIsPresent()
        {
            // Arrange
            string source = "Line1\rLine2";
            string target = "Line1 Line2";

            // Act 
            string replacment = Plugin.SharedCommands.Helper.RemoveNewLineFromString(source);

            // Assert
            Assert.IsTrue(replacment.Equals(target));
        }

        [Test]
        public void RemoveNewLineFromString_ShouldReplaceEOLWithSpace_WhenEOLIsPresent()
        {
            // Arrange
            string source = "Line1\r\nLine2\nLine3";
            string target = "Line1 Line2 Line3";

            // Act 
            string replacment = Plugin.SharedCommands.Helper.RemoveNewLineFromString(source);

            // Assert
            Assert.IsTrue(replacment.Equals(target));
        }
    }
}
