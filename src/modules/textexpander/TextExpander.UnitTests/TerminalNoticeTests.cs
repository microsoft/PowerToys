// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

/// <summary>What the notice at the caret says when a snippet with lines wasn't put into a terminal.</summary>
[TestClass]
public sealed class TerminalNoticeTests
{
    [TestMethod]
    public void BothRefusalsAreTheSameNoticeAndEachSaysWhy()
    {
        Assert.AreEqual(
            ("Couldn't insert this snippet into the terminal", "A snippet with more than one line goes into a terminal only as a paste, and the clipboard couldn't be used just now, so the trigger was left as you typed it."),
            TerminalNotice.Refusal(alwaysType: false));
        Assert.AreEqual(
            ("Couldn't insert this snippet into the terminal", "A snippet with copied text and more than one line goes into a terminal only as a paste, and Insertion method is set to Always type, so the trigger was left as you typed it."),
            TerminalNotice.Refusal(alwaysType: true));
    }
}
