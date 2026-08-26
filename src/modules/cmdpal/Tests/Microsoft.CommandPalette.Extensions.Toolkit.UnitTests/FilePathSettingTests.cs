// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class FilePathSettingTests
{
    private static readonly string[] _fileTypeFilters = [".exe", "*.cmd"];

    [TestMethod]
    public void FilePathSetting_FileMode_EmitsPickerAndValidationOptions()
    {
        var setting = new FilePathSetting(
            "executable",
            "Executable",
            "Choose an executable",
            @"C:\Tools\app.exe",
            FilePathSelectionMode.File)
        {
            FileTypeFilter = [".exe", "*.cmd"],
            Placeholder = "Executable path",
            ValidationPattern = @"(?i)^.+\.(exe|cmd)$",
            ValidationErrorMessage = "Choose an executable.",
        };

        var dictionary = setting.ToDictionary();

        Assert.AreEqual("Input.CommandPalette.FilePath", dictionary["type"]);
        Assert.AreEqual("file", dictionary["selectionMode"]);
        Assert.AreEqual("Executable path", dictionary["placeholder"]);
        Assert.AreEqual(@"(?i)^.+\.(exe|cmd)$", dictionary["validationPattern"]);
        Assert.AreEqual("Choose an executable.", dictionary["validationErrorMessage"]);
        CollectionAssert.AreEqual(
            _fileTypeFilters,
            ((List<string>)dictionary["fileTypeFilter"]).ToArray());
    }

    [TestMethod]
    public void FilePathSetting_FolderMode_EmitsFolderSelectionMode()
    {
        var setting = new FilePathSetting(
            "folder",
            @"C:\Tools",
            FilePathSelectionMode.Folder);

        Assert.AreEqual("folder", setting.ToDictionary()["selectionMode"]);
    }

    [TestMethod]
    public void FilePathSetting_DefaultsToFileMode()
    {
        var setting = new FilePathSetting("path", string.Empty);

        Assert.AreEqual(FilePathSelectionMode.File, setting.SelectionMode);
        Assert.AreEqual("file", setting.ToDictionary()["selectionMode"]);
    }

    [TestMethod]
    public void FilePathSetting_ToForm_UsesCustomElementType()
    {
        var setting = new FilePathSetting("path", string.Empty);

        var form = JsonNode.Parse(setting.ToForm())!.AsObject();
        var input = form["body"]!.AsArray()[0]!.AsObject();

        Assert.AreEqual("Input.CommandPalette.FilePath", input["type"]!.GetValue<string>());
        Assert.AreEqual("file", input["selectionMode"]!.GetValue<string>());
    }

    [TestMethod]
    public void FilePathSetting_FallsBackToTextInputUnderTheSameId()
    {
        var setting = new FilePathSetting("path", "Executable", "Choose one", @"C:\Tools\app.exe");

        var form = JsonNode.Parse(setting.ToForm())!.AsObject();
        var fallback = form["body"]!.AsArray()[0]!["fallback"]!.AsObject();

        Assert.AreEqual("Input.Text", fallback["type"]!.GetValue<string>());
        Assert.AreEqual("path", fallback["id"]!.GetValue<string>());
        Assert.AreEqual(@"C:\Tools\app.exe", fallback["value"]!.GetValue<string>());
    }

    [TestMethod]
    public void FilePathSetting_UpdateStoresSubmittedPath()
    {
        var setting = new FilePathSetting("path", string.Empty);

        setting.Update(new JsonObject { ["path"] = @"C:\Tools\app.exe" });

        Assert.AreEqual(@"C:\Tools\app.exe", setting.Value);
    }

    [TestMethod]
    public void FilePathSetting_InvalidOptions_Throw()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            new FilePathSetting("path", string.Empty, (FilePathSelectionMode)42));

        var setting = new FilePathSetting("path", string.Empty);
        Assert.ThrowsException<ArgumentException>(() => setting.ValidationPattern = "[");
    }
}
