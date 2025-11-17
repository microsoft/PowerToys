// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Run.Plugin.Registry.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Run.Plugin.Registry.UnitTest.Helper
{
    [TestClass]
    public sealed class QueryHelperTest
    {
        [TestMethod]
        [DataRow(@"HKLM", false, @"HKLM", "")]
        [DataRow(@"HKLM\", false, @"HKLM\", "")]
        [DataRow(@"HKLM\\", true, @"HKLM", "")]
        [DataRow(@"HKLM\\Test", true, @"HKLM", "Test")]
        [DataRow(@"HKLM\Test\\TestTest", true, @"HKLM\Test", "TestTest")]
        [DataRow(@"HKLM\Test\\\TestTest", true, @"HKLM\Test", @"\TestTest")]
        [DataRow("HKLM/\"Software\"/", false, @"HKLM\Software\", "")]
        [DataRow("HKLM/\"Software\"//test", true, @"HKLM\Software", "test")]
        [DataRow("HKLM/\"Software\"//test/123", true, @"HKLM\Software", "test/123")]
        [DataRow("HKLM/\"Software\"//test\\123", true, @"HKLM\Software", @"test\123")]
        [DataRow("HKLM/\"Software\"/test", false, @"HKLM\Software\test", "")]
        [DataRow("HKLM\\Software\\\"test\"", false, @"HKLM\Software\test", "")]
        [DataRow("HKLM\\\"Software\"\\\"test\"", false, @"HKLM\Software\test", "")]
        [DataRow("HKLM\\\"Software\"\\\"test/software\"", false, @"HKLM\Software\test/software", "")]
        [DataRow("HKLM\\\"Software\"/\"test\"\\hello", false, @"HKLM\Software\test\hello", "")]
        [DataRow("HKLM\\\"Software\"\\\"test\"\\hello\\\\\"some/value\"", true, @"HKLM\Software\test\hello", "some/value")]
        [DataRow("HKLM\\\"Software\"\\\"test\"/hello\\\\\"some/value\"", true, @"HKLM\Software\test\hello", "some/value")]
        [DataRow("HKLM\\\"Software\"\\\"test\"\\hello\\\\some\\value", true, @"HKLM\Software\test\hello", @"some\value")]
        public void GetQueryPartsTest(string query, bool expectedHasValueName, string expectedQueryKey, string expectedQueryValueName)
        {
            var hasValueName = QueryHelper.GetQueryParts(query, out var queryKey, out var queryValueName);

            Assert.AreEqual(expectedHasValueName, hasValueName);
            Assert.AreEqual(expectedQueryKey, queryKey);
            Assert.AreEqual(expectedQueryValueName, queryValueName);
        }

        [TestMethod]
        [DataRow(@"HKCR\*\OpenWithList", @"HKEY_CLASSES_ROOT\*\OpenWithList")]
        [DataRow(@"HKCU\Control Panel\Accessibility", @"HKEY_CURRENT_USER\Control Panel\Accessibility")]
        [DataRow(@"HKLM\HARDWARE\UEFI", @"HKEY_LOCAL_MACHINE\HARDWARE\UEFI")]
        [DataRow(@"HKU\.DEFAULT\Environment", @"HKEY_USERS\.DEFAULT\Environment")]
        [DataRow(@"HKCC\System\CurrentControlSet\Control", @"HKEY_CURRENT_CONFIG\System\CurrentControlSet\Control")]
        [DataRow(@"HKPD\???", @"HKEY_PERFORMANCE_DATA\???")]
        public void GetShortBaseKeyTest(string registryKeyShort, string registryKeyFull)
        {
            Assert.AreEqual(registryKeyShort, QueryHelper.GetKeyWithShortBaseKey(registryKeyFull));
        }
    }
}
