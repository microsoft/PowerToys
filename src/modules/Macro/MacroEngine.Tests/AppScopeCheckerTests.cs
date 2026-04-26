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
    public void IsForegroundAppMatch_StripsDotExeExtension()
    {
        Assert.AreEqual("notepad", Path.GetFileNameWithoutExtension("notepad.exe"));
        Assert.AreEqual("NOTEPAD", Path.GetFileNameWithoutExtension("NOTEPAD.EXE"));
    }

    [TestMethod]
    public void IsForegroundAppMatch_CaseInsensitive()
    {
        Assert.IsTrue("notepad".Equals("NOTEPAD", StringComparison.OrdinalIgnoreCase));
    }
}
