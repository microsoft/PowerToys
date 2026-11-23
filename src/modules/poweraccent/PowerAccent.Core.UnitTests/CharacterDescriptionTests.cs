// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerAccent.Core.UnitTests;

[TestClass]
public sealed class CharacterDescriptionTests
{
    [TestMethod]
    public void HiddenDescriptions_DoNotBuildUnicodeMetadata()
    {
        var descriptions = global::PowerAccent.Core.PowerAccent.GetCharacterDescriptions(["A"], showDescriptions: false);

        Assert.IsEmpty(descriptions);
    }

    [TestMethod]
    public void VisibleDescriptions_KeepUnicodeCodePointAndName()
    {
        var descriptions = global::PowerAccent.Core.PowerAccent.GetCharacterDescriptions(["A"], showDescriptions: true);

        Assert.HasCount(1, descriptions);
        Assert.AreEqual("(U+0041) LATIN CAPITAL LETTER A", descriptions[0]);
    }
}
