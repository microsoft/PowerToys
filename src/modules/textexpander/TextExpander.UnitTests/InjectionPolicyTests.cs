// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class InjectionPolicyTests
{
    [TestMethod]
    public void ClipboardRoutingMatchesBackendAndThreshold()
    {
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("Thank you!", 5, InjectionBackend.Auto));
        Assert.IsFalse(InjectionPolicy.ShouldUseClipboard("brb", 5, InjectionBackend.Auto));
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("12345", 5, InjectionBackend.Auto));
        Assert.IsFalse(InjectionPolicy.ShouldUseClipboard("1234", 5, InjectionBackend.Auto));
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("a\nb", 5, InjectionBackend.Auto));
        Assert.IsFalse(InjectionPolicy.ShouldUseClipboard("line\nbreak", 5, InjectionBackend.Typing));
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("brb", 5, InjectionBackend.Clipboard));
    }

    [TestMethod]
    public void EnvironmentParsingAcceptsDocumentedValuesOnly()
    {
        Assert.AreEqual(InjectionBackend.Typing, InjectionPolicy.ParseBackend("type", InjectionBackend.Auto));
        Assert.AreEqual(InjectionBackend.Clipboard, InjectionPolicy.ParseBackend(" PASTE ", InjectionBackend.Auto));
        Assert.AreEqual(InjectionBackend.Auto, InjectionPolicy.ParseBackend("nonsense", InjectionBackend.Auto));
        Assert.AreEqual(12, InjectionPolicy.ParseThreshold("12", 5));
        Assert.AreEqual(5, InjectionPolicy.ParseThreshold("0", 5));
        Assert.IsTrue(InjectionPolicy.ParseFlag("yes", false));
        Assert.IsFalse(InjectionPolicy.ParseFlag("off", true));
        Assert.IsTrue(InjectionPolicy.ParseFlag("banana", true));
    }
}
