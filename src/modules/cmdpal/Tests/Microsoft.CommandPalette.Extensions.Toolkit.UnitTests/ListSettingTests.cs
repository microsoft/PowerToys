// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class ListSettingTests
{
    private static readonly string[] _submittedItems = ["gamma", "delta"];
    private static readonly string[] _loadedItems = ["alpha", "beta"];
    private static readonly string[] _pathItems = ["C:\\Tools", "C:\\Program Files"];
    private static readonly string[] _fileTypeFilters = [".exe", "*.cmd"];
    private static readonly string[] _listInputTypes = ["Input.CommandPalette.StringList", "Input.CommandPalette.FilePathList", "Input.CommandPalette.KeyValueList"];
    private static readonly KeyValuePair<string, string>[] _keyValueItems =
    [
        new("key=part", "C:\\Tools=value\nnext"),
        new("plain", string.Empty),
    ];

    private static readonly KeyValuePair<string, string>[] _loadedKeyValueItems =
    [
        new("alpha", "one"),
        new("beta", "two=three"),
    ];

    [TestMethod]
    public void StringListSetting_PersistsItemsAsJsonArray()
    {
        var setting = new StringListSetting("items", ["alpha", "beta"]);

        // The settings file and the card carry the same shape.
        Assert.AreEqual("""[{"value":"alpha"},{"value":"beta"}]""", setting.ToDictionary()["value"]);
        var state = JsonNode.Parse($"{{{setting.ToState()}}}")!.AsObject();
        Assert.AreEqual(
            """[{"value":"alpha"},{"value":"beta"}]""",
            state["items"]!.ToJsonString());
    }

    [TestMethod]
    public void StringListSetting_SettingsUpdate_ReadsPersistedArray()
    {
        var setting = new StringListSetting("items", []);
        var settings = new Settings();
        settings.Add(setting);

        settings.Update("""{"items":["alpha","beta"]}""");

        CollectionAssert.AreEqual(_loadedItems, setting.Value!.ToArray());
    }

    [TestMethod]
    public void StringListSetting_UpdateFromForm_ReadsAdaptiveCardValue()
    {
        var setting = new StringListSetting("items", []);
        var settings = new Settings();
        settings.Add(setting);

        settings.UpdateFromForm(new JsonObject { ["items"] = CardValue("items", _submittedItems, static (k, v) => new StringListSetting(k, v)) }.ToJsonString());

        CollectionAssert.AreEqual(_submittedItems, setting.Value!.ToArray());
    }

    [TestMethod]
    public void StringListSetting_UnknownPerItemPropertiesAreIgnoredOnRead()
    {
        var setting = new StringListSetting("items", []);
        var settings = new Settings();
        settings.Add(setting);

        settings.UpdateFromForm(new JsonObject
        {
            ["items"] = """[{"value":"alpha","enabled":false},{"value":"beta"}]""",
        }.ToJsonString());

        CollectionAssert.AreEqual(_loadedItems, setting.Value!.ToArray());
    }

    [TestMethod]
    public void StringListSetting_MalformedCardValueKeepsTheCurrentValue()
    {
        var setting = new StringListSetting("items", ["alpha", "beta"]);
        var settings = new Settings();
        settings.Add(setting);

        settings.UpdateFromForm(new JsonObject { ["items"] = "not an array" }.ToJsonString());

        CollectionAssert.AreEqual(_loadedItems, setting.Value!.ToArray());
    }

    [TestMethod]
    public void KeyValueListSetting_MalformedCardValueKeepsTheCurrentValue()
    {
        var setting = new KeyValueListSetting("pairs", _loadedKeyValueItems);
        var settings = new Settings();
        settings.Add(setting);

        settings.UpdateFromForm(new JsonObject { ["pairs"] = "{not an array}" }.ToJsonString());

        CollectionAssert.AreEqual(_loadedKeyValueItems, setting.Value!.ToArray());
    }

    [TestMethod]
    public void StringListSetting_RoundTripsThroughPersistedState()
    {
        var setting = new StringListSetting("items", ["alpha", "beta"]);
        var reloaded = new StringListSetting("items", []);
        var settings = new Settings();
        settings.Add(reloaded);

        settings.Update($"{{{setting.ToState()}}}");

        CollectionAssert.AreEqual(setting.Value!.ToArray(), reloaded.Value!.ToArray());
    }

    [TestMethod]
    public void StringListSetting_EmitsValidationPattern()
    {
        var setting = new StringListSetting("items", ["alpha"])
        {
            ItemValidationPattern = "^[a-z]+$",
            ItemValidationErrorMessage = "Use lowercase letters.",
            PreventDuplicates = true,
            DuplicateItemErrorMessage = "Use unique items.",
        };

        var dictionary = setting.ToDictionary();

        Assert.AreEqual("^[a-z]+$", dictionary["itemValidationPattern"]);
        Assert.AreEqual("Use lowercase letters.", dictionary["itemValidationErrorMessage"]);
        Assert.AreEqual(true, dictionary["preventDuplicates"]);
        Assert.AreEqual("Use unique items.", dictionary["duplicateItemErrorMessage"]);
    }

    [TestMethod]
    public void StringListSetting_PreventDuplicates_DefaultsToFalse()
    {
        var setting = new StringListSetting("items", ["alpha", "alpha"]);

        Assert.AreEqual(false, setting.PreventDuplicates);
        Assert.AreEqual(false, setting.ToDictionary()["preventDuplicates"]);
    }

    [TestMethod]
    public void StringListSetting_InvalidValidationPattern_Throws()
    {
        var setting = new StringListSetting("items", []);

        Assert.ThrowsException<ArgumentException>(() => setting.ItemValidationPattern = "[");
    }

    [TestMethod]
    public void FilePathListSetting_EmitsPickerOptions()
    {
        var setting = new FilePathListSetting(
            "searchPaths",
            "Search paths",
            "Additional folders to search",
            ["C:\\Tools"],
            FilePathListItemType.Folders)
        {
            FileTypeFilter = [".exe", "*.cmd"],
        };

        var dictionary = setting.ToDictionary();

        Assert.AreEqual("Input.CommandPalette.FilePathList", dictionary["type"]);
        Assert.AreEqual(false, dictionary["allowFiles"]);
        Assert.AreEqual(true, dictionary["allowFolders"]);
        CollectionAssert.AreEqual(
            _fileTypeFilters,
            ((List<string>)dictionary["fileTypeFilter"]).ToArray());
    }

    [TestMethod]
    public void FilePathListSetting_NoAllowedItemTypes_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            new FilePathListSetting("paths", [], (FilePathListItemType)0));
    }

    [TestMethod]
    public void FilePathListSetting_ToForm_UsesRegisteredCustomElementType()
    {
        var setting = new FilePathListSetting("paths", [])
        {
            FileTypeFilter = [".exe"],
        };

        var form = JsonNode.Parse(setting.ToForm())!.AsObject();
        var input = form["body"]!.AsArray()[0]!.AsObject();

        Assert.AreEqual("Input.CommandPalette.FilePathList", input["type"]!.GetValue<string>());
        Assert.AreEqual(".exe", input["fileTypeFilter"]!.AsArray()[0]!.GetValue<string>());
    }

    [TestMethod]
    public void ListSettings_EmitFallbackNoticeNamingTheSetting()
    {
        var setting = new StringListSetting("items", "Search paths", "Where to look", ["alpha"]);

        var form = JsonNode.Parse(setting.ToForm())!.AsObject();
        var fallback = form["body"]!.AsArray()[0]!["fallback"]!.AsObject();

        Assert.AreEqual("TextBlock", fallback["type"]!.GetValue<string>());
        Assert.AreEqual(true, fallback["wrap"]!.GetValue<bool>());
        StringAssert.Contains(fallback["text"]!.GetValue<string>(), "Search paths");
    }

    [TestMethod]
    public void FilePathListSetting_RoundTripsThroughPersistedState()
    {
        var setting = new FilePathListSetting("paths", ["C:\\Tools", "C:\\Program Files"]);
        var reloaded = new FilePathListSetting("paths", []);
        var settings = new Settings();
        settings.Add(reloaded);

        settings.Update($"{{{setting.ToState()}}}");

        CollectionAssert.AreEqual(setting.Value!.ToArray(), reloaded.Value!.ToArray());
    }

    [TestMethod]
    public void CustomInputSettings_EnteredValuesRoundTripThroughPersistedState()
    {
        var stringList = new StringListSetting("strings", []);
        var pathList = new FilePathListSetting("paths", []);
        var keyValueList = new KeyValueListSetting("pairs", []);
        var singlePath = new FilePathSetting("singlePath", string.Empty);
        var settings = new Settings();
        settings.Add(stringList);
        settings.Add(pathList);
        settings.Add(keyValueList);
        settings.Add(singlePath);

        settings.UpdateFromForm(new JsonObject
        {
            ["strings"] = CardValue("items", _submittedItems, static (k, v) => new StringListSetting(k, v)),
            ["paths"] = CardValue("paths", _pathItems, static (k, v) => new FilePathListSetting(k, v)),
            ["pairs"] = CardValue("pairs", _keyValueItems, static (k, v) => new KeyValueListSetting(k, v)),
            ["singlePath"] = @"C:\Tools\app.exe",
        }.ToJsonString());

        var reloadedStringList = new StringListSetting("strings", []);
        var reloadedPathList = new FilePathListSetting("paths", []);
        var reloadedKeyValueList = new KeyValueListSetting("pairs", []);
        var reloadedSinglePath = new FilePathSetting("singlePath", string.Empty);
        var reloadedSettings = new Settings();
        reloadedSettings.Add(reloadedStringList);
        reloadedSettings.Add(reloadedPathList);
        reloadedSettings.Add(reloadedKeyValueList);
        reloadedSettings.Add(reloadedSinglePath);

        reloadedSettings.Update(settings.ToJson());

        CollectionAssert.AreEqual(_submittedItems, reloadedStringList.Value!.ToArray());
        CollectionAssert.AreEqual(_pathItems, reloadedPathList.Value!.ToArray());
        CollectionAssert.AreEqual(_keyValueItems, reloadedKeyValueList.Value!.ToArray());
        Assert.AreEqual(@"C:\Tools\app.exe", reloadedSinglePath.Value);
    }

    [TestMethod]
    public void KeyValueListSetting_PersistsPairsAsJsonArray()
    {
        var setting = new KeyValueListSetting("pairs", [new("name", "value=with equals")]);

        var state = JsonNode.Parse($"{{{setting.ToState()}}}")!.AsObject();
        var entry = state["pairs"]!.AsArray()[0]!.AsObject();

        Assert.AreEqual("name", entry["key"]!.GetValue<string>());
        Assert.AreEqual("value=with equals", entry["value"]!.GetValue<string>());
        Assert.AreEqual(
            """[{"key":"name","value":"value=with equals"}]""",
            setting.ToDictionary()["value"]);
    }

    [TestMethod]
    public void KeyValueListSetting_SettingsUpdate_ReadsPersistedArray()
    {
        var setting = new KeyValueListSetting("pairs", []);
        var settings = new Settings();
        settings.Add(setting);

        settings.Update("""{"pairs":[{"key":"alpha","value":"one"},{"key":"beta","value":"two=three"}]}""");

        CollectionAssert.AreEqual(_loadedKeyValueItems, setting.Value!.ToArray());
    }

    [TestMethod]
    public void KeyValueListSetting_UpdateFromForm_ReadsAdaptiveCardValue()
    {
        var setting = new KeyValueListSetting("pairs", []);
        var settings = new Settings();
        settings.Add(setting);

        settings.UpdateFromForm(
            new JsonObject { ["pairs"] = CardValue("pairs", _loadedKeyValueItems, static (k, v) => new KeyValueListSetting(k, v)) }.ToJsonString());

        CollectionAssert.AreEqual(_loadedKeyValueItems, setting.Value!.ToArray());
    }

    [TestMethod]
    public void KeyValueListSetting_RoundTripsDuplicateKeysAndEscapedCharacters()
    {
        var setting = new KeyValueListSetting("pairs", _keyValueItems);
        var reloaded = new KeyValueListSetting("pairs", []);
        var settings = new Settings();
        settings.Add(reloaded);

        settings.Update($"{{{setting.ToState()}}}");

        CollectionAssert.AreEqual(_keyValueItems, reloaded.Value!.ToArray());
    }

    [TestMethod]
    public void KeyValueListSetting_EmitsValidationPatterns()
    {
        var setting = new KeyValueListSetting("pairs", [new("name", "value")])
        {
            KeyValidationPattern = "^[a-z]+$",
            KeyValidationErrorMessage = "Invalid key.",
            ValueValidationPattern = "^.+$",
            ValueValidationErrorMessage = "Invalid value.",
            PreventDuplicateKeys = true,
            DuplicateKeyErrorMessage = "Use unique keys.",
        };

        var dictionary = setting.ToDictionary();

        Assert.AreEqual("Input.CommandPalette.KeyValueList", dictionary["type"]);
        Assert.AreEqual("^[a-z]+$", dictionary["keyValidationPattern"]);
        Assert.AreEqual("^.+$", dictionary["valueValidationPattern"]);
        Assert.AreEqual(true, dictionary["preventDuplicateKeys"]);
        Assert.AreEqual("Use unique keys.", dictionary["duplicateKeyErrorMessage"]);
    }

    [TestMethod]
    public void KeyValueListSetting_PreventDuplicateKeys_DefaultsToFalse()
    {
        var setting = new KeyValueListSetting("pairs", [new("key", "one"), new("key", "two")]);

        Assert.AreEqual(false, setting.PreventDuplicateKeys);
        Assert.AreEqual(false, setting.ToDictionary()["preventDuplicateKeys"]);
    }

    [TestMethod]
    public void KeyValueListSetting_InvalidValidationPattern_Throws()
    {
        var setting = new KeyValueListSetting("pairs", []);

        Assert.ThrowsException<ArgumentException>(() => setting.KeyValidationPattern = "[");
        Assert.ThrowsException<ArgumentException>(() => setting.ValueValidationPattern = "(");
    }

    // Builds a submission the way the setting itself renders one, so the tests never carry a
    // second encoder that could drift from the SDK. The exact wire shape is pinned by the
    // *_EmitsCardValue tests below; the host's copy of it lives in AdaptiveListValueCodec.
    private static string CardValue<T>(string key, IReadOnlyList<T> items, Func<string, IReadOnlyList<T>, Setting<IReadOnlyList<T>>> create) =>
        (string)create(key, items).ToDictionary()["value"];

    [TestMethod]
    public void SettingsForm_ContainsEveryListInputType()
    {
        var settings = new Settings();
        settings.Add(new StringListSetting("strings", ["alpha"]));
        settings.Add(new FilePathListSetting("paths", [@"C:\Tools"]));
        settings.Add(new KeyValueListSetting("pairs", [new("key", "value")]));

        var form = JsonNode.Parse(settings.ToFormJson())!.AsObject();
        var inputTypes = form["body"]!
            .AsArray()
            .Select(input => input!["type"]!.GetValue<string>())
            .ToArray();

        CollectionAssert.AreEqual(_listInputTypes, inputTypes);
    }
}
