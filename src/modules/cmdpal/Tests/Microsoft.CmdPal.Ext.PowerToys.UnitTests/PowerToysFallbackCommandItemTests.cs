// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToysExtension.Helpers;

namespace Microsoft.CmdPal.Ext.PowerToys.UnitTests;

[TestClass]
public class PowerToysFallbackCommandItemTests
{
    [TestMethod]
    public void FallbackItemsAppendFallbackSuffixToCommandIds()
    {
        var firstCommand = new NoOpCommand { Id = "com.microsoft.powertoys.first" };
        var secondCommand = new NoOpCommand { Id = "com.microsoft.powertoys.second" };

        var firstFallback = new PowerToysFallbackCommandItem(firstCommand, "First", string.Empty, null, null);
        var secondFallback = new PowerToysFallbackCommandItem(secondCommand, "Second", string.Empty, null, null);

        Assert.AreEqual($"{firstCommand.Id}.fallback", firstFallback.Id);
        Assert.AreEqual($"{secondCommand.Id}.fallback", secondFallback.Id);
        Assert.AreNotEqual(firstCommand.Id, firstFallback.Id);
        Assert.AreNotEqual(secondCommand.Id, secondFallback.Id);
        Assert.AreNotEqual(firstFallback.Id, secondFallback.Id);
    }
}
