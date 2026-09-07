// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseJump.HotKeys.UnitTests;

public static class KeystrokeTests
{
    [TestClass]
    public sealed class ParseTests
    {
        public sealed class TestCase
        {
            public TestCase(string value, Keystroke expectedResult)
            {
                this.Value = value;
                this.ExpectedResult = expectedResult;
            }

            public string Value { get; }

            public Keystroke ExpectedResult { get; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            // individual modifiers / keys
            yield return new object[] { new TestCase("CTRL", new(Keys.None, KeyModifiers.Control)) };
            yield return new object[] { new TestCase("ALT", new(Keys.None, KeyModifiers.Alt)) };
            yield return new object[] { new TestCase("SHIFT", new(Keys.None, KeyModifiers.Shift)) };
            yield return new object[] { new TestCase("WIN", new(Keys.None, KeyModifiers.Windows)) };
            yield return new object[] { new TestCase("F", new(Keys.F, KeyModifiers.None)) };

            // multiple modifiers
            yield return new object[] { new TestCase("CTRL + ALT + SHIFT", new(Keys.None, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift)) };
            yield return new object[] { new TestCase("SHIFT + ALT + CTRL", new(Keys.None, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift)) };

            // modifiers and keys
            yield return new object[] { new TestCase("CTRL + ALT + SHIFT + F", new(Keys.F, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift)) };
            yield return new object[] { new TestCase("CTRL + ALT + SHIFT + WIN + F", new(Keys.F, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Windows)) };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases))]
        public void RunTestCases(TestCase data)
        {
            var expected = data.ExpectedResult;
            var result = Keystroke.TryParse(data.Value, out var actual);
            Assert.IsTrue(result);
            if (actual == null)
            {
                throw new InvalidOperationException();
            }

            Assert.AreEqual(expected.Key, actual.Key);
            Assert.AreEqual(expected.Modifiers, actual.Modifiers);
        }
    }
}
