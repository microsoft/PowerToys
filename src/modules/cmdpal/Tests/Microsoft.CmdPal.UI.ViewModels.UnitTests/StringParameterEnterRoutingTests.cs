// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class StringParameterEnterRoutingTests
{
    [DataTestMethod]
    [DataRow(true, StringParameterEnterAction.Submit)]
    [DataRow(false, StringParameterEnterAction.FocusNext)]
    public void SingleLineEnter_HasOneOwnerAction(bool showCommand, StringParameterEnterAction expected)
    {
        Assert.AreEqual(expected, StringParameterEnterRouting.GetAction(VirtualKey.Enter, acceptsReturn: false, showCommand: showCommand));
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void MultilineEnter_RemainsAvailableToTheTextBox(bool showCommand)
    {
        Assert.AreEqual(
            StringParameterEnterAction.None,
            StringParameterEnterRouting.GetAction(VirtualKey.Enter, acceptsReturn: true, showCommand: showCommand));
    }

    [TestMethod]
    public void NonEnterKey_HasNoOwnerAction()
    {
        Assert.AreEqual(
            StringParameterEnterAction.None,
            StringParameterEnterRouting.GetAction(VirtualKey.Tab, acceptsReturn: false, showCommand: true));
    }
}
