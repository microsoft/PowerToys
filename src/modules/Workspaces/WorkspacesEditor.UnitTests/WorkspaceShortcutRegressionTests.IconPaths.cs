// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesEditor.Services;

namespace WorkspacesEditor.UnitTests
{
    public partial class WorkspaceShortcutRegressionTests
    {
        [TestMethod]
        public void ForeignLinkWithExtendedIconPath_ProtectsIconUntilTheLinkUsesItsOwnFile()
        {
            using var fixture = new ShortcutFixture();
            var original = fixture.Project("Extended icon path");
            fixture.Apply(original);
            string icon = fixture.IconPath(original);
            Assert.IsTrue(char.IsAsciiLetter(icon[0]) && icon[1] == ':', "This test requires a drive-letter path for the local ICO.");
            string extendedIcon = @"\\?\" + icon;
            var foreign = fixture.Project(original.Name);
            fixture.WriteShortcut(foreign, iconPath: extendedIcon);
            string foreignPath = fixture.LinkPath(foreign.Name);
            byte[] originalLink = File.ReadAllBytes(foreignPath);
            var stored = WorkspaceShellLink.Inspect(originalLink, Guid.Parse(original.Id));
            Assert.IsFalse(stored.Owned);
            Assert.AreEqual(extendedIcon, stored.IconPath, "The native link must persist the extended path spelling.");
            CollectionAssert.AreEqual(File.ReadAllBytes(icon), File.ReadAllBytes(stored.IconPath));

            var renamed = fixture.Project("Another desktop name", original.Id);
            Assert.AreEqual(WorkspaceShortcutStatus.Success, fixture.Service.ApplyWithResult(renamed, original.Name).Status);
            byte[] expectedIcon = File.ReadAllBytes(icon);
            Assert.AreEqual(WorkspaceShortcutStatus.Success, fixture.OpenService().RemoveWithResult(renamed).Status);
            fixture.OpenService().RetryPendingRemovals([foreign.Id]);
            Assert.IsFalse(File.Exists(fixture.LinkPath(renamed.Name)));
            CollectionAssert.AreEqual(originalLink, File.ReadAllBytes(foreignPath));
            CollectionAssert.AreEqual(expectedIcon, File.ReadAllBytes(icon));

            fixture.WriteShortcut(foreign);
            byte[] retargetedLink = File.ReadAllBytes(foreignPath);
            string foreignIcon = fixture.IconPath(foreign);
            File.Copy(icon, foreignIcon, overwrite: true);
            Assert.IsFalse(fixture.OpenService().RetryPendingRemovals([foreign.Id]));
            Assert.IsFalse(File.Exists(icon));
            CollectionAssert.AreEqual(retargetedLink, File.ReadAllBytes(foreignPath));
            CollectionAssert.AreEqual(expectedIcon, File.ReadAllBytes(foreignIcon));
        }

        [TestMethod]
        [DataRow(@"\\?\UNC\share.test\users\Icons\workspace.ico", @"\\share.test\users\Icons\workspace.ico", true)]
        [DataRow(@"\\share.test\users\Icons\workspace.ico", @"\\?\unc\share.test\users\Icons\workspace.ico", true)]
        [DataRow(@"\\?\D:\Icons\workspace.ico", @"D:\Icons\workspace.ico", true)]
        [DataRow(@"D:\Icons\workspace.ico", @"\\?\D:\Icons\workspace.ico", true)]
        [DataRow(@"\\?\UNC\elsewhere.test\users\Icons\workspace.ico", @"\\share.test\users\Icons\workspace.ico", false)]
        [DataRow(@"\\?\UNC\share.test\other\Icons\workspace.ico", @"\\share.test\users\Icons\workspace.ico", false)]
        [DataRow(@"\\?\UNC\share.test\\Icons\workspace.ico", @"\\share.test\users\Icons\workspace.ico", false)]
        [DataRow(@"\\?\UNC\share.test\users\old\..\Icons\workspace.ico", @"\\share.test\users\Icons\workspace.ico", false)]
        [DataRow(@"\\?\D:\Icons\.\workspace.ico", @"D:\Icons\workspace.ico", false)]
        [DataRow(@"\\?\D:\Icons/workspace.ico", @"D:\Icons\workspace.ico", false)]
        [DataRow(@"//?/D:/Icons/workspace.ico", @"D:\Icons\workspace.ico", false)]
        [DataRow(@"\\.\D:\Icons\workspace.ico", @"D:\Icons\workspace.ico", false)]
        [DataRow(@"\??\D:\Icons\workspace.ico", @"D:\Icons\workspace.ico", false)]
        [DataRow(@"\\?\GLOBALROOT\Device\disk\Icons\workspace.ico", @"\\?\GLOBALROOT\Device\disk\Icons\workspace.ico", false)]
        public void ExtendedIconPaths_CompareLocalAndUncSpellingsLexically(string referenced, string managed, bool expected)
        {
            Assert.AreEqual(expected, WorkspaceShortcutFile.MayReferenceIcon(referenced, managed));
        }
    }
}
