// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class IconContrastSettingsTests
{
    [TestMethod]
    public void ReadingSnapshotAndSubscribingBeforeWindowInitializationDoNotRequirePackageIdentity()
    {
        Action handler = () => { };
        IconContrastSettings.Changed += handler;
        try
        {
            Assert.AreEqual(default(IconContrast), IconContrastSettings.Current);
        }
        finally
        {
            IconContrastSettings.Changed -= handler;
        }
    }
}
