// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UITests;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI;

namespace UITests_FancyZonesEditor
{
    [TestClass]
    public class NewFancyZonesEditorTest
    {
        public NewFancyZonesEditorTest()
        {
            FancyZonesEditorHelper.InitFancyZonesLayout();
        }

        [TestClass]
        public class TestCaseFirstLaunch : UITestBase
        {
            public TestCaseFirstLaunch()
                : base(PowerToysModule.FancyZone)
            {
            }

            [TestInitialize]
            public void TestInitialize()
            {
                // files not yet exist
                FancyZonesEditorSession.Files.LayoutTemplatesIOHelper.DeleteFile();
                FancyZonesEditorSession.Files.CustomLayoutsIOHelper.DeleteFile();
                FancyZonesEditorSession.Files.LayoutHotkeysIOHelper.DeleteFile();
                FancyZonesEditorSession.Files.DefaultLayoutsIOHelper.DeleteFile();
                this.RestartScopeExe();
            }

            [TestCleanup]
            public void TestCleanup()
            {
            }

            [TestMethod]
            public void FirstLaunch() // verify the session is initialized
            {
                Assert.IsNotNull(Session.Find("FancyZones Layout"));
            }
        }
    }
}
