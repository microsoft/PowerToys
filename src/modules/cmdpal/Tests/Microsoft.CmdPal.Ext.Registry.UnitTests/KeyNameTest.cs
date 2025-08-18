// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Registry.Constants;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Registry.UnitTests;

[TestClass]
public class KeyNameTest
{
    [TestMethod]
    [DataRow("HKEY", KeyName.FirstPart)]
    [DataRow("HKEY_", KeyName.FirstPartUnderscore)]
    [DataRow("HKCR", KeyName.ClassRootShort)]
    [DataRow("HKCC", KeyName.CurrentConfigShort)]
    [DataRow("HKCU", KeyName.CurrentUserShort)]
    [DataRow("HKLM", KeyName.LocalMachineShort)]
    [DataRow("HKPD", KeyName.PerformanceDataShort)]
    [DataRow("HKU", KeyName.UsersShort)]
    public void TestConstants(string shortName, string baseName)
    {
        Assert.AreEqual(shortName, baseName);
    }
}
