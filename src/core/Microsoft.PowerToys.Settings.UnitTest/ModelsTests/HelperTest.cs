using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Microsoft.PowerToys.Settings.UnitTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Windows.ApplicationModel.VoiceCommands;

namespace CommonLibTest
{
    [TestClass]
    class HelperTest
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
        public void Helper_CompareVersions_Smaller()
        {
            TestStringIsSmaller("v0.0.0", "v0.0.0");
            TestStringIsSmaller("v0.1.1", "v0.1.1");
            TestStringIsSmaller("v00.0.0", "v0.00.000");
            TestStringIsSmaller("v1.09.10", "v01.0.10");
            TestStringIsSmaller("v1.1.1", "v1.1.1");
            TestStringIsSmaller("v1.999.99", "1.999.99");
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Helper_CompareVersions_BadFormat_NoVersion()
        {
            Helper.CompareVersions("v0.0.1", "");
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Helper_CompareVersions_BadFormat_ShortVersion()
        {
            Helper.CompareVersions("v0.0.1", "v0.1");
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Helper_CompareVersions_BadFormat_LongVersion()
        {
            Helper.CompareVersions("v0.0.1", "v0.0.0.1");
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Helper_CompareVersions_BadFormat_NoVersionString()
        {
            Helper.CompareVersions("v0.0.1", "HelloWorld");
        }
    }
}
