// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Models.KeyboardManager;

namespace PowerToys.DSC.UnitTests.KeyboardManager;

[TestClass]
public sealed class KbmProfileConverterTests
{
    // Profile JSON in the exact shape written by the C++ editor
    // (MappingConfiguration::SaveSettingsToFile), covering all four sections,
    // a chord, run-program and open-URI entries with the "*Unsupported*"
    // back-compat text, and an app-specific exact-match shortcut.
    private const string EditorProfileJson = /*lang=json,strict*/ """
        {
            "remapKeys": {
                "inProcess": [
                    { "originalKeys": "20", "newRemapKeys": "27" },
                    { "originalKeys": "45", "newRemapKeys": "256" }
                ]
            },
            "remapKeysToText": {
                "inProcess": [
                    { "originalKeys": "113", "unicodeText": "hello" }
                ]
            },
            "remapShortcuts": {
                "global": [
                    { "originalKeys": "162;65", "exactMatch": false, "newRemapKeys": "163;86", "operationType": 0 },
                    { "originalKeys": "260;79;75", "exactMatch": false, "runProgramElevationLevel": 1, "operationType": 1, "runProgramAlreadyRunningAction": 2, "runProgramStartWindowType": 3, "runProgramFilePath": "cmd.exe", "runProgramArgs": "", "runProgramStartInDir": "", "unicodeText": "*Unsupported*" },
                    { "originalKeys": "17;18;66", "exactMatch": false, "runProgramElevationLevel": 0, "operationType": 2, "openUri": "https://example.com", "unicodeText": "*Unsupported*" }
                ],
                "appSpecific": [
                    { "originalKeys": "17;18;78", "exactMatch": true, "newRemapKeys": "17;83", "operationType": 0, "targetApp": "Notepad.exe" }
                ]
            },
            "remapShortcutsToText": {
                "global": [
                    { "originalKeys": "17;18;84", "exactMatch": false, "unicodeText": "typed" }
                ],
                "appSpecific": []
            }
        }
        """;

    [TestMethod]
    public void FromProfile_ConvertsEditorProfile()
    {
        // Arrange
        var profile = JsonSerializer.Deserialize<KeyboardManagerProfile>(EditorProfileJson);

        // Act
        var model = KbmProfileConverter.FromProfile(profile);

        // Assert: keys sorted by virtual-key code
        Assert.AreEqual(3, model.Keys.Count);
        Assert.AreEqual("CapsLock", model.Keys[0].From);
        Assert.AreEqual("Esc", model.Keys[0].To);
        Assert.AreEqual("Insert", model.Keys[1].From);
        Assert.AreEqual("Disable", model.Keys[1].To);
        Assert.AreEqual("F2", model.Keys[2].From);
        Assert.AreEqual("hello", model.Keys[2].ToText);

        // Assert: shortcuts sorted by (targetApp, from); global entries first
        Assert.AreEqual(5, model.Shortcuts.Count);

        Assert.AreEqual("Ctrl+Alt+B", model.Shortcuts[0].From);
        Assert.AreEqual("https://example.com", model.Shortcuts[0].OpenUri);
        Assert.IsNull(model.Shortcuts[0].ExactMatch);

        Assert.AreEqual("Ctrl+Alt+T", model.Shortcuts[1].From);
        Assert.AreEqual("typed", model.Shortcuts[1].ToText);

        Assert.AreEqual("LCtrl+A", model.Shortcuts[2].From);
        Assert.AreEqual("RCtrl+V", model.Shortcuts[2].To);

        // The chord second key is recovered from the trailing stored key even
        // though the editor does not write a secondKeyOfChord property
        Assert.AreEqual("Win+O, K", model.Shortcuts[3].From);
        var runProgram = model.Shortcuts[3].RunProgram;
        Assert.IsNotNull(runProgram);
        Assert.AreEqual("cmd.exe", runProgram.FilePath);
        Assert.IsNull(runProgram.Args);
        Assert.IsNull(runProgram.StartInDir);
        Assert.AreEqual("elevated", runProgram.Elevation);
        Assert.AreEqual("doNothing", runProgram.IfRunning);
        Assert.AreEqual("maximized", runProgram.WindowStyle);

        Assert.AreEqual("Ctrl+Alt+N", model.Shortcuts[4].From);
        Assert.AreEqual("Ctrl+S", model.Shortcuts[4].To);
        Assert.AreEqual("notepad.exe", model.Shortcuts[4].TargetApp);
        Assert.AreEqual(true, model.Shortcuts[4].ExactMatch);
    }

    [TestMethod]
    public void ToProfile_ProducesEditorCompatibleShape()
    {
        // Arrange
        var model = new KbmProfileModel
        {
            Keys =
            [
                new() { From = "CapsLock", To = "Esc" },
                new() { From = "F2", ToText = "hello" },
            ],
            Shortcuts =
            [
                new() { From = "Ctrl+Shift+A", To = "Ctrl+V" },
                new() { From = "Win+O, K", RunProgram = new() { FilePath = "cmd.exe", Elevation = "elevated" } },
                new() { From = "Ctrl+Alt+B", OpenUri = "https://example.com" },
                new() { From = "Ctrl+Alt+N", To = "Ctrl+S", TargetApp = "Notepad.exe", ExactMatch = true },
                new() { From = "Ctrl+Alt+T", ToText = "typed" },
            ],
        };

        // Act
        var profile = KbmProfileConverter.ToProfile(model);

        // Assert
        Assert.AreEqual("20", profile.RemapKeys.InProcessRemapKeys[0].OriginalKeys);
        Assert.AreEqual("27", profile.RemapKeys.InProcessRemapKeys[0].NewRemapKeys);
        Assert.AreEqual("113", profile.RemapKeysToText.InProcessRemapKeys[0].OriginalKeys);
        Assert.AreEqual("hello", profile.RemapKeysToText.InProcessRemapKeys[0].NewRemapString);

        var global = profile.RemapShortcuts.GlobalRemapShortcuts;
        Assert.AreEqual(3, global.Count);
        Assert.AreEqual("17;16;65", global[0].OriginalKeys);
        Assert.AreEqual("17;86", global[0].NewRemapKeys);
        Assert.AreEqual(false, global[0].ExactMatch);

        Assert.AreEqual("260;79;75", global[1].OriginalKeys);
        Assert.AreEqual(75u, global[1].SecondKeyOfChord);
        Assert.AreEqual(1, global[1].OperationType);
        Assert.AreEqual("cmd.exe", global[1].RunProgramFilePath);
        Assert.AreEqual(string.Empty, global[1].RunProgramArgs);
        Assert.AreEqual(string.Empty, global[1].RunProgramStartInDir);
        Assert.AreEqual(1, global[1].RunProgramElevationLevel);
        Assert.AreEqual(0, global[1].RunProgramAlreadyRunningAction);
        Assert.AreEqual(0, global[1].RunProgramStartWindowType);
        Assert.AreEqual("*Unsupported*", global[1].NewRemapString);

        Assert.AreEqual("17;18;66", global[2].OriginalKeys);
        Assert.AreEqual(2, global[2].OperationType);
        Assert.AreEqual("https://example.com", global[2].OpenUri);
        Assert.AreEqual("*Unsupported*", global[2].NewRemapString);

        var appSpecific = profile.RemapShortcuts.AppSpecificRemapShortcuts;
        Assert.AreEqual(1, appSpecific.Count);
        Assert.AreEqual("17;18;78", appSpecific[0].OriginalKeys);
        Assert.AreEqual("17;83", appSpecific[0].NewRemapKeys);
        Assert.AreEqual("notepad.exe", appSpecific[0].TargetApp);
        Assert.AreEqual(true, appSpecific[0].ExactMatch);

        var globalText = profile.RemapShortcutsToText.GlobalRemapShortcuts;
        Assert.AreEqual(1, globalText.Count);
        Assert.AreEqual("17;18;84", globalText[0].OriginalKeys);
        Assert.AreEqual("typed", globalText[0].NewRemapString);
    }

    [TestMethod]
    public void Canonicalize_RoundTripsEditorProfile()
    {
        // Converting the editor profile to the friendly model and back to a
        // profile must normalize identically on a second pass; this is the
        // invariant Test/Set idempotence relies on.
        var profile = JsonSerializer.Deserialize<KeyboardManagerProfile>(EditorProfileJson);
        var model = KbmProfileConverter.FromProfile(profile);
        var canonical = KbmProfileConverter.Canonicalize(model);

        var modelJson = JsonSerializer.SerializeToNode(model);
        var canonicalJson = JsonSerializer.SerializeToNode(canonical);
        Assert.IsTrue(JsonNode.DeepEquals(modelJson, canonicalJson), $"{modelJson} != {canonicalJson}");
    }

    [TestMethod]
    public void Canonicalize_NormalizesAuthoredVariations()
    {
        // Arrange: same remappings expressed with aliases, different casing,
        // non-canonical modifier order, explicit defaults, and different
        // entry ordering
        var authored = new KbmProfileModel
        {
            Keys =
            [
                new() { From = "escape", To = "return" },
                new() { From = "capslock", To = "esc" },
            ],
            Shortcuts =
            [
                new() { From = "shift+ctrl+a", To = "ctrl+v", ExactMatch = false },
                new() { From = "Ctrl+Alt+N", To = "Ctrl+S", TargetApp = "  NOTEPAD.EXE " },
            ],
        };

        var canonicalEquivalent = new KbmProfileModel
        {
            Keys =
            [
                new() { From = "CapsLock", To = "Esc" },
                new() { From = "Esc", To = "Enter" },
            ],
            Shortcuts =
            [
                new() { From = "Ctrl+Shift+A", To = "Ctrl+V" },
                new() { From = "Ctrl+Alt+N", To = "Ctrl+S", TargetApp = "notepad.exe" },
            ],
        };

        // Act
        var canonical = KbmProfileConverter.Canonicalize(authored);

        // Assert
        var canonicalJson = JsonSerializer.SerializeToNode(canonical);
        var expectedJson = JsonSerializer.SerializeToNode(canonicalEquivalent);
        Assert.IsTrue(JsonNode.DeepEquals(canonicalJson, expectedJson), $"{canonicalJson} != {expectedJson}");
    }

    [TestMethod]
    public void Validate_ValidModel_NoErrors()
    {
        var model = new KbmProfileModel
        {
            Keys = [new() { From = "CapsLock", To = "Esc" }],
            Shortcuts =
            [
                new() { From = "Ctrl+Shift+A", To = "Ctrl+V" },
                new() { From = "Ctrl+Shift+A", To = "Ctrl+V", TargetApp = "notepad.exe" },
                new() { From = "Win+O, K", ToText = "chord" },
                new() { From = "Ctrl+Alt+T", RunProgram = new() { FilePath = "cmd.exe", Elevation = "Elevated", IfRunning = "close", WindowStyle = "hidden" } },
                new() { From = "Ctrl+Alt+B", OpenUri = "https://example.com" },
            ],
        };

        var errors = KbmProfileConverter.Validate(model);

        Assert.AreEqual(0, errors.Count, string.Join("; ", errors));
    }

    [TestMethod]
    public void Validate_InvalidModel_ReportsErrors()
    {
        var model = new KbmProfileModel
        {
            Keys =
            [
                new() { From = "CapsLok", To = "Esc" },
                new() { From = "Insert" },
                new() { From = "Home", To = "Esc", ToText = "both" },
                new() { From = "Disable", To = "Esc" },
                new() { From = "Tab", To = "Win+O, K" },
                new() { From = "F3", To = "Esc" },
                new() { From = "F3", To = "Tab" },
            ],
            Shortcuts =
            [
                new() { From = "A", To = "Ctrl+V" },
                new() { From = "Ctrl+Shift+A", To = "Ctrl+V" },
                new() { From = "Shift+Ctrl+A", To = "Ctrl+C" },
                new() { From = "Ctrl+Alt+T", RunProgram = new() { FilePath = string.Empty } },
                new() { From = "Ctrl+Alt+U", RunProgram = new() { FilePath = "cmd.exe", Elevation = "root" } },
                new() { From = "Ctrl+Alt+V", OpenUri = string.Empty },
            ],
        };

        var errors = KbmProfileConverter.Validate(model);

        AssertHasError(errors, "keys[0].from: Invalid key name 'CapsLok'");
        AssertHasError(errors, "keys[1] must set exactly one of");
        AssertHasError(errors, "keys[2] must set exactly one of");
        AssertHasError(errors, "keys[3].from: 'Disable' cannot be remapped");
        AssertHasError(errors, "keys[4].to: Chords are not supported in remap targets");
        AssertHasError(errors, "keys[6].from: key 'F3' is remapped more than once");
        AssertHasError(errors, "shortcuts[0].from: a shortcut requires at least one modifier");
        AssertHasError(errors, "shortcuts[2].from: shortcut 'Ctrl+Shift+A' is remapped more than once globally");
        AssertHasError(errors, "shortcuts[3].runProgram.filePath must not be empty");
        AssertHasError(errors, "shortcuts[4].runProgram.elevation: invalid value 'root'");
        AssertHasError(errors, "shortcuts[5].openUri must not be empty");
    }

    private static void AssertHasError(System.Collections.Generic.IList<string> errors, string expectedFragment)
    {
        Assert.IsTrue(errors.Any(e => e.Contains(expectedFragment, System.StringComparison.Ordinal)), $"Expected an error containing '{expectedFragment}'; got: {string.Join(" | ", errors)}");
    }
}
