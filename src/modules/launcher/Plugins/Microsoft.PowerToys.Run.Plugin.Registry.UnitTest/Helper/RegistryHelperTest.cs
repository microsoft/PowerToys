// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Linq;
using Microsoft.PowerToys.Run.Plugin.Registry.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Run.Plugin.Registry.UnitTest.Helper
{
    [TestClass]
    public sealed class RegistryHelperTest
    {
        [TestMethod]
        [DataRow(@"HKCC\System\CurrentControlSet\Control", "HKEY_CURRENT_CONFIG")]
        [DataRow(@"HKCR\*\OpenWithList", "HKEY_CLASSES_ROOT")]
        [DataRow(@"HKCU\Control Panel\Accessibility", "HKEY_CURRENT_USER")]
        [DataRow(@"HKLM\HARDWARE\UEFI", "HKEY_LOCAL_MACHINE")]
        [DataRow(@"HKPD\???", "HKEY_PERFORMANCE_DATA")]
        [DataRow(@"HKU\.DEFAULT\Environment", "HKEY_USERS")]
        public void GetRegistryBaseKeyTestOnlyOneBaseKey(string query, string expectedBaseKey)
        {
            var (baseKeyList, _) = RegistryHelper.GetRegistryBaseKey(query);
            Assert.IsNotNull(baseKeyList);
            Assert.IsTrue(baseKeyList.Count() == 1);
            Assert.AreEqual(expectedBaseKey, baseKeyList.First().Name);
        }

        [TestMethod]
        public void GetRegistryBaseKeyTestMoreThanOneBaseKey()
        {
            var (baseKeyList, _) = RegistryHelper.GetRegistryBaseKey("HKC\\Control Panel\\Accessibility"); /* #no-spell-check-line */

            Assert.IsNotNull(baseKeyList);
            Assert.IsTrue(baseKeyList.Count() > 1);

            var list = baseKeyList.Select(found => found.Name);
            Assert.IsTrue(list.Contains("HKEY_CLASSES_ROOT"));
            Assert.IsTrue(list.Contains("HKEY_CURRENT_CONFIG"));
            Assert.IsTrue(list.Contains("HKEY_CURRENT_USER"));
        }

        [TestMethod]
        [DataRow(@"HKCR\*\OpenWithList", @"*\OpenWithList")]
        [DataRow(@"HKCU\Control Panel\Accessibility", @"Control Panel\Accessibility")]
        [DataRow(@"HKLM\HARDWARE\UEFI", @"HARDWARE\UEFI")]
        [DataRow(@"HKU\.DEFAULT\Environment", @".DEFAULT\Environment")]
        [DataRow(@"HKCC\System\CurrentControlSet\Control", @"System\CurrentControlSet\Control")]
        [DataRow(@"HKPD\???", @"???")]
        public void GetRegistryBaseKeyTestSubKey(string query, string expectedSubKey)
        {
            var (_, subKey) = RegistryHelper.GetRegistryBaseKey(query);
            Assert.AreEqual(expectedSubKey, subKey);
        }

        [TestMethod]
        public void GetAllBaseKeysTest()
        {
            var list = RegistryHelper.GetAllBaseKeys();

            CollectionAssert.AllItemsAreNotNull((ICollection)list);
            CollectionAssert.AllItemsAreUnique((ICollection)list);

            var keys = list.Select(found => found.Key).ToList() as ICollection;

            CollectionAssert.Contains(keys, Win32.Registry.ClassesRoot);
            CollectionAssert.Contains(keys, Win32.Registry.CurrentConfig);
            CollectionAssert.Contains(keys, Win32.Registry.CurrentUser);
            CollectionAssert.Contains(keys, Win32.Registry.LocalMachine);
            CollectionAssert.Contains(keys, Win32.Registry.PerformanceData);
            CollectionAssert.Contains(keys, Win32.Registry.Users);
        }
    }
}
