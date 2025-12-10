// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Commands;
using PowerToys.DSC.DSCResources;
using PowerToys.DSC.Models;

namespace PowerToys.DSC.UnitTests.SettingsResourceTests;

[TestClass]
public sealed class SettingsResourceCommandTest : BaseDscTest
{
    [TestMethod]
    public void Modules_ListAllSupportedModules()
    {
        // Arrange
        var expectedModules = new List<string>()
        {
            SettingsResource.AppModule,
            nameof(ModuleType.AdvancedPaste),
            nameof(ModuleType.AlwaysOnTop),
            nameof(ModuleType.Awake),
            nameof(ModuleType.ColorPicker),
            nameof(ModuleType.CropAndLock),
            nameof(ModuleType.EnvironmentVariables),
            nameof(ModuleType.FancyZones),
            nameof(ModuleType.FileLocksmith),
            nameof(ModuleType.FindMyMouse),
            nameof(ModuleType.Hosts),
            nameof(ModuleType.ImageResizer),
            nameof(ModuleType.KeyboardManager),
            nameof(ModuleType.MouseHighlighter),
            nameof(ModuleType.MouseJump),
            nameof(ModuleType.MousePointerCrosshairs),
            nameof(ModuleType.Peek),
            nameof(ModuleType.PowerRename),
            nameof(ModuleType.PowerAccent),
            nameof(ModuleType.RegistryPreview),
            nameof(ModuleType.MeasureTool),
            nameof(ModuleType.ShortcutGuide),
            nameof(ModuleType.PowerOCR),
            nameof(ModuleType.Workspaces),
            nameof(ModuleType.ZoomIt),
        };

        // Act
        var result = ExecuteDscCommand<ModulesCommand>("--resource", SettingsResource.ResourceName);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(string.Join(Environment.NewLine, expectedModules.Order()), result.Output.Trim());
    }

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
