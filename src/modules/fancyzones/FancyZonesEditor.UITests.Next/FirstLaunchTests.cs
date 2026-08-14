// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class FirstLaunchTests : FancyZonesEditorTestBase
{
    public FirstLaunchTests()
    {
        EditorTestData.WriteForFirstLaunchTests(Files);
    }

    [TestMethod]
    [TestCategory("FancyZonesEditor")]
    public void FirstLaunch()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
    }
}