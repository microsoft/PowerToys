// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using EnvironmentVariablesUILib.Helpers;

namespace EnvironmentVariablesUILib.UnitTests.Helpers;

[TestClass]
public class EnvironmentVariablesHelperValidationTests
{
    private static readonly string Name259Chars = new('A', 259);
    private static readonly string Name260Chars = new('A', 260);

    // Variable/Profile name
    [TestMethod]
    [DataRow("ValidName", true)]
    [DataRow("valid_name_123", true)]
    [DataRow("", false)]
    [DataRow("   ", false)]
    [DataRow(" leading", false)]
    [DataRow("trailing ", false)]
    [DataRow("has=equals", false)]
    public void TryValidateVariableName_BasicCases_ReturnsExpected(string name, bool expected)
    {
        Assert.AreEqual(expected, EnvironmentVariablesHelper.TryValidateVariableName(name, out _));
    }

    [TestMethod]
    [DataRow("\n")]
    [DataRow("\r")]
    [DataRow("\x01")]
    [DataRow("\x1F")]
    [DataRow("\x7F")]
    public void TryValidateVariableName_ControlCharacters_ReturnsFalse(string name)
    {
        Assert.IsFalse(EnvironmentVariablesHelper.TryValidateVariableName(name, out _));
    }

    [TestMethod]
    public void TryValidateVariableName_AtAuthoringLimit_ReturnsTrue()
    {
        Assert.IsTrue(EnvironmentVariablesHelper.TryValidateVariableName(Name259Chars, out _));
    }

    [TestMethod]
    public void TryValidateVariableName_ExceedsAuthoringLimit_ReturnsFalse()
    {
        Assert.IsFalse(EnvironmentVariablesHelper.TryValidateVariableName(Name260Chars, out _));
    }

    [TestMethod]
    public void TryValidateProfileName_WithEquals_ReturnsFalse()
    {
        Assert.IsFalse(EnvironmentVariablesHelper.TryValidateProfileName("My=Profile", out _));
    }

    // Variable value
    [TestMethod]
    public void TryValidateVariableValue_NullValue_ReturnsTrue()
    {
        Assert.IsTrue(EnvironmentVariablesHelper.TryValidateVariableValue(null, out _));
    }

    [TestMethod]
    public void TryValidateVariableValue_EmptyValue_ReturnsTrue()
    {
        Assert.IsTrue(EnvironmentVariablesHelper.TryValidateVariableValue(string.Empty, out _));
    }

    [TestMethod]
    public void TryValidateVariableValue_WithNullChar_ReturnsFalse()
    {
        Assert.IsFalse(EnvironmentVariablesHelper.TryValidateVariableValue("test\0value", out _));
    }

    // Combined name + value length
    [TestMethod]
    public void TryValidateVariable_CombinedLengthAtLimit_ReturnsTrue()
    {
        // name(1) + '='(1) + value(32764) = 32766 == limit
        var value = new string('V', 32764);
        Assert.IsTrue(EnvironmentVariablesHelper.TryValidateVariable("N", value, out _));
    }

    [TestMethod]
    public void TryValidateVariable_CombinedLengthExceedsLimit_ReturnsFalse()
    {
        // name(1) + '='(1) + value(32765) = 32767 > 32766
        var value = new string('V', 32765);
        Assert.IsFalse(EnvironmentVariablesHelper.TryValidateVariable("N", value, out _));
    }

    // Backup variable - exempt from the authoring limit
    [TestMethod]
    public void TryValidateBackupVariable_NameExceedsAuthoringLimit_ReturnsTrue()
    {
        Assert.IsTrue(EnvironmentVariablesHelper.TryValidateBackupVariable(Name260Chars, "value", out _));
    }

    [TestMethod]
    public void TryValidateBackupVariable_WithEquals_ReturnsFalse()
    {
        // enforceAuthoringLimits:false exempts only the length limit - '=' is still rejected.
        Assert.IsFalse(EnvironmentVariablesHelper.TryValidateBackupVariable("backup=name", "value", out _));
    }

    [TestMethod]
    public void TryValidateBackupVariable_CombinedLengthAtLimit_ReturnsTrue()
    {
        // name(1) + '='(1) + value(32764) = 32766 == limit, with authoring limit exempt
        var value = new string('V', 32764);
        Assert.IsTrue(EnvironmentVariablesHelper.TryValidateBackupVariable("N", value, out _));
    }

    [TestMethod]
    public void TryValidateBackupVariable_CombinedLengthExceedsLimit_ReturnsFalse()
    {
        var value = new string('V', 32765);
        Assert.IsFalse(EnvironmentVariablesHelper.TryValidateBackupVariable("N", value, out _));
    }

    // TryValidateVariable - null value (delete path)
    [TestMethod]
    public void TryValidateVariable_NullValue_ReturnsTrue()
    {
        // null means "delete" - the combined length guard must handle value?.Length ?? 0 safely.
        Assert.IsTrue(EnvironmentVariablesHelper.TryValidateVariable("N", null, out _));
    }

    // Error messages
    [TestMethod]
    [DataRow(" leading", "whitespace")]
    [DataRow("has=equals", "'='")]
    [DataRow("\x01", "control")] // non-whitespace control char - reaches the char check loop
    [DataRow("Name\x01Value", "control")] // embedded control char in a longer name
    public void TryValidateVariableName_InvalidInput_ErrorMessageDescribesReason(
        string name, string expectedFragment)
    {
        EnvironmentVariablesHelper.TryValidateVariableName(name, out string errorMessage);
        StringAssert.Contains(errorMessage, expectedFragment, System.StringComparison.OrdinalIgnoreCase);
    }
}
