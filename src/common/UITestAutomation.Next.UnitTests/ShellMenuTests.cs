// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class ShellMenuTests
{
    private static readonly string[] ExpectedCaptions = ["Cafe\u0301-\u6f22-\U0001F680", "R&D", "R&D"];

    [TestMethod]
    public void WindowClassesDistinguishClassicAndModernPopups()
    {
        Assert.IsTrue(ShellMenu.WindowClassMatches("#32768", ShellMenu.ClassicWindowClassName));
        Assert.IsFalse(ShellMenu.WindowClassMatches("#32768-extra", ShellMenu.ClassicWindowClassName));
        Assert.IsTrue(ShellMenu.WindowClassMatches(
            "prefix.Microsoft.UI.Content.PopupWindowSiteBridge.suffix", ShellMenu.ModernWindowClassName));
        Assert.IsFalse(ShellMenu.WindowClassMatches("CabinetWClass", ShellMenu.ModernWindowClassName));
    }

    [TestMethod]
    public void VisibleItemWaitReobservesZeroBoundsAndRequiresExactCaptionAndType()
    {
        var ready = Item("Open templates", width: 100);
        var observations = new Queue<IReadOnlyList<Element>>(
        [
            [Item("Open templates", width: 0)],
            [Item("Open templates elsewhere", width: 100), Item("Open templates", width: 100, type: "Text")],
            [ready],
        ]);

        var result = ShellMenu.WaitForVisibleMenuItem(
            observations.Dequeue, "Open templates", 1_000, StringComparison.Ordinal, pollIntervalMS: 1);

        Assert.AreSame(ready, result);
        Assert.HasCount(0, observations);
    }

    [TestMethod]
    public void VisibleItemComparisonPolicyIsExplicit()
    {
        var item = Item("Open templates", width: 100);
        Assert.AreSame(
            item,
            ShellMenu.WaitForVisibleMenuItem(() => new[] { item }, "OPEN TEMPLATES", 1_000, StringComparison.OrdinalIgnoreCase, 1));
        Assert.IsNull(
            ShellMenu.WaitForVisibleMenuItem(() => new[] { item }, "OPEN TEMPLATES", 20, StringComparison.Ordinal, 1));
    }

    [TestMethod]
    public void PositiveBoundsDoNotBypassTheOffscreenProbe()
    {
        var item = Item("Preview", width: 100);
        var probes = 0;
        var result = ShellMenu.WaitForVisibleMenuItem(
            () => new[] { item },
            "Preview",
            1_000,
            StringComparison.Ordinal,
            pollIntervalMS: 1,
            isDisplayed: _ => ++probes > 1);

        Assert.AreSame(item, result);
        Assert.AreEqual(2, probes);
    }

    [TestMethod]
    public void VisibleItemWaitRetriesRecognizedStaleElements()
    {
        var calls = 0;
        var expected = Item("Preview", width: 100);
        var result = ShellMenu.WaitForVisibleMenuItem(
            () => ++calls == 1 ? throw new AssertFailedException("stale_element") : new[] { expected },
            "Preview",
            1_000,
            StringComparison.Ordinal,
            1);

        Assert.AreSame(expected, result);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public void VisibleItemWaitDoesNotSwallowUnrelatedFailures()
    {
        Assert.Throws<InvalidOperationException>(() => ShellMenu.WaitForVisibleMenuItem(
            () => throw new InvalidOperationException("Unexpected failure"),
            "Preview",
            1_000,
            StringComparison.Ordinal,
            1));
        Assert.IsFalse(ShellMenu.IsTransientElementException(new AssertFailedException("signature validation failed")));
    }

    [TestMethod]
    public void SubmenuDiscoveryUsesContentAndOwningProcessNotTheFirstPopup()
    {
        var root = new Session(PowerToysModule.Runner, 1, "Root", 10, "explorer");
        var searched = new List<long>();
        var marker = Item("Open templates", width: 100);
        var result = ShellMenu.WaitForSubmenuCore(
            root,
            "Open templates",
            () => new[]
            {
                Window(1, 10),
                Window(2, 20),
                Window(3, 10),
                Window(4, 10),
            },
            (session, _, _, _) =>
            {
                searched.Add(session.WindowHandle);
                return session.WindowHandle == 4 ? marker : null;
            },
            timeoutMS: 1_000,
            requiredConsecutiveMatches: 2,
            comparison: StringComparison.Ordinal,
            pollIntervalMS: 1);

        Assert.IsNotNull(result);
        Assert.AreEqual(4L, result.WindowHandle);
        CollectionAssert.AreEqual(new long[] { 3, 4, 3, 4 }, searched);
    }

    [TestMethod]
    public void SubmenuDiscoveryRebindsWhenThePopupIsReplaced()
    {
        var root = new Session(PowerToysModule.Runner, 1, "Root", 10, "explorer");
        var observations = 0;
        var marker = Item("Open templates", width: 100);
        var result = ShellMenu.WaitForSubmenuCore(
            root,
            "Open templates",
            () => new[] { Window(++observations == 1 ? 2 : 3, 10) },
            (session, _, _, _) => session.WindowHandle == 3 ? marker : null,
            timeoutMS: 1_000,
            requiredConsecutiveMatches: 2,
            comparison: StringComparison.Ordinal,
            pollIntervalMS: 1);

        Assert.IsNotNull(result);
        Assert.AreEqual(3L, result.WindowHandle);
        Assert.AreEqual(3, observations);
    }

    [TestMethod]
    public void CaptionInventoryPreservesDuplicatesUnicodeAndLiteralAmpersands()
    {
        using var document = JsonDocument.Parse("""
            {"windows":[{"elements":[
              {"type":"Menu","children":[
                {"type":"MenuItem","name":"Cafe\u0301-\u6f22-\ud83d\ude80"},
                {"type":"MenuItem","name":"R&D"},
                {"type":"MenuItem","name":"R&D"},
                {"type":"Separator","name":"Ignore"},
                {"type":"Text","name":"Ignore"},
                {"type":"MenuItem","name":""},
                {"type":"MenuItem","name":null}
              ]}
            ]}]}
            """);

        CollectionAssert.AreEqual(
            ExpectedCaptions,
            ShellMenu.ParseMenuNames(document.RootElement).ToArray());
    }

    [TestMethod]
    [DataRow("&Rename\tCtrl+R", "Rename")]
    [DataRow("R&&D", "R&D")]
    [DataRow("  &Open templates  ", "Open templates")]
    [DataRow("", "")]
    [DataRow("Cafe\u0301-\u6f22-\U0001F680", "Cafe\u0301-\u6f22-\U0001F680")]
    public void ClassicCaptionNormalizationPreservesVisibleText(string caption, string expected) =>
        Assert.AreEqual(expected, ShellMenu.NormalizeClassicCaption(caption));

    [TestMethod]
    public void AnAbsentNativeMenuIsNotAnEmptySuccessfulInventory() =>
        Assert.IsNull(ShellMenu.TryReadClassicItemCaptions(IntPtr.Zero));

    private static Element Item(string name, int width, string type = "MenuItem") =>
        new() { Name = name, ControlType = type, Width = width, Height = 20 };

    private static WindowsFinder.WindowInfo Window(long handle, int processId) =>
        new(handle, string.Empty, "explorer", processId, ShellMenu.ModernWindowClassName, 100, 100);
}
