// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroEngine;

namespace PowerToys.MacroEngine.Tests;

[TestClass]
public sealed class AppScopeCheckerTests
{
    [TestMethod]
    public void ProcessNamesMatch_StripsDotExeExtension()
    {
        Assert.IsTrue(AppScopeChecker.ProcessNamesMatch("notepad.exe", "notepad"));
        Assert.IsTrue(AppScopeChecker.ProcessNamesMatch("NOTEPAD.EXE", "notepad"));
    }

    [TestMethod]
    public void ProcessNamesMatch_CaseInsensitive()
    {
        Assert.IsTrue(AppScopeChecker.ProcessNamesMatch("notepad.exe", "NOTEPAD"));
    }

    [TestMethod]
    public void ProcessNamesMatch_DifferentNames_ReturnsFalse()
    {
        Assert.IsFalse(AppScopeChecker.ProcessNamesMatch("notepad.exe", "chrome"));
    }

    [TestMethod]
    public void ProcessNamesMatch_NoExtension_Matches()
    {
        Assert.IsTrue(AppScopeChecker.ProcessNamesMatch("notepad", "notepad"));
    }

    [TestMethod]
    public void ProcessNamesMatch_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(AppScopeChecker.ProcessNamesMatch(null, "notepad"));
        Assert.IsFalse(AppScopeChecker.ProcessNamesMatch("notepad.exe", null));
        Assert.IsFalse(AppScopeChecker.ProcessNamesMatch(string.Empty, "notepad"));
        Assert.IsFalse(AppScopeChecker.ProcessNamesMatch("notepad.exe", string.Empty));
    }
}
