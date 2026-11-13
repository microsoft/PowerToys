// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Registry.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Registry.UnitTests;

[OSCondition(OperatingSystems.Windows)]
[TestClass]
public class BasicStructureTest
{
    [TestMethod]
    public void RegistryHelpersAreAvailable()
    {
        Assert.AreEqual(@"HKEY_CURRENT_USER\Software", QueryHelper.GetKeyWithLongBaseKey(@"HKCU\Software"));
    }
}
