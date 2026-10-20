// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class PowerScripts
    {
        private static readonly string[] DisabledNetworkPolicy = { PowerScriptListItem.NetworkPolicy };
        private static readonly string[] UnknownPolicy = { "futurePolicy" };

        [TestMethod]
        public void CatalogueFiltersByPrimaryInputWithoutChangingScriptInstances()
        {
            var scripts = CreateCatalogue();
            scripts[0].Parameters.Add(new PowerScriptParameterItem { Name = "source", Type = "file", IsRequired = true });
            var noInput = PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 1);
            Assert.AreEqual(1, noInput.Count);
            Assert.AreSame(scripts[0], noInput[0], "Filtering must preserve each script's policy state.");
            Assert.AreEqual("action", noInput[0].Id);
            Assert.AreEqual(3, scripts.Length);

            var files = PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 2);
            Assert.AreEqual("files", files.Single().Id);
        }

        [DataTestMethod]
        [DataRow(0, "action", "text", "files")]
        [DataRow(1, "files", "text", "action")]
        [DataRow(2, "files", "action", "text")]
        [DataRow(3, "text", "files", "action")]
        [DataRow(4, "action", "text", "files")]
        public void CatalogueSortsBySelectedMetadata(int sortIndex, string first, string second, string third)
        {
            var sorted = PowerScriptsViewModel.FilterAndSortScripts(CreateCatalogue(), sortIndex, 0);
            CollectionAssert.AreEqual(new[] { first, second, third }, sorted.Select(script => script.Id).ToArray());
        }

        [TestMethod]
        public void CatalogueCanRecoverFromAnEmptyFilter()
        {
            var scripts = CreateCatalogue();
            Assert.AreEqual(0, PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 7).Count);
            Assert.AreEqual(3, PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 0).Count);
        }

        private static PowerScriptListItem[] CreateCatalogue() => new[]
        {
            new PowerScriptListItem { Id = "action", Name = "alpha", Runtime = "PowerShell", Io = new PowerScriptIo { Input = "none", Output = "text" } },
            new PowerScriptListItem { Id = "files", Name = "Zebra", Runtime = "Python", Io = new PowerScriptIo { Input = "files", Output = "none" } },
            new PowerScriptListItem { Id = "text", Name = "Bravo", Runtime = "PowerShell", Io = new PowerScriptIo { Input = "text", Output = "html" } },
        };

        [TestMethod]
        public void MxcSafeSettingsDoNotRequireRiskAcceptance()
        {
            Assert.IsFalse(PowerScriptsViewModel.RequiresMxcRiskAcceptance(true, Array.Empty<string>()));
            Assert.IsFalse(PowerScriptsViewModel.RequiresMxcRiskAcceptance(null, Array.Empty<string>()));
        }

        [TestMethod]
        public void MxcDisabledExecutionRequiresRiskAcceptance()
        {
            Assert.IsTrue(PowerScriptsViewModel.RequiresMxcRiskAcceptance(false, Array.Empty<string>()));
        }

        [TestMethod]
        public void MxcDisabledKnownPolicyRequiresRiskAcceptance()
        {
            Assert.IsTrue(PowerScriptsViewModel.RequiresMxcRiskAcceptance(
                true,
                DisabledNetworkPolicy));
        }

        [TestMethod]
        public void MxcUnknownPolicyDoesNotWeakenIsolation()
        {
            Assert.IsFalse(PowerScriptsViewModel.RequiresMxcRiskAcceptance(
                true,
                UnknownPolicy));
        }

        [TestMethod]
        public void MxcUnspecifiedPreferenceFollowsPlatformSupport()
        {
            Assert.IsTrue(PowerScriptsViewModel.ResolveMxcEnabled(null, true));
            Assert.IsFalse(PowerScriptsViewModel.ResolveMxcEnabled(null, false));
        }

        [TestMethod]
        public void MxcExplicitPreferenceOverridesPlatformDefault()
        {
            Assert.IsTrue(PowerScriptsViewModel.ResolveMxcEnabled(true, false));
            Assert.IsFalse(PowerScriptsViewModel.ResolveMxcEnabled(false, true));
        }

        [TestMethod]
        public void PerScriptPolicyCanStrengthenGlobalPolicy()
        {
            var effectiveDisabled = PowerScriptListItem.ResolveEffectiveDisabledPolicies(
                new[] { PowerScriptListItem.NetworkPolicy },
                new[] { PowerScriptListItem.NetworkPolicy },
                Array.Empty<string>());

            CollectionAssert.DoesNotContain(
                new System.Collections.Generic.List<string>(effectiveDisabled),
                PowerScriptListItem.NetworkPolicy);
        }

        [TestMethod]
        public void PerScriptPolicyCanRequestOnlyNeededAccess()
        {
            var effectiveDisabled = PowerScriptListItem.ResolveEffectiveDisabledPolicies(
                Array.Empty<string>(),
                new[]
                {
                    PowerScriptListItem.FilesystemPolicy,
                    PowerScriptListItem.UiPolicy,
                    PowerScriptListItem.LeastPrivilegePolicy,
                },
                new[] { PowerScriptListItem.NetworkPolicy });

            CollectionAssert.AreEqual(
                new[] { PowerScriptListItem.NetworkPolicy },
                new System.Collections.Generic.List<string>(effectiveDisabled));
        }
    }
}
