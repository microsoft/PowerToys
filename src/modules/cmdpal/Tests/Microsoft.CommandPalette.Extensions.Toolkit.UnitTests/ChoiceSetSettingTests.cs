// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class ChoiceSetSettingTests
{
    [TestMethod]
    public void ChoiceSetSetting_UsesAndExposesTheInitializedCollection()
    {
        List<ChoiceSetSetting.Choice> choices = [new("First", "first")];
        var setting = new ChoiceSetSetting("choice", choices);

        choices.Add(new("Second", "second"));

        Assert.AreSame(choices, setting.Choices);
        CollectionAssert.AreEqual(choices, (List<ChoiceSetSetting.Choice>)setting.ToDictionary()["choices"]);
    }
}
