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

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public sealed class BookmarkPlaceholderPageTests
{
    [TestMethod]
    public void ResetPlaceholderValues_ClearsAllUniquePlaceholderValues()
    {
        var bookmark = new BookmarkData("Test bookmark", "https://example.com/{id}/{project}/{id}");
        var resolver = new StubBookmarkResolver();
        var iconLocator = new StubBookmarkIconLocator();

        using var page = new BookmarkPlaceholderPage(
            bookmark,
            iconLocator,
            resolver,
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

    private sealed class StubBookmarkResolver : IBookmarkResolver
    {
        public Task<(bool Success, Classification Result)> TryClassifyAsync(string input, CancellationToken cancellationToken = default) =>
            Task.FromResult((true, Classification.Unknown(input)));

        public Classification ClassifyOrUnknown(string input) => Classification.Unknown(input);
    }

    private sealed class StubBookmarkIconLocator : IBookmarkIconLocator
    {
        public Task<IIconInfo> GetIconForPath(Classification classification, CancellationToken cancellationToken = default) =>
            Task.FromResult<IIconInfo>(null!);
    }
}
