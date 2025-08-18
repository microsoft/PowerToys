// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.CmdPal.Ext.System.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.System.UnitTests;

[TestClass]
public class ImageTests
{
    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public void IconThemeTest(bool isDarkIcon)
    {
        var systemPage = new SystemCommandPage(new Settings());
        var commands = systemPage.GetItems();

        foreach (var item in commands)
        {
            var icon = item.Icon;
            Assert.IsNotNull(icon, $"Icon for '{item.Title}' should not be null.");
            if (isDarkIcon)
            {
                Assert.IsNotEmpty(icon.Dark.Icon, $"Icon for '{item.Title}' should not be empty.");
            }
            else
            {
                Assert.IsNotEmpty(icon.Light.Icon, $"Icon for '{item.Title}' should not be empty.");
            }
        }
    }
}
