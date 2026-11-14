// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using HostsUILib.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hosts.Tests
{
    [TestClass]
    public class GroupPolicyTest
    {
        [DataTestMethod]
        [DataRow(null, null, false, DisplayName = "Not configured")]
        [DataRow(0, null, true, DisplayName = "Editor disabled")]
        [DataRow(1, null, false, DisplayName = "Editor enabled")]
        [DataRow(null, 0, true, DisplayName = "Global disabled")]
        [DataRow(null, 1, false, DisplayName = "Global enabled")]
        [DataRow(1, 0, false, DisplayName = "Editor enabled overrides global disabled")]
        [DataRow(0, 1, true, DisplayName = "Editor disabled overrides global enabled")]
        [DataRow(2, 0, true, DisplayName = "Unrecognized editor value falls back to global")]
        [DataRow(2, 2, false, DisplayName = "Unrecognized values are not disabled")]
        public void IsHostsFileEditorDisabled(int? editorValue, int? globalValue, bool expected)
        {
            var values = new Dictionary<string, int?>
            {
                [GroupPolicy.HostsFileEditorEnabledValueName] = editorValue,
                [GroupPolicy.GlobalUtilityEnabledValueName] = globalValue,
            };

            Assert.AreEqual(expected, GroupPolicy.IsHostsFileEditorDisabled(name => values[name]));
        }
    }
}
