// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Forms = System.Windows.Forms;

namespace Microsoft.PowerToys.ZoomIt.UITests;

[TestClass]
[TestCategory("ZoomIt")]
[DoNotParallelize]
public sealed class ZoomItFixtureTests
{
    [TestMethod]
    public void SourceWindowIsDisposedWhenInitializationTimesOut()
    {
        Forms.Form? created = null;
        Assert.ThrowsExactly<TimeoutException>(() =>
        {
            using var fixture = new DesktopFixture(
                createWindow: () =>
                {
                    Thread.Sleep(200);
                    created = new Forms.Form();
                    return created;
                },
                initializationTimeout: TimeSpan.FromMilliseconds(50));
        });
        Assert.IsNotNull(created);
        Assert.IsTrue(created.IsDisposed, "A source window created after the timeout must still be disposed before the constructor exits.");
    }

    [TestMethod]
    public void SourceWindowCreationFailureIsPropagated()
    {
        var expected = new InvalidOperationException("Injected source-window creation failure.");
        var actual = Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            using var fixture = new DesktopFixture(createWindow: () => throw expected);
        });
        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public void ShortcutParserPreservesSupportedKeys()
    {
        CollectionAssert.AreEqual(new[] { Key.Ctrl, Key.Shift, Key.Num7 }, ZoomItUi.ParseShortcut("Demo", "Ctrl + Shift + 7"));
        CollectionAssert.AreEqual(new[] { Key.LWin, Key.Alt, Key.F2 }, ZoomItUi.ParseShortcut("Zoom", "windows + alt + F2"));
    }

    [TestMethod]
    public void UnsupportedShortcutNamesItsCardAndText()
    {
        const string card = "ZoomItDemoTypeShortcut";
        const string text = "Ctrl + UnsupportedKey";
        var error = Assert.ThrowsExactly<AssertFailedException>(() => ZoomItUi.ParseShortcut(card, text));
        StringAssert.Contains(error.Message, card);
        StringAssert.Contains(error.Message, text);
        StringAssert.Contains(error.Message, "UnsupportedKey");
    }
}
