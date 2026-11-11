// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Linq;

using Microsoft.CmdPal.Ext.Registry.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Registry.UnitTests;

[OSCondition(OperatingSystems.Windows)]
[TestClass]
public class RegistryHelperTest
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
        Assert.HasCount(1, baseKeyList);
        Assert.AreEqual(expectedBaseKey, baseKeyList.First().Name);
    }

    [TestMethod]
    public void GetRegistryBaseKeyTestMoreThanOneBaseKey()
    {
        var (baseKeyList, _) = RegistryHelper.GetRegistryBaseKey("HKC\\Control Panel\\Accessibility"); /* #no-spell-check-line */

        Assert.IsNotNull(baseKeyList);
        Assert.IsGreaterThan(1, baseKeyList.Count());

        var list = baseKeyList.Select(found => found.Name);
        Assert.Contains("HKEY_CLASSES_ROOT", list);
        Assert.Contains("HKEY_CURRENT_CONFIG", list);
        Assert.Contains("HKEY_CURRENT_USER", list);
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

        Assert.AreAllNotNull((ICollection)list);
        Assert.AreAllDistinct((ICollection)list);

        var keys = list.Select(found => found.Key).ToList();

        Assert.Contains(Win32.Registry.ClassesRoot, keys);
        Assert.Contains(Win32.Registry.CurrentConfig, keys);
        Assert.Contains(Win32.Registry.CurrentUser, keys);
        Assert.Contains(Win32.Registry.LocalMachine, keys);
        Assert.Contains(Win32.Registry.PerformanceData, keys);
        Assert.Contains(Win32.Registry.Users, keys);
    }
}
