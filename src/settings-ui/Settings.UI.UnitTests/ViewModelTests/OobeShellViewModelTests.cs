// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;

using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class OobeShellViewModelTests
    {
        [TestMethod]
        public void ModulesPlacesAltWindowCycleImmediatelyBeforeWorkspaces()
        {
            var viewModel = new OobeShellViewModel();
            var moduleNames = viewModel.Modules.Select(module => module.ModuleName).ToList();

            int altWindowCycleIndex = moduleNames.IndexOf(PowerToysModules.AltWindowCycle.ToString());
            int workspacesIndex = moduleNames.IndexOf(PowerToysModules.Workspaces.ToString());

            Assert.IsTrue(altWindowCycleIndex >= 0);
            Assert.AreEqual(altWindowCycleIndex + 1, workspacesIndex);
        }
    }
}
