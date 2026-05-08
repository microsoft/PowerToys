// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers;

namespace PowerDisplay.UnitTests;

[TestClass]
public class DisplayClassifierTests
{
    [DataTestMethod]

    // Internal: INTERNAL high-bit flag
    [DataRow(0x80000000u, true, DisplayName = "INTERNAL bit only")]
    [DataRow(0x8000000Bu, true, DisplayName = "INTERNAL | DISPLAYPORT_EMBEDDED")]

    // Internal: documented embedded subtypes
    [DataRow(11u, true, DisplayName = "DISPLAYPORT_EMBEDDED")]
    [DataRow(13u, true, DisplayName = "UDI_EMBEDDED")]

    // External: LVDS is not classified internal per docs
    [DataRow(6u, false, DisplayName = "LVDS (not classified internal per docs)")]

    // External: documented external connectors
    [DataRow(5u, false, DisplayName = "HDMI")]
    [DataRow(10u, false, DisplayName = "DISPLAYPORT_EXTERNAL")]
    [DataRow(12u, false, DisplayName = "UDI_EXTERNAL")]

    // External: virtual / wireless
    [DataRow(15u, false, DisplayName = "MIRACAST")]
    [DataRow(17u, false, DisplayName = "INDIRECT_VIRTUAL")]

    // External: OTHER (-1) cast to uint
    [DataRow(0xFFFFFFFFu, false, DisplayName = "OTHER (-1 cast to uint)")]

    // External: unrecognised values default to external
    [DataRow(0xDEADBEEFu, false, DisplayName = "Unknown value defaults to external")]
    public void IsInternal_ReturnsExpectedClassification(uint outputTechnology, bool expected)
    {
        Assert.AreEqual(expected, DisplayClassifier.IsInternal(outputTechnology));
    }

    [TestMethod]
    public void GetOutputTechnologyName_KnownValues_ReturnsName()
    {
        Assert.AreEqual("HDMI", DisplayClassifier.GetOutputTechnologyName(5u));
        Assert.AreEqual("DISPLAYPORT_EMBEDDED", DisplayClassifier.GetOutputTechnologyName(11u));
        Assert.AreEqual("INTERNAL", DisplayClassifier.GetOutputTechnologyName(0x80000000u));
    }

    [TestMethod]
    public void GetOutputTechnologyName_InternalFlagWithSubtype_ComposesName()
    {
        // INTERNAL flag combined with DISPLAYPORT_EMBEDDED should produce a composite name
        var name = DisplayClassifier.GetOutputTechnologyName(0x8000000Bu);
        StringAssert.Contains(name, "INTERNAL");
        StringAssert.Contains(name, "DISPLAYPORT_EMBEDDED");
    }

    [TestMethod]
    public void GetOutputTechnologyName_UnknownValue_ReturnsHexFormat()
    {
        Assert.AreEqual("Unknown(0xDEADBEEF)", DisplayClassifier.GetOutputTechnologyName(0xDEADBEEFu));
    }

    [TestMethod]
    public void GetOutputTechnologyName_InternalFlagWithUnknownSubtype_RendersAsUnknownHex()
    {
        // INTERNAL bit set with an undocumented subtype (7): we render the raw hex.
        Assert.AreEqual("Unknown(0x80000007)", DisplayClassifier.GetOutputTechnologyName(0x80000007u));
    }
}
