// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Microsoft.PowerToys.UITest.UITestBase;

namespace PowerOCR.UITests;

[TestClass]
public class PowerOCRTests : UITestBase
{
    public PowerOCRTests()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Medium)
    {
    }

    [TestInitialize]
    public void TestInitialize()
    {
        if (FindAll<NavigationViewItem>("Text Extractor").Count == 0)
        {
            // Expand Advanced list-group if needed
            Find<NavigationViewItem>("System Tools").Click();
        }

        Find<NavigationViewItem>("Text Extractor").Click();

        Find<ToggleSwitch>("Enable Text Extractor").Toggle(true);

        SendKeys(Key.Win, Key.D);
    }

    [TestMethod("PowerOCR.DetectTextExtractor")]
    [TestCategory("PowerOCR Detection")]
    public void DetectTextExtractorTest()
    {
        try
        {
            SendKeys(Key.Win, Key.Shift, Key.T);

            Thread.Sleep(5000);

            var textExtractorWindow = Find("TextExtractor", 10000, true);

            Assert.IsNotNull(textExtractorWindow, "TextExtractor window should be found after hotkey activation");

            Console.WriteLine("✓ TextExtractor window detected successfully after hotkey activation");

            SendKeys(Key.Esc);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to detect TextExtractor window: {ex.Message}");
            Assert.Fail("TextExtractor window was not found after hotkey activation");
        }
    }
}
