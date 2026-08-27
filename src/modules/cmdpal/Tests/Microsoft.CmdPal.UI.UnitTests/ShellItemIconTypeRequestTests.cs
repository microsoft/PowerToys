// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class ShellItemIconTypeRequestTests
{
    [TestMethod]
    public void SameExtensionProducesOneCaseInsensitiveTypeIdentity()
    {
        var first = new ShellItemIconRequest(@"C:\Windows\System32\first.dll", jumbo: false);
        var second = new ShellItemIconRequest(@"C:\Windows\System32\SECOND.DLL", jumbo: false);

        Assert.IsTrue(ShellItemIconTypeRequest.TryCreate(first, out var firstType));
        Assert.IsTrue(ShellItemIconTypeRequest.TryCreate(second, out var secondType));

        Assert.AreEqual(firstType.CacheIdentity, secondType.CacheIdentity);
        Assert.AreEqual(ShellItemIconLocationMode.FileType, firstType.LocationMode);
        Assert.AreEqual(first.ItemPath, firstType.ItemPath);
        Assert.AreNotEqual(first.CacheIdentity, firstType.CacheIdentity);
    }

    [TestMethod]
    public void JumboAndStandardTypeIdentitiesRemainSeparate()
    {
        Assert.IsTrue(ShellItemIconTypeRequest.TryCreate(
            new ShellItemIconRequest(@"C:\Files\report.txt", jumbo: false),
            out var standard));
        Assert.IsTrue(ShellItemIconTypeRequest.TryCreate(
            new ShellItemIconRequest(@"C:\Files\report.txt", jumbo: true),
            out var jumbo));

        Assert.AreNotEqual(standard.CacheIdentity, jumbo.CacheIdentity);
        Assert.IsFalse(standard.Jumbo);
        Assert.IsTrue(jumbo.Jumbo);
    }

    [TestMethod]
    public void ExtensionlessItemsSkipTheProvisionalTypePhase()
    {
        Assert.IsFalse(ShellItemIconTypeRequest.TryCreate(
            new ShellItemIconRequest(@"C:\Files\README", jumbo: false),
            out _));
    }

    [TestMethod]
    public void ShortcutItemsSkipTheProvisionalTypePhase()
    {
        Assert.IsFalse(ShellItemIconTypeRequest.TryCreate(
            new ShellItemIconRequest(@"C:\Files\target.lnk", jumbo: false),
            out _));
        Assert.IsFalse(ShellItemIconTypeRequest.TryCreate(
            new ShellItemIconRequest(@"C:\Files\TARGET.LNK", jumbo: true),
            out _));
    }
}
