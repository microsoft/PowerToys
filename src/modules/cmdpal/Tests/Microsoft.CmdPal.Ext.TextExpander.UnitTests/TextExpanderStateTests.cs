// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.PowerToys.TextExpander;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

[TestClass]
public sealed class TextExpanderStateTests
{
    [TestMethod]
    [DataRow((int)PolicyRule.NotConfigured, null, (int)TextExpanderAvailability.Off)]
    [DataRow((int)PolicyRule.NotConfigured, false, (int)TextExpanderAvailability.Off)]
    [DataRow((int)PolicyRule.NotConfigured, true, (int)TextExpanderAvailability.On)]
    [DataRow((int)PolicyRule.Disabled, true, (int)TextExpanderAvailability.OffByPolicy)]
    [DataRow((int)PolicyRule.Enabled, false, (int)TextExpanderAvailability.On)]
    [DataRow((int)PolicyRule.Enabled, null, (int)TextExpanderAvailability.On)]
    public void Evaluate_PolicyAndSetting_PolicyWinsAndSettingIsOffByDefault(int policy, bool? enabled, int expected)
        => Assert.AreEqual((TextExpanderAvailability)expected, TextExpanderModuleState.Evaluate((PolicyRule)policy, enabled));

    [TestMethod]
    [DataRow("""{ "enabled": { "TextExpander": true } }""", true)]
    [DataRow("""{ "enabled": { "TextExpander": false } }""", false)]
    [DataRow("""{ "enabled": { "Awake": true } }""", false)]
    [DataRow("""{ "startup": true }""", false)]
    [DataRow("{ \"enabled\": { \"TextExpander\": true } }\0\0\0", true)]
    public void ReadEnabledFlag_GeneralSettings_ReadsTextExpandersSwitch(string json, bool expected)
        => Assert.AreEqual(expected, TextExpanderModuleState.ReadEnabledFlag(json));

    [TestMethod]
    [DataRow("")]
    [DataRow("{ \"enabled\": ")]
    [DataRow("[]")]
    public void ReadEnabledFlag_FileCaughtMidSave_ReturnsNullRatherThanOff(string json)
        => Assert.IsNull(TextExpanderModuleState.ReadEnabledFlag(json));

    [TestMethod]
    public void EvaluatePolicy_OwnPolicySet_ComesBeforeThePolicyForAllUtilities()
    {
        var store = new FakePolicyStore();
        Assert.AreEqual(PolicyRule.NotConfigured, TextExpanderPolicy.Evaluate(store));

        store.Values[TextExpanderPolicy.AllUtilitiesValueName] = 0;
        Assert.AreEqual(PolicyRule.Disabled, TextExpanderPolicy.Evaluate(store));

        store.Values[TextExpanderPolicy.TextExpanderValueName] = 1;
        Assert.AreEqual(PolicyRule.Enabled, TextExpanderPolicy.Evaluate(store));

        // A value that's neither 0 nor 1 is a mistake, which gpo.h treats as not set.
        store.Values[TextExpanderPolicy.TextExpanderValueName] = 2;
        Assert.AreEqual(PolicyRule.Disabled, TextExpanderPolicy.Evaluate(store));
    }

    [TestMethod]
    public void ParseSettings_WrappedOrBareValues_ReadsBoth()
    {
        var settings = TextExpanderSettings.Parse("""
            {
              "name": "TextExpander",
              "properties": {
                "snippets_path": { "value": "  C:\\Snippets\\mine.json  " },
                "case_sensitive": false,
                "injection_backend": { "value": "type" }
              }
            }
            """);

        Assert.AreEqual(new TextExpanderSettings(@"C:\Snippets\mine.json", CaseSensitive: false, InjectionBackend.Typing, InjectionPolicy.DefaultClipboardThresholdChars), settings);
        Assert.AreEqual(
            new TextExpanderSettings(null, CaseSensitive: false, InjectionBackend.Auto, InjectionPolicy.DefaultClipboardThresholdChars),
            TextExpanderSettings.Parse("""{ "properties": { "case_sensitive": { "value": false }, "snippets_path": "" } }"""));
    }

    [TestMethod]
    public void ParseSettings_MissingValues_KeepTextExpandersDefaults()
        => Assert.AreEqual(TextExpanderSettings.Default, TextExpanderSettings.Parse("""{ "properties": {} }"""));

    [TestMethod]
    [DataRow("type", "Typing")]
    [DataRow("Typing", "Typing")]
    [DataRow("keyboard", "Typing")]
    [DataRow("inject", "Typing")]
    [DataRow("auto", "Auto")]
    [DataRow("clipboard", "Clipboard")]
    [DataRow("paste", "Clipboard")]
    [DataRow("something else", "Auto")]
    public void ParseSettings_InsertionMethod_FollowsTheNamesTheEngineAccepts(string backend, string expected)
        => Assert.AreEqual(Enum.Parse<InjectionBackend>(expected), TextExpanderSettings.Parse($$"""{ "properties": { "injection_backend": { "value": "{{backend}}" } } }""")!.Backend);

    [TestMethod]
    [DataRow("""{ "properties": { "clipboard_threshold_chars": { "value": 40 } } }""", 40)]
    [DataRow("""{ "properties": { "clipboard_threshold_chars": 12 } }""", 12)]
    [DataRow("""{ "properties": { "clipboard_threshold_chars": { "value": "30" } } }""", 30)]
    [DataRow("""{ "properties": { "clipboard_threshold_chars": { "value": 0 } } }""", 5)]
    [DataRow("""{ "properties": { "clipboard_threshold_chars": true } }""", 5)]
    public void ParseSettings_ShortestSnippetToPaste_IsReadAsTheEngineReadsIt(string json, int expected)
        => Assert.AreEqual(expected, TextExpanderSettings.Parse(json)!.ClipboardThresholdChars);

    [TestMethod]
    [DataRow("")]
    [DataRow("not json")]
    [DataRow("""{ "name": "TextExpander" }""")]
    [DataRow("""{ "properties": [] }""")]
    public void ParseSettings_NotSettings_ReturnsNull(string json)
        => Assert.IsNull(TextExpanderSettings.Parse(json));
}
