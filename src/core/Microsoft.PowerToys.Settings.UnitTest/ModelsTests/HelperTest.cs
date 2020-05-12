using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using System;

namespace CommonLibTest
{
    [TestClass]
    public class HelperTest
    {
        public static void TestStringIsSmaller(string v1, string v2)
        {
            var res = Helper.CompareVersions(v1, v2);
            Assert.IsTrue(res < 0);
        }


        public static void TestStringsAreEqual(string v1, string v2)
        {
            var res = Helper.CompareVersions(v1, v2);
            Assert.IsTrue(res == 0);
        }

        [TestMethod]
        public void Helper_CompareVersions_ShouldBeEqual_WhenSuccessful()
        {
            TestStringsAreEqual("v0.0.0", "v0.0.0");
            TestStringsAreEqual("v0.1.1", "v0.1.1");
            TestStringsAreEqual("v1.1.1", "v1.1.1");
            TestStringsAreEqual("v1.999.99", "v1.999.99");
        }

        [TestMethod]
        public void Helper_CompareVersions_ShouldBeSmaller_WhenSuccessful()
        {
            TestStringIsSmaller("v0.0.0", "v0.0.1");
            TestStringIsSmaller("v0.0.0", "v0.1.0");
            TestStringIsSmaller("v0.0.0", "v1.0.0");
            TestStringIsSmaller("v1.0.1", "v1.0.2");
            TestStringIsSmaller("v1.1.1", "v1.1.2");
            TestStringIsSmaller("v1.1.1", "v1.2.0");
            TestStringIsSmaller("v1.999.99", "v2.0.0");
            TestStringIsSmaller("v1.0.99", "v1.2.0");
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Helper_CompareVersions_ShouldThrowBadFormat_WhenNoVersionString()
        {
            Helper.CompareVersions("v0.0.1", "");
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Helper_CompareVersions_ShouldThrowBadFormat_WhenShortVersionString()
        {
            Helper.CompareVersions("v0.0.1", "v0.1");
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Helper_CompareVersions_ShouldThrowBadFormat_WhenLongVersionString()
        {
            Helper.CompareVersions("v0.0.1", "v0.0.0.1");
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Helper_CompareVersions_ShouldThrowBadFormat_WhenItIsNotAVersionString()
        {
            Helper.CompareVersions("v0.0.1", "HelloWorld");
        }
    }
}
