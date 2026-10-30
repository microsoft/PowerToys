// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using AdvancedPaste.Models;
using AdvancedPaste.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace AdvancedPaste.UnitTests.ServicesTests;

[TestClass]
public sealed class PowerScriptsServiceTests
{
    [TestMethod]
    public void CompatibilityRequiresPastableOutputWithoutModuleTargeting()
    {
        var clipboard = new PowerScriptClipboard(true, false, []);
        Assert.IsTrue(PowerScriptsService.CanUseClipboard(new() { Input = PowerScriptIO.Text, Output = PowerScriptIO.Text }, clipboard));
        Assert.IsTrue(PowerScriptsService.CanUseClipboard(new() { Input = PowerScriptIO.Text, Output = PowerScriptIO.Image }, clipboard));
        Assert.IsFalse(PowerScriptsService.CanUseClipboard(new() { Input = PowerScriptIO.Text, Output = PowerScriptIO.None }, clipboard));
    }

    [TestMethod]
    public void NoInputScriptsWorkWithAnEmptyClipboardWithoutEnablingTextScripts()
    {
        Assert.IsTrue(PowerScriptsService.CanUseClipboard(new() { Input = PowerScriptIO.None, Output = PowerScriptIO.Text }, PowerScriptClipboard.Empty));
        Assert.IsFalse(PowerScriptsService.CanUseClipboard(new() { Input = PowerScriptIO.Text, Output = PowerScriptIO.Text }, PowerScriptClipboard.Empty));
        var format = PasteFormat.CreatePowerScriptFormat(new() { Id = "action", Name = "Example" }, isEnabled: true);
        Assert.IsTrue(format.IsEnabled);
        Assert.IsTrue(format.SupportsClipboardFormats(ClipboardFormat.None));
        Assert.AreEqual("PowerScript_action", format.AutomationId);
    }

    [TestMethod]
    public void AllAndOnlyClipboardCompatibleScriptsAreOfferedWithoutAssignments()
    {
        var files = new PowerScriptInfo
        {
            Id = "files",
            Input = PowerScriptIO.Files,
            Output = PowerScriptIO.Files,
            InputExtensions = [".txt"],
            InputMinFiles = 1,
            InputMaxFiles = 1,
        };
        var image = MediaScript("image", """{"type":"object","properties":{"mimeType":{"enum":["image/png"]}}}""");
        PowerScriptInfo[] scripts =
        [
            new() { Id = "text", Input = PowerScriptIO.Text, Output = PowerScriptIO.Text },
            new() { Id = "newly-installed", Input = PowerScriptIO.Text, Output = PowerScriptIO.Image },
            files,
            image,
            new() { Id = "no-input", Input = PowerScriptIO.None, Output = PowerScriptIO.Text },
            new() { Id = "no-output", Input = PowerScriptIO.Text, Output = PowerScriptIO.None },
        ];
        var textClipboard = new PowerScriptClipboard(true, false, []);
        var fromText = PowerScriptsService.GetApplicableScripts(scripts, true, textClipboard);
        Assert.AreEqual(2, fromText.Count);
        Assert.AreEqual("text", fromText[0].Id);
        Assert.AreEqual("newly-installed", fromText[1].Id);
        Assert.AreEqual(0, PowerScriptsService.GetApplicableScripts(scripts, false, textClipboard).Count);
        Assert.AreEqual(0, PowerScriptsService.GetApplicableScripts(scripts, true, PowerScriptClipboard.Empty).Count);
        var fromFiles = PowerScriptsService.GetApplicableScripts(scripts, true, new(false, false, [@"C:\a.TXT"]));
        Assert.AreEqual(1, fromFiles.Count);
        Assert.AreSame(files, fromFiles[0]);
        var fromImage = PowerScriptsService.GetApplicableScripts(scripts, true, new(false, true, []));
        Assert.AreEqual(1, fromImage.Count);
        Assert.AreSame(image, fromImage[0]);
    }

    [TestMethod]
    public void FileScriptsRequireEveryExtensionAndTheEntireSelectionCount()
    {
        var script = new PowerScriptInfo
        {
            Input = PowerScriptIO.Files,
            Output = PowerScriptIO.Files,
            InputExtensions = [".txt"],
            InputMinFiles = 2,
            InputMaxFiles = 3,
        };
        Assert.IsFalse(PowerScriptsService.CanUseClipboard(script, new(false, false, [@"C:\a.txt"])));
        Assert.IsTrue(PowerScriptsService.CanUseClipboard(script, new(false, false, [@"C:\a.txt", @"C:\B.TXT"])));
        Assert.IsFalse(PowerScriptsService.CanUseClipboard(script, new(false, false, [@"C:\a.txt", @"C:\b.png"])));
        Assert.IsFalse(PowerScriptsService.CanUseClipboard(script, new(false, false, [@"C:\a.txt", @"C:\b.txt", @"C:\c.txt", @"C:\d.txt"])));
        Assert.IsFalse(PowerScriptsService.CanUseClipboard(script, new(false, true, [])));
    }

    [TestMethod]
    public void InlineImagesUseTheirMimeAndCollectionContractNotGenericImageFlags()
    {
        var scalar = MediaScript("image", """{"type":"object","properties":{"mimeType":{"enum":["image/jpeg"]}}}""");
        Assert.IsTrue(PowerScriptsService.CanUseClipboard(scalar, new(false, true, [])));
        Assert.IsTrue(PowerScriptsService.CanUseClipboard(scalar, new(false, false, [@"C:\photo.jpg"])));
        Assert.IsFalse(PowerScriptsService.CanUseClipboard(scalar, new(false, false, [@"C:\photo.png"])));
        Assert.IsFalse(PowerScriptsService.CanUseClipboard(scalar, new(false, false, [@"C:\a.jpg", @"C:\b.jpg"])));
        var collection = MediaScript("image", """{"type":"array","minItems":2,"maxItems":3,"items":{"type":"object","properties":{"mimeType":{"enum":["image/png","image/webp"]}}}}""");
        Assert.IsTrue(PowerScriptsService.CanUseClipboard(collection, new(false, false, [@"C:\a.png", @"C:\b.webp"])));
        Assert.IsFalse(PowerScriptsService.CanUseClipboard(collection, new(false, true, [])));
    }

    [TestMethod]
    public void AudioDoesNotAcceptVideoOrUnsupportedCodecsAndEmptyMediaIsNotInput()
    {
        var audio = MediaScript("audio", """{"type":"array","minItems":0,"items":{"type":"object","properties":{"mimeType":{"enum":["audio/wav","audio/mpeg"]}}}}""");
        Assert.IsTrue(PowerScriptsService.CanUseClipboard(audio, new(false, false, [@"C:\a.wav", @"C:\b.mp3"])));
        Assert.IsFalse(PowerScriptsService.CanUseClipboard(audio, new(false, false, [@"C:\video.mp4"])));
        Assert.IsFalse(PowerScriptsService.CanUseClipboard(audio, new(false, false, [@"C:\audio.flac"])));
        Assert.IsFalse(PowerScriptsService.CanUseClipboard(audio, PowerScriptClipboard.Empty));
        var webp = MediaScript("image", """{"type":"object","properties":{"mimeType":{"enum":["image/webp"]}}}""");
        Assert.IsFalse(PowerScriptsService.CanUseClipboard(webp, new(false, true, [])));
        Assert.IsTrue(PowerScriptsService.CanUseClipboard(webp, new(false, false, [@"C:\photo.webp"])));
    }

    [TestMethod]
    public void ParametersPreserveFalseZeroEmptyAndOmittedDefaults()
    {
        PowerScriptParameter[] parameters =
        [
            new("enabled", "bool", true, null, null, null, [], null, null),
            new("count", "int", true, null, null, null, [], 0, 10),
            new("label", "string", true, null, null, null, [], null, null),
            new("defaulted", "string", true, null, null, "inherited", [], null, null),
            new("optional", "string", false, null, null, null, [], null, null),
        ];
        var result = PowerScriptsService.CollectParameters(parameters, new Dictionary<string, string>
        {
            ["enabled"] = "false",
            ["count"] = "0",
            ["label"] = string.Empty,
        });
        Assert.IsFalse(result["enabled"].GetBoolean());
        Assert.AreEqual(0, result["count"].GetInt32());
        Assert.AreEqual(string.Empty, result["label"].GetString());
        Assert.IsFalse(result.ContainsKey("defaulted"));
        Assert.IsFalse(result.ContainsKey("optional"));
    }

    [TestMethod]
    public void InvalidRequiredEnumAndIntegerValuesAreRejectedBeforeExecution()
    {
        var choice = new PowerScriptParameter("style", "choice", true, null, null, null, ["short", "full"], null, null);
        Assert.ThrowsExactly<ArgumentException>(() => PowerScriptsService.CollectParameters([choice], new Dictionary<string, string>()));
        Assert.ThrowsExactly<ArgumentException>(() => PowerScriptsService.CollectParameters([choice], new Dictionary<string, string> { ["style"] = "other" }));
        var count = new PowerScriptParameter("count", "int", true, null, null, null, [], 1, 10);
        Assert.ThrowsExactly<ArgumentException>(() => PowerScriptsService.CollectParameters([count], new Dictionary<string, string> { ["count"] = "0" }));
        Assert.ThrowsExactly<ArgumentException>(() => PowerScriptsService.CollectParameters([count], new Dictionary<string, string> { ["count"] = "1.5" }));
        Assert.ThrowsExactly<ArgumentException>(() => PowerScriptsService.CollectParameters([count], new Dictionary<string, string> { ["count"] = "2147483648" }));
    }

    private static PowerScriptInfo MediaScript(string category, string primary)
    {
        using var document = JsonDocument.Parse("{\"properties\":{\"" + category + "\":" + primary + "}}");
        return new()
        {
            Input = category == "image" ? PowerScriptIO.Image : PowerScriptIO.Audio,
            Output = PowerScriptIO.Text,
            InputSchema = document.RootElement.Clone(),
        };
    }
}
