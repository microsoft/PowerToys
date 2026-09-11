// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class PowerShellErrorFormatterTests
{
    [TestMethod]
    public void FormatsErrorsAndOmitsProgressRecords()
    {
        const string input = "#< CLIXML\n<Objs><Obj S='progress'><AV>Preparing modules</AV></Obj><S S='Error'>Access denied._x000D__x000A_</S></Objs>";
        Assert.AreEqual("Access denied.\r\n", PowerShellErrorFormatter.Format(input));
    }

    [TestMethod]
    [DataRow("plain error")]
    [DataRow("#< CLIXML\n<Objs>")]
    [DataRow("#< CLIXML\n<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///C:/Windows/win.ini'>]><Objs><S S='Error'>&e;</S></Objs>")]
    public void PreservesPlainTruncatedAndDtdInputWithoutExpandingEntities(string input)
    {
        Assert.AreEqual(input, PowerShellErrorFormatter.Format(input));
    }
}
