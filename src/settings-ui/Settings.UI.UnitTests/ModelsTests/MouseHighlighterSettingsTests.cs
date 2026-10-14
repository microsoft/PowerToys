// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class MouseHighlighterSettingsTests
    {
        [TestMethod]
        public void Defaults_ShouldUseRecommendedClickColors()
        {
            var settings = new MouseHighlighterSettings();

            Assert.AreEqual("#a6BFFF00", settings.Properties.LeftButtonClickColor.Value);
            Assert.AreEqual("#a600BFFF", settings.Properties.RightButtonClickColor.Value);
        }
    }
}
