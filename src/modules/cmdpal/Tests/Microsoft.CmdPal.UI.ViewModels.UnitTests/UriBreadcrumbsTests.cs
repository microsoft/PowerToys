// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class UriBreadcrumbsTests
{
    private static readonly string[] DecodedExtensionBreadcrumbs = ["extensions", "gallery", "sample extension"];
    private static readonly string[] SingleDecodedExtensionBreadcrumbs = ["extensions", "gallery", "sample%2Fextension"];

    [TestMethod]
    public void TryParse_DecodesEachSegmentAfterSplitting()
    {
        var result = UriBreadcrumbs.TryParse(
            new Uri("x-cmdpal://extensions/gallery/sample%20extension"),
            "x-cmdpal",
            out var breadcrumbs);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(DecodedExtensionBreadcrumbs, breadcrumbs);
    }

    [TestMethod]
    public void TryParse_DecodesEachSegmentExactlyOnce()
    {
        var result = UriBreadcrumbs.TryParse(
            new Uri("x-cmdpal://extensions/gallery/sample%252Fextension"),
            "x-cmdpal",
            out var breadcrumbs);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(SingleDecodedExtensionBreadcrumbs, breadcrumbs);
    }

    [DataTestMethod]
    [DataRow("x-cmdpal://extensions/gallery/sample%2Fextension")]
    [DataRow("x-cmdpal://extensions/gallery/sample%5Cextension")]
    [DataRow("x-cmdpal://extensions/gallery/sample%0Aextension")]
    public void TryParse_UnsafeDecodedSegment_ReturnsFalseWithEmptyOutput(string uri)
    {
        var result = UriBreadcrumbs.TryParse(new Uri(uri), "x-cmdpal", out var breadcrumbs);

        Assert.IsFalse(result);
        Assert.AreEqual(0, breadcrumbs.Length);
    }
}
