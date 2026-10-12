// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.UnitTests.ConvertersTests;

[TestClass]
public sealed class PasteFormatsToHeightConverterTests
{
    [TestMethod]
    public void GetHeight_DefaultDoesNotLimitItemCount()
    {
        var converter = new PasteFormatsToHeightConverter();

        Assert.AreEqual(400, converter.GetHeight(10));
    }

    [TestMethod]
    public void GetHeight_ExplicitMaxItemsLimitsItemCount()
    {
        var converter = new PasteFormatsToHeightConverter
        {
            MaxItems = 5,
        };

        Assert.AreEqual(200, converter.GetHeight(10));
    }
}
