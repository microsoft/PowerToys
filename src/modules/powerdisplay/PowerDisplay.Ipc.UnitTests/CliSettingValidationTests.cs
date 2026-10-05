// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Ipc;

namespace PowerDisplay.Ipc.UnitTests;

/// <summary>
/// Tests for <see cref="CliSettingValidation"/> — the single source of the discrete supported-value
/// rule shared by the <c>set</c> command and the <c>apply-profile</c> outcomes path.
/// </summary>
[TestClass]
public class CliSettingValidationTests
{
    private static readonly int[] SupportedSet = { 0x01, 0x05, 0x08 };

    [TestMethod]
    public void IsDiscreteValueSupported_NullSet_AcceptsAnyValue()
    {
        // No advertised set → the hardware write is the final arbiter, so accept.
        Assert.IsTrue(CliSettingValidation.IsDiscreteValueSupported(0x99, null));
    }

    [TestMethod]
    public void IsDiscreteValueSupported_EmptySet_AcceptsAnyValue()
    {
        Assert.IsTrue(CliSettingValidation.IsDiscreteValueSupported(0x99, Array.Empty<int>()));
    }

    [TestMethod]
    public void IsDiscreteValueSupported_ValueInSet_ReturnsTrue()
    {
        Assert.IsTrue(CliSettingValidation.IsDiscreteValueSupported(0x05, SupportedSet));
    }

    [TestMethod]
    public void IsDiscreteValueSupported_ValueNotInSet_ReturnsFalse()
    {
        Assert.IsFalse(CliSettingValidation.IsDiscreteValueSupported(0x99, SupportedSet));
    }
}
