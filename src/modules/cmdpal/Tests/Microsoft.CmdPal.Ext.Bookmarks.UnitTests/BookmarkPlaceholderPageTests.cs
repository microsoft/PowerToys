// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Pages;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public sealed class BookmarkPlaceholderPageTests
{
    [TestMethod]
    public void ResetPlaceholderValues_ClearsAllUniquePlaceholderValues()
    {
        var bookmark = new BookmarkData("Test bookmark", "https://example.com/{id}/{project}/{id}");
        var resolver = new Mock<IBookmarkResolver>();
        resolver
            .Setup(item => item.ClassifyOrUnknown(It.IsAny<string>()))
            .Returns((string input) => Classification.Unknown(input));
        var iconLocator = new Mock<IBookmarkIconLocator>();
        iconLocator
            .Setup(item => item.GetIconForPath(It.IsAny<Classification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<IIconInfo>(null!));

        using var page = new BookmarkPlaceholderPage(
            bookmark,
            iconLocator.Object,
            resolver.Object,
            new PlaceholderParser());

        var parameterOccurrences = page.Parameters.OfType<StringParameterRun>().ToArray();
        Assert.AreEqual(3, parameterOccurrences.Length);
        Assert.AreSame(parameterOccurrences[0], parameterOccurrences[2]);

        var parameters = parameterOccurrences.Distinct().ToArray();
        Assert.AreEqual(2, parameters.Length);
        parameters[0].Text = "42";
        parameters[1].Text = "PowerToys";

        page.ResetPlaceholderValues();

        CollectionAssert.AreEqual(
            new[] { string.Empty, string.Empty },
            parameters.Select(parameter => parameter.Text).ToArray());
    }
}
