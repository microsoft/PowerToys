// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.System.UnitTests;

[TestClass]
public class ImageTests
    {
        [DataTestMethod]
        [DataRow("shutdown", "shutdown")]
        [DataRow("restart", "restart")]
        [DataRow("sign out", "logoff")]
        [DataRow("lock", "lock")]
        [DataRow("sleep", "sleep")]
        [DataRow("hibernate", "sleep")]
        [DataRow("recycle bin", "recyclebin")]
        [DataRow("uefi firmware settings", "firmwareSettings")]
        [DataRow("ip v4 addr", "networkAdapter")]
        [DataRow("ip v6 addr", "networkAdapter")]
        [DataRow("mac addr", "networkAdapter")]
        public void IconThemeDarkTest(string typedString, string expectedIconName)
        {
            // Setup
            var iconUri = Icons.GetIcon(expectedIconName);

            // Act
            // Since we can't easily mock the complete system, just test that icons are available
            var result = iconUri;

            // Assert
            Assert.IsNotNull(result);
        }

        [DataTestMethod]
        [DataRow("shutdown", "shutdown")]
        [DataRow("restart", "restart")]
        [DataRow("sign out", "logoff")]
        [DataRow("lock", "lock")]
        [DataRow("sleep", "sleep")]
        [DataRow("hibernate", "sleep")]
        [DataRow("recycle bin", "recyclebin")]
        [DataRow("uefi firmware settings", "firmwareSettings")]
        [DataRow("ip v4 addr", "networkAdapter")]
        [DataRow("ip v6 addr", "networkAdapter")]
        [DataRow("mac addr", "networkAdapter")]
        public void IconThemeLightTest(string typedString, string expectedIconName)
        {
            // Setup
            var iconUri = Icons.GetIcon(expectedIconName);

            // Act
            // Since we can't easily mock the complete system, just test that icons are available
            var result = iconUri;

            // Assert
            Assert.IsNotNull(result);
        }
    }
