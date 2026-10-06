// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Commands;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Pages;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public sealed class BookmarkListItemAdministratorTests
{
    [TestMethod]
    [DataRow(CommandKind.FileExecutable, ".exe", false, true, true)]
    [DataRow(CommandKind.FileExecutable, ".com", false, true, false)]
    [DataRow(CommandKind.FileExecutable, ".pif", false, true, false)]
    [DataRow(CommandKind.PathCommand, ".exe", false, true, true)]
    [DataRow(CommandKind.PathCommand, ".cmd", false, true, true)]
    [DataRow(CommandKind.PathCommand, ".msc", false, true, true)]
    [DataRow(CommandKind.PathCommand, ".cpl", false, true, true)]
    [DataRow(CommandKind.PathCommand, ".txt", false, true, false)]
    [DataRow(CommandKind.PathCommand, ".com", false, true, false)]
    [DataRow(CommandKind.PathCommand, ".pif", false, true, false)]
    [DataRow(CommandKind.Shortcut, ".lnk", false, true, true)]
    [DataRow(CommandKind.FileExecutable, ".exe", true, true, false)]
    [DataRow(CommandKind.FileExecutable, ".exe", false, false, false)]
    [DataRow(CommandKind.FileDocument, ".msc", false, true, true)]
    [DataRow(CommandKind.FileDocument, ".MSC", false, true, true)]
    [DataRow(CommandKind.FileDocument, ".cpl", false, true, true)]
    [DataRow(CommandKind.FileDocument, ".CPL", false, true, true)]
    [DataRow(CommandKind.FileDocument, ".msc", false, false, false)]
    [DataRow(CommandKind.FileDocument, ".cpl", true, true, false)]
    [DataRow(CommandKind.FileDocument, ".txt", false, true, false)]
    [DataRow(CommandKind.FileDocument, ".dll", false, true, false)]
    [DataRow(CommandKind.FileDocument, ".ps1", false, true, false)]
    [DataRow(CommandKind.WebUrl, ".exe", false, true, false)]
    public async Task Bookmark_OffersRunAsAdministratorForLaunchableTargets(CommandKind kind, string extension, bool isPlaceholder, bool targetExists, bool expected)
    {
        var target = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
        if (targetExists)
        {
            File.WriteAllText(target, string.Empty);
        }

        try
        {
            var classification = new Classification(kind, target, target, "/c echo test", LaunchMethod.ShellExecute, Path.GetTempPath(), isPlaceholder);
            var bookmark = new BookmarkData("Test bookmark", target);
            var iconLocator = new Mock<IBookmarkIconLocator>();
            iconLocator.Setup(locator => locator.GetIconForPath(It.IsAny<Classification>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IconInfo("\uE737"));
            using var item = new BookmarkListItem(bookmark, new MockBookmarkManager(bookmark), new FixedBookmarkResolver(classification), iconLocator.Object, new PlaceholderParser());

            await item.IsInitialized.WaitAsync(TimeSpan.FromSeconds(5));

            var adminCommands = item.MoreCommands.OfType<CommandContextItem>().Where(command => command.Command is RunAsAdministratorCommand).ToArray();
            Assert.HasCount(expected ? 1 : 0, adminCommands);
            if (expected)
            {
                Assert.AreEqual(WellKnownKeyChords.RunAsAdministrator, adminCommands[0].RequestedShortcut);
            }
        }
        finally
        {
            File.Delete(target);
        }
    }

    private sealed class FixedBookmarkResolver(Classification classification) : IBookmarkResolver
    {
        public Task<(bool Success, Classification Result)> TryClassifyAsync(string input, CancellationToken cancellationToken = default) =>
            Task.FromResult((true, classification));

        public Classification ClassifyOrUnknown(string input) => classification;
    }
}
