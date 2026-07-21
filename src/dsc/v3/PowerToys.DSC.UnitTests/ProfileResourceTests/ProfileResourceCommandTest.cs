// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Commands;
using PowerToys.DSC.DSCResources;
using PowerToys.DSC.Models;

namespace PowerToys.DSC.UnitTests.ProfileResourceTests;

[TestClass]
public sealed class ProfileResourceCommandTest : BaseDscTest
{
    [TestMethod]
    public void Modules_ListsOnlyKeyboardManager()
    {
        // Act
        var result = ExecuteDscCommand<ModulesCommand>("--resource", ProfileResource.ResourceName);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(nameof(ModuleType.KeyboardManager), result.Output.Trim());
    }

    [TestMethod]
    public void UnsupportedModule_Fail()
    {
        // Act
        var result = ExecuteDscCommand<GetCommand>("--resource", ProfileResource.ResourceName, "--module", "Awake");
        var messages = result.Messages();

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(DscMessageLevel.Error, messages[0].Level);
        Assert.AreEqual(GetResourceString("ModuleNotSupportedByResource", "Awake", ProfileResource.ResourceName), messages[0].Message);
    }

    [TestMethod]
    public void Schema_ContainsRequiredProfileProperty()
    {
        // Act
        var result = ExecuteDscCommand<SchemaCommand>("--resource", ProfileResource.ResourceName, "--module", nameof(ModuleType.KeyboardManager));

        // Assert
        Assert.IsTrue(result.Success);
        var schema = JsonNode.Parse(result.Output);
        Assert.IsNotNull(schema);
        Assert.IsNotNull(schema["properties"]?["profile"]);
        var required = schema["required"]?.AsArray();
        Assert.IsNotNull(required);
        Assert.IsTrue(required.ToString().Contains("profile"));
    }

    [TestMethod]
    public void Set_EmptyInput_Fail()
    {
        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", ProfileResource.ResourceName, "--module", nameof(ModuleType.KeyboardManager));
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
        var result = ExecuteDscCommand<TestCommand>("--resource", ProfileResource.ResourceName, "--module", nameof(ModuleType.KeyboardManager));
        var messages = result.Messages();

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(DscMessageLevel.Error, messages[0].Level);
        Assert.AreEqual(GetResourceString("InputEmptyOrNullError"), messages[0].Message);
    }
}
