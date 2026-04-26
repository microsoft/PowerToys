// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerToys.MacroEngine.Tests;

[TestClass]
public sealed class SendInputHelperTests
{
    private static readonly string[] ExpectedCombos = ["Ctrl+C", "Enter"];
    private static readonly string[] ExpectedText = ["Hello"];

    [TestMethod]
    public void FakeHelper_RecordsKeyCombos()
    {
        var fake = new FakeSendInputHelper();
        fake.PressKeyCombo("Ctrl+C");
        fake.PressKeyCombo("Enter");
        CollectionAssert.AreEqual(ExpectedCombos, fake.KeyCombos);
    }

    [TestMethod]
    public void FakeHelper_RecordsText()
    {
        var fake = new FakeSendInputHelper();
        fake.TypeText("Hello");
        CollectionAssert.AreEqual(ExpectedText, fake.Texts);
    }
}
