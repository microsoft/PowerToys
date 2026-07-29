// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Verifies the stable list-item identity used to reuse item adapters across a
/// refresh (p4-03). Keying by the SDK-emitted stable id (or nested command id) rather
/// than the title keeps each row bound to its own command when a refresh reorders
/// items that share a title, so a duplicate-title reorder does not swap actions.
/// </summary>
[TestClass]
public class JSListItemAdapterKeyTests
{
    private static string Key(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JSListItemAdapter.ComputeKey(doc.RootElement);
    }

    [TestMethod]
    public void ComputeKey_PrefersStableId()
    {
        Assert.AreEqual("id:item-42", Key("""{ "id": "item-42", "title": "Anything" }"""));
    }

    [TestMethod]
    public void ComputeKey_FallsBackToCommandId()
    {
        Assert.AreEqual("cmd:cmd-7", Key("""{ "title": "Anything", "command": { "id": "cmd-7" } }"""));
    }

    [TestMethod]
    public void ComputeKey_FallsBackToTitleWhenNoId()
    {
        Assert.AreEqual("title:Only Title", Key("""{ "title": "Only Title" }"""));
    }

    [TestMethod]
    public void ComputeKey_IdIsNamespacedApartFromTitle()
    {
        // An id "X" must not produce the same key as a title "X".
        var idKey = Key("""{ "id": "X" }""");
        var titleKey = Key("""{ "title": "X" }""");
        Assert.AreNotEqual(idKey, titleKey);
    }

    [TestMethod]
    public void ComputeKey_DuplicateTitlesWithDistinctIds_ProduceDistinctKeys()
    {
        // The duplicate-title reorder scenario: two rows share a title but have their
        // own ids, so they get distinct keys and stay bound to their own commands.
        var first = Key("""{ "id": "a", "title": "Same Title" }""");
        var second = Key("""{ "id": "b", "title": "Same Title" }""");
        Assert.AreNotEqual(first, second);
    }
}
