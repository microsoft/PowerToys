// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
    private static readonly string[] ClearedPlaceholderValues = [string.Empty, string.Empty];
    private static readonly string[] PopulatedPlaceholderValues = ["42", "PowerToys"];

    [TestMethod]
    public void LaunchWithCurrentValues_SuccessfulLaunch_ClearsPlaceholderValuesAndSubtitle()
    {
        using var page = CreatePage(_ => true);
        var parameters = PopulatePlaceholderValues(page);
        Assert.AreEqual("https://example.com/42/PowerToys/42", page.Command.Subtitle);

        var result = InvokeLaunch(page);

        Assert.AreEqual(CommandResultKind.Dismiss, result.Kind);
        CollectionAssert.AreEqual(
            ClearedPlaceholderValues,
            parameters.Select(parameter => parameter.Text).ToArray());
        Assert.AreEqual(string.Empty, page.Command.Subtitle);
    }

    [TestMethod]
    public void LaunchWithCurrentValues_FailedLaunch_PreservesPlaceholderValuesAndSubtitle()
    {
        using var page = CreatePage(_ => false);
        var parameters = PopulatePlaceholderValues(page);
        const string expectedSubtitle = "https://example.com/42/PowerToys/42";
        Assert.AreEqual(expectedSubtitle, page.Command.Subtitle);

        var result = InvokeLaunch(page);

        Assert.AreEqual(CommandResultKind.KeepOpen, result.Kind);
        CollectionAssert.AreEqual(
            PopulatedPlaceholderValues,
            parameters.Select(parameter => parameter.Text).ToArray());
        Assert.AreEqual(expectedSubtitle, page.Command.Subtitle);
    }

    private static BookmarkPlaceholderPage CreatePage(Func<Classification, bool> launch)
    {
        return new BookmarkPlaceholderPage(
            new BookmarkData("Test bookmark", "https://example.com/{id}/{project}/{id}"),
            new StubBookmarkIconLocator(),
            new StubBookmarkResolver(),
            new PlaceholderParser(),
            launch);
    }

    private static StringParameterRun[] PopulatePlaceholderValues(BookmarkPlaceholderPage page)
    {
        var parameterOccurrences = page.Parameters.OfType<StringParameterRun>().ToArray();
        Assert.AreEqual(3, parameterOccurrences.Length);
        Assert.AreSame(parameterOccurrences[0], parameterOccurrences[2]);

        var parameters = parameterOccurrences.Distinct().ToArray();
        Assert.AreEqual(2, parameters.Length);
        parameters[0].Text = "42";
        parameters[1].Text = "PowerToys";
        return parameters;
    }

    private static CommandResult InvokeLaunch(BookmarkPlaceholderPage page)
    {
        Assert.IsInstanceOfType(page.Command.Command, typeof(IInvokableCommand));
        var command = (IInvokableCommand)page.Command.Command!;
        return (CommandResult)command.Invoke(null!);
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
