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
    public class DefaultLayoutsTest
    {
        public DefaultLayoutsTest()
        {
            FancyZonesEditorHelper.InitFancyZonesLayout();
        }
    }
}
