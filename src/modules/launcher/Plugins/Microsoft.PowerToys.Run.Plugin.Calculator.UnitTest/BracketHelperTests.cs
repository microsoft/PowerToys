// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests
{
    [TestClass]
    public class BracketHelperTests
    {
        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("\t \r\n")]
        [DataRow("none")]
        [DataRow("()")]
        [DataRow("(())")]
        [DataRow("()()")]
        [DataRow("(()())")]
        [DataRow("([][])")]
        [DataRow("([(()[])[](([]()))])")]
        public void IsBracketComplete_TestValid_WhenCalled(string input)
        {
            // Arrange

            // Act
            var result = BracketHelper.IsBracketComplete(input);

            // Assert
            Assert.IsTrue(result);
        }

        [DataTestMethod]
        [DataRow("((((",  "only opening brackets")]
        [DataRow("]]]",  "only closing brackets")]
        [DataRow("([)(])",  "inner bracket mismatch")]
        [DataRow(")(",  "opening and closing reversed")]
        [DataRow("(]",  "mismatch in bracket type")]
        public void IsBracketComplete_TestInvalid_WhenCalled(string input, string invalidReason)
        {
            // Arrange

            // Act
            var result = BracketHelper.IsBracketComplete(input);

            // Assert
            Assert.IsFalse(result, invalidReason);
        }
    }
}
