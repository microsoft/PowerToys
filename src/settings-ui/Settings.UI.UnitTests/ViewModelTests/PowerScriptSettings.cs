// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests;

[TestClass]
public class PowerScriptSettings
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly string[] WeakenedGlobalPolicies = { PowerScriptListItem.NetworkPolicy, PowerScriptListItem.UiPolicy };
    private static readonly string[] TitlePath = { "title" };
    private static readonly string[] RuntimePath = { "x-powerscript", "runtime" };
    private static readonly string[] ConsumersPath = { "x-powerscript", "consumers" };
    private static readonly string[] MaxFilesPath = { "x-powerscript", "maxFiles" };
    private static readonly string[] ArgMapPath = { "x-execute", "argMap" };
    private static readonly string[] RequiredPath = { "inputSchema", "required" };

    [TestMethod]
    public void UpdatingCatalogueMetadataNotifiesBindingsWithoutInvalidatingDraft()
    {
        var script = new PowerScriptListItem
        {
            Id = "original",
            Name = "Old title",
            EntryFullPath = @"C:\scripts\action.ps1",
            Trusted = true,
        };
        script.InvalidateSettings();
        var revision = script.SettingsRevision;
        var notifications = new System.Collections.Generic.HashSet<string>();
        script.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        script.UpdateMetadata(new PowerScriptListItem
        {
            Id = "updated",
            Name = "New title",
            EntryFullPath = script.EntryFullPath,
            Io = new() { Input = "text", Output = "files" },
            Consumers = new() { "color-picker" },
            ConfigurationErrors = new() { "Review configuration" },
            Trusted = false,
        });

        Assert.AreEqual(revision, script.SettingsRevision, "Catalogue refresh must not discard a draft in another editor.");
        Assert.AreEqual("New title", script.Name);
        Assert.AreEqual("text", script.Io.Input);
        Assert.AreEqual("files", script.Io.Output);
        Assert.AreEqual("color-picker", script.ConsumersDisplay);
        Assert.IsTrue(script.HasConfigurationErrors);
        Assert.IsFalse(script.Trusted, "An updated trust decision must not remain stale in the card.");
        Assert.IsTrue(notifications.Contains(nameof(PowerScriptListItem.Name)));
        Assert.IsTrue(notifications.Contains(nameof(PowerScriptListItem.CatalogueSummary)));
        Assert.IsTrue(notifications.Contains(nameof(PowerScriptListItem.TrustDisplay)));
        Assert.IsFalse(notifications.Contains(string.Empty), "Whole-item rebinding must not recreate an active editor.");
    }

    [TestMethod]
    public void UpdatingMetadataDoesNotRetainAnObsoleteIsolationApproval()
    {
        var script = new PowerScriptListItem();
        script.ApplyLocalMxcSettings(false, WeakenedGlobalPolicies);
        Assert.IsFalse(script.IsMxcEffectivelyEnabled);
        script.UpdateMetadata(new PowerScriptListItem
        {
            HasLocalMxcSettings = true,
            LocalMxc = new()
            {
                Enabled = false,
                RiskAccepted = false,
            },
        });
        script.ApplyLocalMxcSettings(false, WeakenedGlobalPolicies);

        Assert.IsTrue(script.IsMxcReviewRequired);
        Assert.IsTrue(script.IsMxcEffectivelyEnabled);
        Assert.IsTrue(script.IsFilesystemRestrictionEffective);
        Assert.IsTrue(script.IsNetworkRestrictionEffective);
    }

    [TestMethod]
    public void ResetTextAfterFailedSaveDoesNotFreezeRecommendedTitle()
    {
        var snapshot = CreateSnapshot();
        snapshot.Recommended["title"] = "Author title";
        snapshot.Effective = (JsonObject)snapshot.Recommended.DeepClone();
        AssertResetIsNotRecommitted(snapshot, TitlePath, "Author title", "Edited title", JsonValue.Create("Edited title"));
    }

    [TestMethod]
    public void ResetListAfterFailedSaveDoesNotFreezeRecommendedConsumers()
    {
        var snapshot = CreateSnapshot();
        snapshot.Recommended["x-powerscript"]["consumers"] = JsonNode.Parse("""["command-palette"]""");
        snapshot.Effective = (JsonObject)snapshot.Recommended.DeepClone();
        AssertResetIsNotRecommitted(snapshot, ConsumersPath, "command-palette", "invalid id", JsonNode.Parse("""["invalid id"]"""));
    }

    [TestMethod]
    public void ResetChoiceAfterFailedSaveDoesNotFreezeRecommendedRuntime()
    {
        AssertResetIsNotRecommitted(CreateSnapshot(), RuntimePath, "powershell", "python", JsonValue.Create("python"));
    }

    [TestMethod]
    public void ResetNumberToEmptyAfterFailedSaveDoesNotWriteNullOverride()
    {
        AssertResetIsNotRecommitted(CreateSnapshot(), MaxFilesPath, double.NaN, 10d, JsonValue.Create(10));
    }

    [TestMethod]
    public void ResetMapAfterFailedSaveDoesNotFreezeAuthorArgumentMapping()
    {
        var snapshot = CreateSnapshot();
        snapshot.Recommended["x-execute"] = JsonNode.Parse("""{"command":["python","action.py"],"argMap":{"message":"--message"}}""");
        snapshot.Effective = (JsonObject)snapshot.Recommended.DeepClone();
        AssertResetIsNotRecommitted(
            snapshot,
            ArgMapPath,
            "message=--message",
            "message=--changed",
            JsonNode.Parse("""{"message":"--changed"}"""));
    }

    [TestMethod]
    public void ResetRequiredGroupDoesNotRecommitDeferredCheckboxChange()
    {
        AssertResetIsNotRecommitted(CreateSnapshot(), RequiredPath, true, false, new JsonArray());
    }

    [TestMethod]
    public void DisplayStateRecognizesEditingBackToOriginalAfterFailedSave()
    {
        var state = new PowerScriptSettingsEditor.FieldEditState<string>("original");
        Assert.IsTrue(state.HasChangedDisplay("edited"));
        state.AcceptDisplay("edited");

        // A failed Host save retains the edited draft. Typing the original value afterward
        // must replace that draft, rather than being ignored as equal to the initial display.
        Assert.IsFalse(state.HasChangedDisplay("edited"));
        Assert.IsTrue(state.HasChangedDisplay("original"));
        state.AcceptDisplay("original");
        Assert.IsFalse(state.HasChangedDisplay("original"));
    }

    private static void AssertResetIsNotRecommitted<T>(
        PowerScriptSettingsSnapshot snapshot,
        string[] path,
        T recommendedDisplay,
        T editedDisplay,
        JsonNode editedValue)
    {
        var editor = new PowerScriptSettingsEditor(snapshot);
        var state = new PowerScriptSettingsEditor.FieldEditState<T>(recommendedDisplay);
        Assert.IsTrue(state.HasChangedDisplay(editedDisplay));
        editor.Set(editedValue, path);
        state.AcceptDisplay(editedDisplay);

        // Simulate the retained draft after Host validation fails, including an unrelated edit.
        editor.Set(JsonValue.Create("Keep this edit"), "description");
        editor.Reset(path);
        state.AcceptDisplay(recommendedDisplay);
        var displayedValue = recommendedDisplay;

        // Delayed/repeated WinUI change notifications cannot alter this event-independent state.
        // Subsequent save passes compare the final control display with the accepted reset value.
        for (var pass = 0; pass < 3; pass++)
        {
            if (state.HasChangedDisplay(displayedValue))
            {
                editor.Set(editor.Read(path), path);
                state.AcceptDisplay(displayedValue);
            }
        }

        Assert.IsTrue(
            JsonNode.DeepEquals(JsonNode.Parse("""{"description":"Keep this edit"}"""), editor.Overrides),
            "Reset must remove the override without losing another field or freezing a recommendation.");
    }

    [DataTestMethod]
    [DataRow(true, true)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(false, false)]
    public void StaleLocalIsolationIsFullyRestrictedRegardlessOfGlobalWeakening(bool globalEnabled, bool localEnabled)
    {
        var script = new PowerScriptListItem
        {
            HasLocalMxcSettings = true,
            LocalMxc = new PowerScriptLocalMxcSettings
            {
                Enabled = localEnabled,
                DisabledPolicies = new() { PowerScriptListItem.NetworkPolicy },
                RiskAccepted = false,
            },
        };
        script.ApplyLocalMxcSettings(globalEnabled, WeakenedGlobalPolicies);

        Assert.IsTrue(script.IsMxcReviewRequired);
        Assert.IsTrue(script.IsMxcEffectivelyEnabled);
        Assert.IsTrue(script.IsFilesystemRestrictionEffective);
        Assert.IsTrue(script.IsNetworkRestrictionEffective);
        Assert.IsTrue(script.IsUiRestrictionEffective);
        Assert.IsTrue(script.IsLeastPrivilegeRestrictionEffective);
    }

    [TestMethod]
    public void ExplicitlyConfirmedLocalIsolationUsesTheConfirmedPolicy()
    {
        var script = new PowerScriptListItem
        {
            HasLocalMxcSettings = true,
            LocalMxc = new PowerScriptLocalMxcSettings
            {
                Enabled = true,
                DisabledPolicies = new() { PowerScriptListItem.NetworkPolicy },
                RiskAccepted = true,
            },
        };
        script.ApplyLocalMxcSettings(false, System.Array.Empty<string>());

        Assert.IsFalse(script.IsMxcReviewRequired);
        Assert.IsTrue(script.IsMxcEffectivelyEnabled);
        Assert.IsTrue(script.IsFilesystemRestrictionEffective);
        Assert.IsFalse(script.IsNetworkRestrictionEffective);
    }

    [TestMethod]
    public void EditingOneExtensionLeafDoesNotCopyRecommendedSiblings()
    {
        var snapshot = CreateSnapshot();
        var editor = new PowerScriptSettingsEditor(snapshot);
        editor.Set(JsonValue.Create("python"), "x-powerscript", "runtime");

        Assert.AreEqual(1, editor.Overrides.Count);
        Assert.AreEqual(1, editor.Overrides["x-powerscript"].AsObject().Count);
        Assert.AreEqual("python", editor.Overrides["x-powerscript"]["runtime"].GetValue<string>());
        Assert.IsNull(editor.Overrides["inputSchema"]);
        Assert.AreEqual("text", editor.Read("x-powerscript", "input").GetValue<string>());
        Assert.AreEqual(0, snapshot.Overrides.Count, "The dialog must not mutate its Host snapshot.");
    }

    [TestMethod]
    public void ResettingLeafRemovesOverrideAndPrunesOnlyEmptyParents()
    {
        var editor = new PowerScriptSettingsEditor(CreateSnapshot());
        editor.Set(JsonValue.Create("python"), "x-powerscript", "runtime");
        editor.Set(JsonValue.Create("files"), "x-powerscript", "input");
        editor.Reset("x-powerscript", "runtime");

        Assert.IsFalse(editor.Overrides["x-powerscript"].AsObject().ContainsKey("runtime"));
        Assert.AreEqual("files", editor.Overrides["x-powerscript"]["input"].GetValue<string>());
        Assert.AreEqual("powershell", editor.Read("x-powerscript", "runtime").GetValue<string>());
        editor.Reset("x-powerscript", "input");
        Assert.AreEqual(0, editor.Overrides.Count, "Reset must not pin recommended values.");
    }

    [TestMethod]
    public void NullClearsAnAuthorFieldButResetRestoresInheritance()
    {
        var editor = new PowerScriptSettingsEditor(CreateSnapshot());
        editor.Set(null, "description");
        Assert.IsTrue(editor.Overrides.ContainsKey("description"));
        Assert.IsNull(editor.Overrides["description"]);
        Assert.IsNull(editor.Read("description"));

        editor.Reset("description");
        Assert.IsFalse(editor.Overrides.ContainsKey("description"));
        Assert.AreEqual("Author description", editor.Read("description").GetValue<string>());
    }

    [TestMethod]
    public void ResettingAnAbsentRecommendationRemovesTheEffectiveValue()
    {
        var editor = new PowerScriptSettingsEditor(CreateSnapshot());
        editor.Set(JsonValue.Create("future-module"), "x-powerscript", "publisher");
        editor.Reset("x-powerscript", "publisher");
        Assert.IsNull(editor.Read("x-powerscript", "publisher"));
        Assert.AreEqual(0, editor.Overrides.Count);
    }

    [TestMethod]
    public void EditingAndResettingPreserveUnrepresentedLocalMetadata()
    {
        var snapshot = CreateSnapshot();
        snapshot.Overrides = JsonNode.Parse("""{"x-powerscript":{"publisher":"Local publisher"},"annotations":{"readOnlyHint":false}}""").AsObject();
        var editor = new PowerScriptSettingsEditor(snapshot);
        editor.Set(JsonValue.Create("New name"), "title");
        editor.Reset("title");
        Assert.AreEqual("Local publisher", editor.Overrides["x-powerscript"]["publisher"].GetValue<string>());
        Assert.IsFalse(editor.Overrides["annotations"]["readOnlyHint"].GetValue<bool>());
        Assert.IsTrue(JsonNode.DeepEquals(snapshot.Overrides, editor.Overrides));
    }

    [TestMethod]
    public void ParameterRemovalUsesATombstoneAndResetRestoresRequiredWithoutPinningSchema()
    {
        var editor = new PowerScriptSettingsEditor(CreateSnapshot());
        editor.Set(null, "inputSchema", "properties", "message");
        editor.SetParameterRequired("message", false);
        Assert.IsTrue(editor.Overrides["inputSchema"]["properties"].AsObject().ContainsKey("message"));
        Assert.IsNull(editor.Overrides["inputSchema"]["properties"]["message"]);
        Assert.AreEqual(0, editor.Read("inputSchema", "required").AsArray().Count);

        editor.ResetParameter("message");
        Assert.AreEqual("string", editor.Read("inputSchema", "properties", "message", "type").GetValue<string>());
        Assert.AreEqual("message", editor.Read("inputSchema", "required")[0].GetValue<string>());
        Assert.AreEqual(0, editor.Overrides.Count);
    }

    [TestMethod]
    public void ResettingOneParameterRetainsOtherParameterEdits()
    {
        var editor = new PowerScriptSettingsEditor(CreateSnapshot());
        editor.Set(JsonValue.Create(10), "inputSchema", "properties", "count", "default");
        editor.SetParameterRequired("count", true);
        editor.SetParameterRequired("message", false);
        editor.ResetParameter("message");

        Assert.AreEqual(10, editor.Overrides["inputSchema"]["properties"]["count"]["default"].GetValue<int>());
        var required = editor.Read("inputSchema", "required").AsArray();
        Assert.AreEqual(2, required.Count);
        Assert.IsTrue(required.ToJsonString().Contains("message", System.StringComparison.Ordinal));
        Assert.IsTrue(required.ToJsonString().Contains("count", System.StringComparison.Ordinal));
        Assert.IsFalse(editor.Overrides["inputSchema"]["properties"].AsObject().ContainsKey("message"));
    }

    [TestMethod]
    public void ResettingRequiredStateDoesNotResetTheParameterDefault()
    {
        var editor = new PowerScriptSettingsEditor(CreateSnapshot());
        editor.Set(JsonValue.Create("Personal default"), "inputSchema", "properties", "message", "default");
        editor.SetParameterRequired("message", false);
        editor.ResetParameterRequired("message");

        Assert.AreEqual("Personal default", editor.Read("inputSchema", "properties", "message", "default").GetValue<string>());
        Assert.AreEqual("message", editor.Read("inputSchema", "required")[0].GetValue<string>());
        Assert.IsFalse(editor.Overrides["inputSchema"].AsObject().ContainsKey("required"));
    }

    [TestMethod]
    public void ResettingRequiredStatePreservesAnotherParametersRequirement()
    {
        var editor = new PowerScriptSettingsEditor(CreateSnapshot());
        editor.SetParameterRequired("count", true);
        editor.SetParameterRequired("message", false);
        editor.ResetParameterRequired("message");

        var required = editor.Read("inputSchema", "required").AsArray();
        Assert.AreEqual(2, required.Count);
        Assert.IsTrue(required.ToJsonString().Contains("message", System.StringComparison.Ordinal));
        Assert.IsTrue(required.ToJsonString().Contains("count", System.StringComparison.Ordinal));
    }

    [TestMethod]
    public void ChoosingBuiltInRuntimeClearsWholeRecipeAndResetRestoresRecommendedRecipe()
    {
        var snapshot = CreateSnapshot();
        snapshot.Recommended["x-execute"] = JsonNode.Parse("""{"command":["python","action.py"],"argMap":{"message":"--message"},"stdin":"json"}""");
        snapshot.Effective = (JsonObject)snapshot.Recommended.DeepClone();
        var editor = new PowerScriptSettingsEditor(snapshot);
        editor.Set(JsonValue.Create("Updated title"), "title");
        editor.Set(null, "x-execute");

        Assert.IsTrue(editor.Overrides.ContainsKey("x-execute"));
        Assert.IsNull(editor.Overrides["x-execute"]);
        Assert.IsNull(editor.Read("x-execute"));
        Assert.AreEqual("Updated title", editor.Read("title").GetValue<string>());

        editor.Reset("x-execute");
        Assert.IsFalse(editor.Overrides.ContainsKey("x-execute"));
        Assert.IsTrue(JsonNode.DeepEquals(snapshot.Recommended["x-execute"], editor.Read("x-execute")));
        Assert.AreEqual("Updated title", editor.Overrides["title"].GetValue<string>());
    }

    [TestMethod]
    public void ResettingCommandAfterWholeRecipeDeletionRestoresOnlyCommandInheritance()
    {
        var snapshot = CreateSnapshot();
        snapshot.Recommended["x-execute"] = JsonNode.Parse("""{"command":["python","action.py"],"argMap":{"message":"--message"},"stdin":"json"}""");
        snapshot.Effective = (JsonObject)snapshot.Recommended.DeepClone();
        var editor = new PowerScriptSettingsEditor(snapshot);
        editor.Set(null, "x-execute");
        editor.Reset("x-execute", "command");

        var executionOverride = editor.Overrides["x-execute"].AsObject();
        Assert.IsFalse(executionOverride.ContainsKey("command"), "Reset inherits the command instead of pinning it.");
        Assert.IsTrue(executionOverride.ContainsKey("argMap"));
        Assert.IsNull(executionOverride["argMap"]);
        Assert.IsTrue(executionOverride.ContainsKey("stdin"));
        Assert.IsNull(executionOverride["stdin"]);
        Assert.IsTrue(JsonNode.DeepEquals(snapshot.Recommended["x-execute"]["command"], editor.Read("x-execute", "command")));
        Assert.IsNull(editor.Read("x-execute", "argMap"));
    }

    [TestMethod]
    public void SnapshotAllowsOmittedNullMxcAndPreservesConsumerTombstone()
    {
        var snapshot = JsonSerializer.Deserialize<PowerScriptSettingsSnapshot>(
            """{"entryPath":"C:\\scripts\\action.py","hasLocalMxcSettings":true,"overrides":{"x-powerscript":{"consumers":null}}}""",
            SnapshotJsonOptions);
        Assert.IsNull(snapshot.Mxc);
        Assert.IsTrue(snapshot.HasLocalMxcSettings);
        var editor = new PowerScriptSettingsEditor(snapshot);
        Assert.IsTrue(editor.Overrides["x-powerscript"].AsObject().ContainsKey("consumers"));
        Assert.IsNull(editor.Overrides["x-powerscript"]["consumers"]);
    }

    private static PowerScriptSettingsSnapshot CreateSnapshot()
    {
        var recommended = JsonNode.Parse("""
            {
              "name": "action",
              "description": "Author description",
              "x-powerscript": { "runtime": "powershell", "input": "text", "output": "text" },
              "inputSchema": {
                "type": "object",
                "properties": {
                  "message": { "type": "string", "description": "Message" },
                  "count": { "type": "integer", "default": 2 }
                },
                "required": [ "message" ]
              }
            }
            """).AsObject();
        return new PowerScriptSettingsSnapshot
        {
            Recommended = recommended,
            Effective = (JsonObject)recommended.DeepClone(),
        };
    }
}
