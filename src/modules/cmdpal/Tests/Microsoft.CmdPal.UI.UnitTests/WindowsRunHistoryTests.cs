// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public sealed class WindowsRunHistoryTests
{
    private string _keyPath = null!;
    private RegistryKey _key = null!;

    [TestInitialize]
    public void Initialize()
    {
        _keyPath = $@"Software\Microsoft\PowerToys\Tests\RunHistory-{Guid.NewGuid():N}";
        _key = Registry.CurrentUser.CreateSubKey(_keyPath);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _key?.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
    }

    [TestMethod]
    public void ReadPreservesMruOrderAndIgnoresUnlistedValues()
    {
        _key.SetValue("MRUList", "ca");
        _key.SetValue("a", @"notepad\1");
        _key.SetValue("b", @"unlisted\1");
        _key.SetValue("c", @"cmd /k echo test\1");

        AssertHistory("cmd /k echo test", "notepad");
    }

    [TestMethod]
    public void ReadMissingKeyReturnsEmptyHistory()
    {
        Assert.AreEqual(0, WindowsRunHistory.Read(null).Count);
    }

    [TestMethod]
    public void ReadWithoutMruOrderReturnsEmptyHistory()
    {
        _key.SetValue("a", @"notepad\1");

        Assert.AreEqual(0, WindowsRunHistory.Read(_key).Count);
    }

    [TestMethod]
    public void ReadNonStringMruOrderReturnsEmptyHistory()
    {
        _key.SetValue("MRUList", 1);
        _key.SetValue("a", @"notepad\1");

        Assert.AreEqual(0, WindowsRunHistory.Read(_key).Count);
    }

    [TestMethod]
    public void ReadSkipsMissingNonStringAndEmptyEntries()
    {
        _key.SetValue("MRUList", "abcde");
        _key.SetValue("b", 42);
        _key.SetValue("c", string.Empty);
        _key.SetValue("d", @"\1");
        _key.SetValue("e", @"notepad\1");

        AssertHistory("notepad");
    }

    [TestMethod]
    public void ReadSkipsInvalidAndRepeatedEntryNames()
    {
        _key.SetValue("MRUList", "?bbAaz");
        _key.SetValue("?", @"invalid\1");
        _key.SetValue("a", @"notepad\1");
        _key.SetValue("b", @"cmd\1");
        _key.SetValue("z", @"winver\1");

        AssertHistory("cmd", "notepad", "winver");
    }

    [TestMethod]
    [DataRow(@"notepad\1", "notepad")]
    [DataRow(@"notepad\5", "notepad")]
    [DataRow(@"C:\folder\1\1", @"C:\folder\1")]
    [DataRow(@"C:\folder\\1", @"C:\folder\")]
    [DataRow("notepad", "notepad")]
    [DataRow(@"C:\folder\", @"C:\folder\")]
    [DataRow("x", "x")]
    [DataRow("  cmd /k echo test  \\1", "  cmd /k echo test  ")]
    [DataRow("notepad \"C:\\caf\u00e9\\file.txt\"\\1", "notepad \"C:\\caf\u00e9\\file.txt\"")]
    public void ReadStripsOnlyTheShowCommandSuffix(string stored, string expected)
    {
        _key.SetValue("MRUList", "a");
        _key.SetValue("a", stored);

        AssertHistory(expected);
    }

    [TestMethod]
    public void ReadPreservesEnvironmentVariables()
    {
        _key.SetValue("MRUList", "a");
        _key.SetValue("a", @"%SystemRoot%\System32\cmd.exe\1", RegistryValueKind.ExpandString);

        AssertHistory(@"%SystemRoot%\System32\cmd.exe");
    }

    private void AssertHistory(params string[] expected)
    {
        CollectionAssert.AreEqual(expected, WindowsRunHistory.Read(_key).ToArray());
    }
}
