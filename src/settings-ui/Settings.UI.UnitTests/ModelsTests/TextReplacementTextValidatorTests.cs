// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using KeyboardManagerEditorUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class TextReplacementTextValidatorTests
    {
        [TestMethod]
        public void IsValidTarget_AllowsRegularUnicodeAndLineBreaks()
        {
            Assert.IsTrue(TextReplacementTextValidator.IsValidTarget("café 😀\r\nnext line"));
            Assert.IsTrue(TextReplacementTextValidator.IsValidTarget("line one\nline two\rline three"));
        }

        [TestMethod]
        public void IsValidTarget_RejectsTabsOtherControlsAndUnpairedSurrogates()
        {
            Assert.IsFalse(TextReplacementTextValidator.IsValidTarget("a\tb"));
            Assert.IsFalse(TextReplacementTextValidator.IsValidTarget("a\bb"));
            Assert.IsFalse(TextReplacementTextValidator.IsValidTarget("a\u007Fb"));
            Assert.IsFalse(TextReplacementTextValidator.IsValidTarget("a\u0085b"));
            Assert.IsFalse(TextReplacementTextValidator.IsValidTarget("a\uD83Db"));
            Assert.IsFalse(TextReplacementTextValidator.IsValidTarget("a\uDE00b"));
        }
    }
}
