// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NUnit.Framework;

namespace Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests
{
    [TestFixture]
    public class BracketHelperTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("\t \r\n")]
        [TestCase("none")]
        [TestCase("()")]
        [TestCase("(())")]
        [TestCase("()()")]
        [TestCase("(()())")]
        [TestCase("([][])")]
        [TestCase("([(()[])[](([]()))])")]
        public void IsBracketComplete_TestValid_WhenCalled(string input)
        {
            // Arrange

            // Act
            var result = BracketHelper.IsBracketComplete(input);

            // Assert
            Assert.IsTrue(result);
        }

        [TestCase("((((",  "only opening brackets")]
        [TestCase("]]]",  "only closing brackets")]
        [TestCase("([)(])",  "inner bracket mismatch")]
        [TestCase(")(",  "opening and closing reversed")]
        [TestCase("(]",  "mismatch in bracket type")]
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
