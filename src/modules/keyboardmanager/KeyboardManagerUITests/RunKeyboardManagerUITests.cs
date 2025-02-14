// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UITests_KeyboardManager
{
    [TestClass]
    public class RunKeyboardManagerUITests : UITestBase
    {
        [TestMethod]
        public void OpenKeyboardManagerEditor()
        {
            // Open KeyboardManagerEditor
            Session.FindElement<Button>(By.Name("Remap a key")).Click();
            Session.Attach(PowerToysModuleWindow.KeyboardManagerKeys);

            // Maximize window
            var window = Session.FindElementByName<Window>("Remap keys").Maximize();

            // Add Key Remapping
            Session.FindElementByName<Button>("Add key remapping").Click();
            Session.FindElementByName<Element>("Row 1, Select:").FindElementByName<Button>("Select").Click();
            Session.FindElementByName<Window>("Select a key on selected keyboard").FindElementByName<Button>("Cancel").Click();
            window.Close();

            // Back to Settings
            Session.Attach(PowerToysModuleWindow.PowerToysSettings);
        }
    }
}
