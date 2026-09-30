// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

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
