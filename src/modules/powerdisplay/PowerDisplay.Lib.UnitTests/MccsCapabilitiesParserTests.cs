// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Unit tests for MccsCapabilitiesParser class.
/// Tests parsing of DDC/CI MCCS capabilities strings using real-world examples.
/// Reference: https://www.ddcutil.com/cap_u3011_verbose_output/
/// </summary>
[TestClass]
public class MccsCapabilitiesParserTests
{
    // Real capabilities string from Dell U3011 monitor
    // Source: https://www.ddcutil.com/cap_u3011_verbose_output/
    private const string DellU3011Capabilities =
        "(prot(monitor)type(lcd)model(U3011)cmds(01 02 03 07 0C E3 F3)vcp(02 04 05 06 08 10 12 14(01 05 08 0B 0C) 16 18 1A 52 60(01 03 04 0C 0F 11 12) AC AE B2 B6 C6 C8 C9 D6(01 04 05) DC(00 02 03 04 05) DF FD)mccs_ver(2.1)mswhql(1))";

    // Real capabilities string from Dell P2416D monitor
    private const string DellP2416DCapabilities =
        "(prot(monitor)type(LCD)model(P2416D)cmds(01 02 03 07 0C E3 F3) vcp(02 04 05 08 10 12 14(05 08 0B 0C) 16 18 1A 52 60(01 11 0F) AA(01 02) AC AE B2 B6 C6 C8 C9 D6(01 04 05) DC(00 02 03 05) DF E0 E1 E2(00 01 02 04 0E 12 14 19) F0(00 08) F1(01 02) F2 FD) mswhql(1)asset_eep(40)mccs_ver(2.1))";

    // Simple test string
    private const string SimpleCapabilities =
        "(prot(monitor)type(lcd)model(TestMonitor)vcp(10 12)mccs_ver(2.2))";

    // Capabilities without outer parentheses (some monitors like Apple Cinema Display)
    private const string NoOuterParensCapabilities =
        "prot(monitor)type(lcd)model(TestMonitor)vcp(10 12)mccs_ver(2.0)";

    // Concatenated hex format (no spaces between hex bytes)
    private const string ConcatenatedHexCapabilities =
        "(prot(monitor)cmds(01020307)vcp(101214)mccs_ver(2.1))";

    [TestMethod]
    public void Parse_NullInput_ReturnsEmptyCapabilities()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(null);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Capabilities);
        Assert.AreEqual(0, result.Capabilities.SupportedVcpCodes.Count);
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Parse_EmptyString_ReturnsEmptyCapabilities()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(string.Empty);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Capabilities.SupportedVcpCodes.Count);
    }

    [TestMethod]
    public void Parse_WhitespaceOnly_ReturnsEmptyCapabilities()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse("   \t\n  ");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Capabilities.SupportedVcpCodes.Count);
    }

    [TestMethod]
    public void Parse_DellU3011_ParsesProtocol()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellU3011Capabilities);

        // Assert
        Assert.AreEqual("monitor", result.Capabilities.Protocol);
    }

    [TestMethod]
    public void Parse_DellU3011_ParsesType()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellU3011Capabilities);

        // Assert
        Assert.AreEqual("lcd", result.Capabilities.Type);
    }

    [TestMethod]
    public void Parse_DellU3011_ParsesModel()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellU3011Capabilities);

        // Assert
        Assert.AreEqual("U3011", result.Capabilities.Model);
    }

    [TestMethod]
    public void Parse_DellU3011_ParsesMccsVersion()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellU3011Capabilities);

        // Assert
        Assert.AreEqual("2.1", result.Capabilities.MccsVersion);
    }

    [TestMethod]
    public void Parse_DellU3011_ParsesCommands()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellU3011Capabilities);

        // Assert
        var cmds = result.Capabilities.SupportedCommands;
        Assert.IsNotNull(cmds);
        Assert.AreEqual(7, cmds.Count);
        CollectionAssert.Contains(cmds, (byte)0x01);
        CollectionAssert.Contains(cmds, (byte)0x02);
        CollectionAssert.Contains(cmds, (byte)0x03);
        CollectionAssert.Contains(cmds, (byte)0x07);
        CollectionAssert.Contains(cmds, (byte)0x0C);
        CollectionAssert.Contains(cmds, (byte)0xE3);
        CollectionAssert.Contains(cmds, (byte)0xF3);
    }

    [TestMethod]
    public void Parse_DellU3011_ParsesBrightnessVcpCode()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellU3011Capabilities);

        // Assert - VCP 0x10 is Brightness
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x10));
        var brightnessInfo = result.Capabilities.GetVcpCodeInfo(0x10);
        Assert.IsNotNull(brightnessInfo);
        Assert.AreEqual(0x10, brightnessInfo.Value.Code);
        Assert.IsTrue(brightnessInfo.Value.IsContinuous);
    }

    [TestMethod]
    public void Parse_DellU3011_ParsesContrastVcpCode()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellU3011Capabilities);

        // Assert - VCP 0x12 is Contrast
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x12));
    }

    [TestMethod]
    public void Parse_DellU3011_ParsesInputSourceWithDiscreteValues()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellU3011Capabilities);

        // Assert - VCP 0x60 is Input Source with discrete values
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x60));
        var inputSourceInfo = result.Capabilities.GetVcpCodeInfo(0x60);
        Assert.IsNotNull(inputSourceInfo);
        Assert.IsTrue(inputSourceInfo.Value.HasDiscreteValues);

        // Should have values: 01 03 04 0C 0F 11 12
        var values = inputSourceInfo.Value.SupportedValues;
        Assert.AreEqual(7, values.Count);
        Assert.IsTrue(values.Contains(0x01));
        Assert.IsTrue(values.Contains(0x03));
        Assert.IsTrue(values.Contains(0x04));
        Assert.IsTrue(values.Contains(0x0C));
        Assert.IsTrue(values.Contains(0x0F));
        Assert.IsTrue(values.Contains(0x11));
        Assert.IsTrue(values.Contains(0x12));
    }

    [TestMethod]
    public void Parse_DellU3011_ParsesColorPresetWithDiscreteValues()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellU3011Capabilities);

        // Assert - VCP 0x14 is Color Preset
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x14));
        var colorPresetInfo = result.Capabilities.GetVcpCodeInfo(0x14);
        Assert.IsNotNull(colorPresetInfo);
        Assert.IsTrue(colorPresetInfo.Value.HasDiscreteValues);

        // Should have values: 01 05 08 0B 0C
        var values = colorPresetInfo.Value.SupportedValues;
        Assert.AreEqual(5, values.Count);
        Assert.IsTrue(values.Contains(0x01));
        Assert.IsTrue(values.Contains(0x05));
        Assert.IsTrue(values.Contains(0x08));
        Assert.IsTrue(values.Contains(0x0B));
        Assert.IsTrue(values.Contains(0x0C));
    }

    [TestMethod]
    public void Parse_DellU3011_ParsesPowerModeWithDiscreteValues()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellU3011Capabilities);

        // Assert - VCP 0xD6 is Power Mode
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0xD6));
        var powerModeInfo = result.Capabilities.GetVcpCodeInfo(0xD6);
        Assert.IsNotNull(powerModeInfo);
        Assert.IsTrue(powerModeInfo.Value.HasDiscreteValues);

        // Should have values: 01 04 05
        var values = powerModeInfo.Value.SupportedValues;
        Assert.AreEqual(3, values.Count);
        Assert.IsTrue(values.Contains(0x01));
        Assert.IsTrue(values.Contains(0x04));
        Assert.IsTrue(values.Contains(0x05));
    }

    [TestMethod]
    public void Parse_DellU3011_TotalVcpCodeCount()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellU3011Capabilities);

        // Assert - VCP codes: 02 04 05 06 08 10 12 14 16 18 1A 52 60 AC AE B2 B6 C6 C8 C9 D6 DC DF FD
        Assert.AreEqual(24, result.Capabilities.SupportedVcpCodes.Count);
    }

    [TestMethod]
    public void Parse_DellP2416D_ParsesModel()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellP2416DCapabilities);

        // Assert
        Assert.AreEqual("P2416D", result.Capabilities.Model);
    }

    [TestMethod]
    public void Parse_DellP2416D_ParsesTypeWithDifferentCase()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellP2416DCapabilities);

        // Assert - Type is "LCD" (uppercase) in this monitor
        Assert.AreEqual("LCD", result.Capabilities.Type);
    }

    [TestMethod]
    public void Parse_DellP2416D_ParsesMccsVersion()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellP2416DCapabilities);

        // Assert
        Assert.AreEqual("2.1", result.Capabilities.MccsVersion);
    }

    [TestMethod]
    public void Parse_DellP2416D_ParsesInputSourceWithThreeValues()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellP2416DCapabilities);

        // Assert - VCP 0x60 Input Source has values: 01 11 0F
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x60));
        var inputSourceInfo = result.Capabilities.GetVcpCodeInfo(0x60);
        Assert.IsNotNull(inputSourceInfo);

        var values = inputSourceInfo.Value.SupportedValues;
        Assert.AreEqual(3, values.Count);
        Assert.IsTrue(values.Contains(0x01));
        Assert.IsTrue(values.Contains(0x11));
        Assert.IsTrue(values.Contains(0x0F));
    }

    [TestMethod]
    public void Parse_DellP2416D_ParsesE2WithManyValues()
    {
        // Act
        var result = MccsCapabilitiesParser.Parse(DellP2416DCapabilities);

        // Assert - VCP 0xE2 has values: 00 01 02 04 0E 12 14 19
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0xE2));
        var e2Info = result.Capabilities.GetVcpCodeInfo(0xE2);
        Assert.IsNotNull(e2Info);

        var values = e2Info.Value.SupportedValues;
        Assert.AreEqual(8, values.Count);
    }

    [TestMethod]
    public void Parse_NoOuterParentheses_StillParses()
    {
        // Act - Some monitors like Apple Cinema Display omit outer parens
        var result = MccsCapabilitiesParser.Parse(NoOuterParensCapabilities);

        // Assert
        Assert.AreEqual("monitor", result.Capabilities.Protocol);
        Assert.AreEqual("lcd", result.Capabilities.Type);
        Assert.AreEqual("TestMonitor", result.Capabilities.Model);
        Assert.AreEqual("2.0", result.Capabilities.MccsVersion);
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x10));
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x12));
    }

    [TestMethod]
    public void Parse_ConcatenatedHexFormat_ParsesCorrectly()
    {
        // Act - Some monitors output hex without spaces: cmds(01020307)
        var result = MccsCapabilitiesParser.Parse(ConcatenatedHexCapabilities);

        // Assert
        var cmds = result.Capabilities.SupportedCommands;
        Assert.AreEqual(4, cmds.Count);
        CollectionAssert.Contains(cmds, (byte)0x01);
        CollectionAssert.Contains(cmds, (byte)0x02);
        CollectionAssert.Contains(cmds, (byte)0x03);
        CollectionAssert.Contains(cmds, (byte)0x07);

        // VCP codes without spaces
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x10));
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x12));
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x14));
    }

    [TestMethod]
    public void Parse_NestedParenthesesInVcp_HandlesCorrectly()
    {
        // Arrange - VCP code 0x14 with nested discrete values
        var input = "(vcp(14(01 05 08)))";

        // Act
        var result = MccsCapabilitiesParser.Parse(input);

        // Assert
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x14));
        var vcpInfo = result.Capabilities.GetVcpCodeInfo(0x14);
        Assert.IsNotNull(vcpInfo);
        Assert.AreEqual(3, vcpInfo.Value.SupportedValues.Count);
    }

    [TestMethod]
    public void Parse_MultipleVcpCodesWithMixedFormats_ParsesAll()
    {
        // Arrange - Mixed: some with values, some without
        var input = "(vcp(10 12 14(01 05) 16 60(0F 11)))";

        // Act
        var result = MccsCapabilitiesParser.Parse(input);

        // Assert
        Assert.AreEqual(5, result.Capabilities.SupportedVcpCodes.Count);

        // Continuous codes (no discrete values)
        var brightness = result.Capabilities.GetVcpCodeInfo(0x10);
        Assert.IsTrue(brightness?.IsContinuous ?? false);

        var contrast = result.Capabilities.GetVcpCodeInfo(0x12);
        Assert.IsTrue(contrast?.IsContinuous ?? false);

        // Discrete codes (with values)
        var colorPreset = result.Capabilities.GetVcpCodeInfo(0x14);
        Assert.IsTrue(colorPreset?.HasDiscreteValues ?? false);
        Assert.AreEqual(2, colorPreset?.SupportedValues.Count);

        var inputSource = result.Capabilities.GetVcpCodeInfo(0x60);
        Assert.IsTrue(inputSource?.HasDiscreteValues ?? false);
        Assert.AreEqual(2, inputSource?.SupportedValues.Count);
    }

    [TestMethod]
    public void Parse_UnknownSegments_DoesNotFail()
    {
        // Arrange - Contains unknown segments like mswhql and asset_eep
        var input = "(prot(monitor)mswhql(1)asset_eep(40)vcp(10))";

        // Act
        var result = MccsCapabilitiesParser.Parse(input);

        // Assert
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual("monitor", result.Capabilities.Protocol);
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x10));
    }

    [TestMethod]
    public void Parse_ExtraWhitespace_HandlesCorrectly()
    {
        // Arrange - Extra spaces everywhere
        var input = "(  prot( monitor )  type( lcd )  vcp( 10   12   14( 01  05 ) )  )";

        // Act
        var result = MccsCapabilitiesParser.Parse(input);

        // Assert
        Assert.AreEqual("monitor", result.Capabilities.Protocol);
        Assert.AreEqual("lcd", result.Capabilities.Type);
        Assert.AreEqual(3, result.Capabilities.SupportedVcpCodes.Count);
    }

    [TestMethod]
    public void Parse_LowercaseHex_ParsesCorrectly()
    {
        // Arrange - All lowercase hex
        var input = "(cmds(01 0c e3 f3)vcp(10 ac ae))";

        // Act
        var result = MccsCapabilitiesParser.Parse(input);

        // Assert
        CollectionAssert.Contains(result.Capabilities.SupportedCommands, (byte)0xE3);
        CollectionAssert.Contains(result.Capabilities.SupportedCommands, (byte)0xF3);
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0xAC));
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0xAE));
    }

    [TestMethod]
    public void Parse_MixedCaseHex_ParsesCorrectly()
    {
        // Arrange - Mixed case hex
        var input = "(vcp(Aa Bb cC Dd))";

        // Act
        var result = MccsCapabilitiesParser.Parse(input);

        // Assert
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0xAA));
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0xBB));
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0xCC));
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0xDD));
    }

    [TestMethod]
    public void Parse_MalformedInput_ReturnsPartialResults()
    {
        // Arrange - Missing closing paren for vcp section
        var input = "(prot(monitor)vcp(10 12";

        // Act
        var result = MccsCapabilitiesParser.Parse(input);

        // Assert - Should still parse what it can
        Assert.AreEqual("monitor", result.Capabilities.Protocol);

        // VCP codes should still be parsed
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x10));
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x12));
    }

    [TestMethod]
    public void Parse_InvalidHexInVcp_SkipsAndContinues()
    {
        // Arrange - Contains invalid hex "GG"
        var input = "(vcp(10 GG 12 14))";

        // Act
        var result = MccsCapabilitiesParser.Parse(input);

        // Assert - Should skip invalid and parse valid codes
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x10));
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x12));
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x14));
        Assert.AreEqual(3, result.Capabilities.SupportedVcpCodes.Count);
    }

    [TestMethod]
    public void Parse_SingleCharacterHex_Skipped()
    {
        // Arrange - Single char "A" is not valid (need 2 chars)
        var input = "(vcp(10 A 12))";

        // Act
        var result = MccsCapabilitiesParser.Parse(input);

        // Assert - Should only have 10 and 12
        Assert.AreEqual(2, result.Capabilities.SupportedVcpCodes.Count);
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x10));
        Assert.IsTrue(result.Capabilities.SupportsVcpCode(0x12));
    }

    [TestMethod]
    public void GetVcpCodesAsHexStrings_ReturnsSortedList()
    {
        // Arrange
        var result = MccsCapabilitiesParser.Parse("(vcp(60 10 14 12))");

        // Act
        var hexStrings = result.Capabilities.GetVcpCodesAsHexStrings();

        // Assert - Should be sorted
        Assert.AreEqual(4, hexStrings.Count);
        Assert.AreEqual("0x10", hexStrings[0]);
        Assert.AreEqual("0x12", hexStrings[1]);
        Assert.AreEqual("0x14", hexStrings[2]);
        Assert.AreEqual("0x60", hexStrings[3]);
    }

    [TestMethod]
    public void GetSortedVcpCodes_ReturnsSortedEnumerable()
    {
        // Arrange
        var result = MccsCapabilitiesParser.Parse("(vcp(60 10 14 12))");

        // Act
        var sortedCodes = result.Capabilities.GetSortedVcpCodes().ToList();

        // Assert
        Assert.AreEqual(0x10, sortedCodes[0].Code);
        Assert.AreEqual(0x12, sortedCodes[1].Code);
        Assert.AreEqual(0x14, sortedCodes[2].Code);
        Assert.AreEqual(0x60, sortedCodes[3].Code);
    }

    [TestMethod]
    public void HasDiscreteValues_ContinuousCode_ReturnsFalse()
    {
        // Arrange
        var result = MccsCapabilitiesParser.Parse("(vcp(10))");

        // Act & Assert
        Assert.IsFalse(result.Capabilities.HasDiscreteValues(0x10));
    }

    [TestMethod]
    public void HasDiscreteValues_DiscreteCode_ReturnsTrue()
    {
        // Arrange
        var result = MccsCapabilitiesParser.Parse("(vcp(60(01 11)))");

        // Act & Assert
        Assert.IsTrue(result.Capabilities.HasDiscreteValues(0x60));
    }

    [TestMethod]
    public void GetSupportedValues_DiscreteCode_ReturnsValues()
    {
        // Arrange
        var result = MccsCapabilitiesParser.Parse("(vcp(60(01 11 0F)))");

        // Act
        var values = result.Capabilities.GetSupportedValues(0x60);

        // Assert
        Assert.IsNotNull(values);
        Assert.AreEqual(3, values.Count);
        Assert.IsTrue(values.Contains(0x01));
        Assert.IsTrue(values.Contains(0x11));
        Assert.IsTrue(values.Contains(0x0F));
    }

    [TestMethod]
    public void IsValid_ValidCapabilities_ReturnsTrue()
    {
        // Arrange & Act
        var result = MccsCapabilitiesParser.Parse(DellU3011Capabilities);

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void IsValid_EmptyVcpCodes_ReturnsFalse()
    {
        // Arrange & Act
        var result = MccsCapabilitiesParser.Parse("(prot(monitor)type(lcd))");

        // Assert - No VCP codes = not valid
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void Capabilities_RawProperty_ContainsOriginalString()
    {
        // Arrange & Act
        var result = MccsCapabilitiesParser.Parse(SimpleCapabilities);

        // Assert
        Assert.AreEqual(SimpleCapabilities, result.Capabilities.Raw);
    }
}
