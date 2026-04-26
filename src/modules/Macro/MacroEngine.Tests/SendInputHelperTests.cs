// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroEngine;

namespace PowerToys.MacroEngine.Tests;

internal sealed class FakeSendInputHelper : ISendInputHelper
{
    public List<string> KeyCombos { get; } = [];
    public List<string> Texts { get; } = [];

    public void PressKeyCombo(string combo) => KeyCombos.Add(combo);
    public void TypeText(string text) => Texts.Add(text);
}

[TestClass]
public sealed class SendInputHelperTests
{
    [TestMethod]
    public void FakeHelper_RecordsKeyCombos()
    {
        var fake = new FakeSendInputHelper();
        fake.PressKeyCombo("Ctrl+C");
        fake.PressKeyCombo("Enter");
        CollectionAssert.AreEqual(new[] { "Ctrl+C", "Enter" }, fake.KeyCombos);
    }

    [TestMethod]
    public void FakeHelper_RecordsText()
    {
        var fake = new FakeSendInputHelper();
        fake.TypeText("Hello");
        CollectionAssert.AreEqual(new[] { "Hello" }, fake.Texts);
    }
}
