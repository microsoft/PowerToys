// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerOCR.Core.Geometry;
using PowerOCR.Core.Models;

namespace PowerOCR.Core.UnitTests;

[TestClass]
public sealed class SelectionGeometryTests
{
    [TestMethod]
    public void ToPixels_At150Percent_MapsDipToPhysicalPixels()
    {
        var result = SelectionGeometry.ToPixels(
            new OcrPoint(10, 20),
            new OcrPoint(110, 70),
            rasterizationScale: 1.5,
            new DisplayBounds(-1920, 0, 1920, 1080));

        Assert.AreEqual(new PixelRect(15, 30, 150, 75), result.Local);
        Assert.AreEqual(new PixelRect(-1905, 30, 150, 75), result.Absolute);
    }

    [TestMethod]
    public void ToPixels_ReverseDrag_ProducesPositiveClampedRectangle()
    {
        var result = SelectionGeometry.ToPixels(
            new OcrPoint(100, 100),
            new OcrPoint(-10, -20),
            rasterizationScale: 1.0,
            new DisplayBounds(0, 0, 1920, 1080));

        Assert.AreEqual(new PixelRect(0, 0, 100, 100), result.Local);
    }
}
