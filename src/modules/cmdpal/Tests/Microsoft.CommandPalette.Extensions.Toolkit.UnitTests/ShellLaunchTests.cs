// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class ShellLaunchTests
{
    [TestMethod]
    [DataRow(@"C:\Apps\Example.exe")]
    [DataRow(@"C:\Start Menu\Example.lnk")]
    [DataRow(@"C:\Documents\Example.txt")]
    [DataRow("https://example.com/")]
    [DataRow(@"shell:AppsFolder\Example_123!App")]
    public void OrdinaryLaunch_UsesExplorer(string target)
    {
        var info = ShellHelpers.CreateShellStartInfo(target, "--arg \"two words\"", @"C:\Working Directory", ShellHelpers.ShellRunAsType.None, false);
        ProcessStartInfo? launched = null;
        ShellHelpers.Launch(info, value => launched = value, _ => Assert.Fail("Ordinary launches must not create a child of CmdPal."));

        Assert.AreSame(info, launched);
        Assert.IsNotNull(launched);
        Assert.AreEqual(target, launched.FileName);
        Assert.AreEqual("--arg \"two words\"", launched.Arguments);
        Assert.AreEqual(@"C:\Working Directory", launched.WorkingDirectory);
        Assert.AreEqual(string.Empty, launched.Verb);
        Assert.AreEqual(ProcessWindowStyle.Normal, launched.WindowStyle);
    }

    [TestMethod]
    [DataRow(ShellHelpers.ShellRunAsType.Administrator, "RunAs")]
    [DataRow(ShellHelpers.ShellRunAsType.OtherUser, "RunAsUser")]
    public void ExplicitRunAs_PreservesDirectShellLaunch(ShellHelpers.ShellRunAsType runAs, string verb)
    {
        var info = ShellHelpers.CreateShellStartInfo("example.exe", "--arg", @"C:\Working Directory", runAs, true);
        ProcessStartInfo? launched = null;
        ShellHelpers.Launch(info, _ => Assert.Fail("Explicit run-as must retain its launch context."), value => launched = value);

        Assert.AreSame(info, launched);
        Assert.IsNotNull(launched);
        Assert.AreEqual(verb, launched.Verb);
        Assert.AreEqual(ProcessWindowStyle.Hidden, launched.WindowStyle);
        Assert.IsTrue(launched.UseShellExecute);
    }

    [TestMethod]
    public void ExplorerFailure_IsNotRetriedInCaller()
    {
        var info = ShellHelpers.CreateShellStartInfo("example.exe", null, null, ShellHelpers.ShellRunAsType.None, false);
        var failure = new InvalidOperationException("Explorer is not available.");
        var actual = Assert.ThrowsException<InvalidOperationException>(() =>
            ShellHelpers.Launch(info, _ => throw failure, _ => Assert.Fail("A fallback would recreate the parentage bug.")));

        Assert.AreSame(failure, actual);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void MissingDirectory_UsesCallerDirectory(string? directory)
    {
        var info = ShellHelpers.CreateShellStartInfo("example.exe", null, directory, ShellHelpers.ShellRunAsType.None, true);

        Assert.AreEqual(Environment.CurrentDirectory, info.WorkingDirectory);
        Assert.AreEqual(string.Empty, info.Arguments);
        Assert.AreEqual(ProcessWindowStyle.Hidden, info.WindowStyle);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void InvalidTarget_IsReportedAsFailure(string? target)
    {
        Assert.IsFalse(ShellHelpers.OpenInShell(target));
    }

    [TestMethod]
    [DataRow(".")]
    [DataRow(@"..\Working Directory")]
    public void RelativeDirectory_IsResolvedBeforeCrossingIntoExplorer(string directory)
    {
        var info = ShellHelpers.CreateShellStartInfo("example.exe", null, directory, ShellHelpers.ShellRunAsType.None, false);

        Assert.AreEqual(Path.GetFullPath(directory), info.WorkingDirectory);
    }

    [TestMethod]
    public void UnknownRunAs_IsRejected()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            ShellHelpers.CreateShellStartInfo("example.exe", null, null, (ShellHelpers.ShellRunAsType)42, false));
    }
}
