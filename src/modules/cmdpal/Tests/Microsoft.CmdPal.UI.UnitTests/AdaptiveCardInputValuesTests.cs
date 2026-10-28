// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.UI.Controls.AdaptiveCards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Data.Json;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class AdaptiveCardInputValuesTests
{
    [TestMethod]
    public void ApplySetsNestedAndShowCardInputValues()
    {
        var card = JsonObject.Parse("""
            {
                "type": "AdaptiveCard",
                "body": [
                    { "type": "Container", "items": [ { "type": "Input.Text", "id": "name", "value": "old" } ] },
                    { "type": "Input.Toggle", "id": "enabled", "value": "false" },
                    { "type": "Input.CommandPalette.StringList", "id": "items" }
                ],
                "actions": [
                    { "type": "Action.ShowCard", "card": { "type": "AdaptiveCard", "body": [ { "type": "Input.Date", "id": "date" } ] } }
                ]
            }
            """);

        AdaptiveCardInputValues.Apply(card, new Dictionary<string, string>
        {
            ["name"] = "typed",
            ["enabled"] = "true",
            ["items"] = "[\"a\"]",
            ["date"] = "2026-10-27",
        });

        var body = card.GetNamedArray("body");
        Assert.AreEqual("typed", body.GetObjectAt(0).GetNamedArray("items").GetObjectAt(0).GetNamedString("value"));
        Assert.AreEqual("true", body.GetObjectAt(1).GetNamedString("value"));
        Assert.AreEqual("[\"a\"]", body.GetObjectAt(2).GetNamedString("value"));
        var showCardBody = card.GetNamedArray("actions").GetObjectAt(0).GetNamedObject("card").GetNamedArray("body");
        Assert.AreEqual("2026-10-27", showCardBody.GetObjectAt(0).GetNamedString("value"));
    }

    [TestMethod]
    public void ApplyWritesNumbersAndClearsEmptyNumbers()
    {
        var card = JsonObject.Parse("""
            {
                "type": "AdaptiveCard",
                "body": [
                    { "type": "Input.Number", "id": "count", "value": 1 },
                    { "type": "Input.Number", "id": "empty", "value": 2 }
                ]
            }
            """);

        AdaptiveCardInputValues.Apply(card, new Dictionary<string, string> { ["count"] = "4.5", ["empty"] = string.Empty });

        var body = card.GetNamedArray("body");
        Assert.AreEqual(4.5, body.GetObjectAt(0).GetNamedNumber("value"));
        Assert.IsFalse(body.GetObjectAt(1).ContainsKey("value"));
    }

    [TestMethod]
    public void ApplyLeavesNonInputsAndUnknownInputsUnchanged()
    {
        var card = JsonObject.Parse("""
            {
                "type": "AdaptiveCard",
                "body": [
                    { "type": "TextBlock", "id": "name", "text": "Label" },
                    { "type": "Input.Text", "id": "other", "value": "kept" }
                ]
            }
            """);

        AdaptiveCardInputValues.Apply(card, new Dictionary<string, string> { ["name"] = "typed" });

        var body = card.GetNamedArray("body");
        Assert.IsFalse(body.GetObjectAt(0).ContainsKey("value"));
        Assert.AreEqual("kept", body.GetObjectAt(1).GetNamedString("value"));
    }
}
