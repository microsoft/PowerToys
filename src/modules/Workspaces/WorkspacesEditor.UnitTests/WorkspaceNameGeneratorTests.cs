// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesEditor.Utils;

namespace WorkspacesEditor.UnitTests
{
    [TestClass]
    public class WorkspaceNameGeneratorTests
    {
        [TestMethod]
        public void Create_IgnoresUnnumberedAndUnrelatedNames()
        {
            string[] names = ["Workspace", "Workspace ", "Workspace notes", "Workspace2", null, "Other 90", "Workspace -5"];

            Assert.AreEqual("Workspace 1", WorkspaceNameGenerator.Create("Workspace", names));
        }

        [TestMethod]
        public void Create_UsesLargestValidNumberWithLocalizedPrefix()
        {
            string[] names = ["工作区", "工作区 3", "工作区 12", "工作区 2", "工作区 notes"];

            Assert.AreEqual("工作区 13", WorkspaceNameGenerator.Create("工作区", names));
        }

        [TestMethod]
        public void Create_NumberBeyondInt64DoesNotOverflowOrReuseExistingName()
        {
            string[] names = ["Workspace 9223372036854775807", "Workspace 9223372036854775808"];

            Assert.AreEqual("Workspace 9223372036854775809", WorkspaceNameGenerator.Create("Workspace", names));
        }
    }
}
