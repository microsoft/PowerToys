// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerRename.UITests;

/// <summary>
/// Initializes a new instance of the <see cref="BasicRenameTests"/> class.
/// Initialize PowerRename UITest with custom test file paths
/// </summary>
/// <param name="testFilePaths">Array of file/folder paths to test with</param>
[TestClass]
public class BasicRenameTests : PowerRenameUITestBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BasicRenameTests"/> class.
    /// Initialize PowerRename UITest with default test files
    /// </summary>
    public BasicRenameTests()
        : base()
    {
    }

    [TestMethod]
    public void BasicInput()
    {
        this.SetSearchBoxText("search");
        this.SetReplaceBoxText("replace");
    }

    [TestMethod]
    public void BasicMatchFileName()
    {
        this.SetSearchBoxText("testCase1");
        this.SetReplaceBoxText("replaced");

        Assert.AreEqual("replaced.txt", this.Find<TextBlock>("replaced.txt").Text);
    }

    [TestMethod]
    public void BasicRegularMatch()
    {
        this.SetSearchBoxText("^test.*\\.txt$");
        this.SetReplaceBoxText("matched.txt");

        CheckOriginalOrRenamedCount(0);

        this.SetRegularExpressionCheckbox(true);

        CheckOriginalOrRenamedCount(2);

        Assert.AreEqual("matched.txt", this.Find<TextBlock>("matched.txt").Text);
    }

    [TestMethod]
    public void BasicMatchAllOccurrences()
    {
        this.SetSearchBoxText("t");
        this.SetReplaceBoxText("f");

        this.SetMatchAllOccurrencesCheckbox(true);

        Assert.AreEqual("fesfCase2.fxf", this.Find<TextBlock>("fesfCase2.fxf").Text);
        Assert.AreEqual("fesfCase1.fxf", this.Find<TextBlock>("fesfCase1.fxf").Text);
    }

    [TestMethod]
    public void BasicCaseSensitive()
    {
        this.SetSearchBoxText("testcase1");
        this.SetReplaceBoxText("match1");

        CheckOriginalOrRenamedCount(1);
        Assert.AreEqual("match1.txt", this.Find<TextBlock>("match1.txt").Text);

        this.SetCaseSensitiveCheckbox(true);
        CheckOriginalOrRenamedCount(0);
    }
}
