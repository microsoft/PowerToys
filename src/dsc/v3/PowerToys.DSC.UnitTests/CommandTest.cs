// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Commands;
using PowerToys.DSC.DSCResources;

namespace PowerToys.DSC.UnitTests;

[TestClass]
public sealed class CommandTest : BaseDscTest
{
    [TestMethod]
    public void GetResource_Found_Success()
    {
        // Act
        var result = ExecuteDscCommand<GetCommand>("--resource", SettingsResource.ResourceName);

        // Assert
        Assert.IsTrue(result.Success);
    }

    [TestMethod]
    public void GetResource_NotFound_Fail()
    {
        // Arrange
        var availableResources = string.Join(", ", BaseCommand.AvailableResources);

        // Act
        var result = ExecuteDscCommand<GetCommand>("--resource", "ResourceNotFound");

        // Assert
        Assert.IsFalse(result.Success);
        Assert.Contains(GetResourceString("InvalidResourceNameError", availableResources), result.Error);
    }
}
