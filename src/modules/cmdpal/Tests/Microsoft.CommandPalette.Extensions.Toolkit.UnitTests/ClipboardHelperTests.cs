// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
[DoNotParallelize]
public class ClipboardHelperTests
{
    [TestMethod]
    public void SetText_RoundTripsThroughClipboard()
    {
        var value = $"Command Palette clipboard test {Guid.NewGuid():N}";

        ClipboardHelper.SetText(value);

        Assert.AreEqual(value, ClipboardHelper.GetText());
    }

    [TestMethod]
    public void SetImage_RejectsNullReference()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ClipboardHelper.SetImage(null!));
    }

    [TestMethod]
    public void SetContent_RejectsNullPackage()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ClipboardHelper.SetContent(null!));
    }
}
