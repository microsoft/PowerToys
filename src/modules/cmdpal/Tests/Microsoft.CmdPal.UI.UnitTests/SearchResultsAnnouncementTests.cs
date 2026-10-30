// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class SearchResultsAnnouncementTests
{
    private static readonly CompositeFormat CountFormat = CompositeFormat.Parse("{0} results");
    private static readonly CompositeFormat WithSelectedFormat = CompositeFormat.Parse("{0}, {1}");

    private static string Compose(int count, string? title) =>
        SearchResultsAnnouncement.Compose(count, title, "No results", "1 result", CountFormat, WithSelectedFormat, CultureInfo.InvariantCulture);

    [TestMethod]
    [DataRow(0, null)]
    [DataRow(0, "Calculator")]
    [DataRow(-1, "Calculator")]
    public void Compose_WithoutResults_AnnouncesNoResults(int count, string? title)
    {
        Assert.AreEqual("No results", Compose(count, title));
    }

    [TestMethod]
    public void Compose_WithOneResult_UsesSingularText()
    {
        Assert.AreEqual("1 result, Calculator", Compose(1, "Calculator"));
    }

    [TestMethod]
    public void Compose_WithManyResults_IncludesCountAndSelectedTitle()
    {
        Assert.AreEqual("12 results, Calculator", Compose(12, "Calculator"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void Compose_WithoutSelectedTitle_AnnouncesCountOnly(string? title)
    {
        Assert.AreEqual("3 results", Compose(3, title));
        Assert.AreEqual("1 result", Compose(1, title));
    }
}
