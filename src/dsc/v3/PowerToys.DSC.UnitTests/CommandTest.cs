// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Commands;
using PowerToys.DSC.DSCResources;
using PowerToys.DSC.Models;

namespace PowerToys.DSC.UnitTests;

[TestClass]
public sealed class CommandTest : BaseDscTest
{
    [TestMethod]
    public void Set_EmptyInput_Fail()
    {
        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", SettingsResource.ResourceName, "--module", "Awake");
        var messages = result.Messages();

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(DscMessageLevel.Error, messages[0].Level);
        Assert.AreEqual(GetResourceString("InputEmptyOrNullError"), messages[0].Message);
    }

    [TestMethod]
    public void Test_EmptyInput_Fail()
    {
        // Act
        var result = ExecuteDscCommand<TestCommand>("--resource", SettingsResource.ResourceName, "--module", "Awake");
        var messages = result.Messages();

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(DscMessageLevel.Error, messages[0].Level);
        Assert.AreEqual(GetResourceString("InputEmptyOrNullError"), messages[0].Message);
    }
}
