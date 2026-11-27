// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Linq;
using System.Security.Principal;

using EnvironmentVariablesUILib.Helpers;
using Microsoft.Win32;

namespace EnvironmentVariablesUILib.UnitTests.Adapters
{
    [TestClass]
    [TestCategory("Adapter")]
    [DoNotParallelize]
    public class RegistryEnvironmentAdapterFunctionalTests
    {
        private const string MachineSubKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
        private const string GuestMarker = "POWERTOYS_EV_FUNCTIONAL_GUEST_MACHINE";

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [TestCategory("OwnedUserRegistry")]
        [DataRow(RegistryValueKind.String)]
        [DataRow(RegistryValueKind.ExpandString)]
        public void PrivateUserKey_RoundTripPreservesRawValueAndKind(RegistryValueKind kind)
        {
            WithOwnedUserKey((subKey, independent) =>
            {
                var expansionName = $"PT_EV_EXPAND_{Guid.NewGuid():N}";
                var raw = $@"%{expansionName}%\bin;;C:\Owned;";
                const string expanded = @"C:\OwnedExpanded\bin;;C:\Owned;";
                const string name = "OwnedValue";
                var missing = new object();
                Assert.IsNull(Environment.GetEnvironmentVariable(expansionName, EnvironmentVariableTarget.Process));
                using var adapter = WindowsEnvironmentVariablesEnvironment.Instance.OpenKey(RegistryHive.CurrentUser, subKey, writable: true);
                Assert.IsNotNull(adapter);
                try
                {
                    Environment.SetEnvironmentVariable(expansionName, @"C:\OwnedExpanded", EnvironmentVariableTarget.Process);
                    independent.SetValue("Sentinel", "unchanged", RegistryValueKind.String);
                    adapter.SetValue(name, raw, kind);

                    Assert.IsTrue(adapter.GetValueNames().Contains(name, StringComparer.Ordinal));
                    Assert.AreEqual(raw, adapter.GetValue(name, missing, RegistryValueOptions.DoNotExpandEnvironmentNames));
                    Assert.AreEqual(raw, independent.GetValue(name, missing, RegistryValueOptions.DoNotExpandEnvironmentNames));
                    Assert.AreEqual(kind == RegistryValueKind.ExpandString ? expanded : raw, adapter.GetValue(name, missing, RegistryValueOptions.None));
                    Assert.AreEqual(kind, independent.GetValueKind(name));
                    Assert.AreEqual(expanded, WindowsEnvironmentVariablesEnvironment.Instance.ExpandEnvironmentVariables(raw));

                    var replacement = $@"%{expansionName}%\edited;;C:\Changed;";
                    var replacementKind = kind == RegistryValueKind.String ? RegistryValueKind.ExpandString : RegistryValueKind.String;
                    adapter.SetValue(name, replacement, replacementKind);

                    Assert.AreEqual(replacement, independent.GetValue(name, missing, RegistryValueOptions.DoNotExpandEnvironmentNames));
                    Assert.AreEqual(replacement, adapter.GetValue(name, missing, RegistryValueOptions.DoNotExpandEnvironmentNames));
                    Assert.AreEqual(replacementKind, independent.GetValueKind(name));
                    Assert.AreEqual(replacementKind == RegistryValueKind.ExpandString ? @"C:\OwnedExpanded\edited;;C:\Changed;" : replacement, adapter.GetValue(name, missing, RegistryValueOptions.None));
                    Assert.AreEqual(2, adapter.GetValueNames().Length);
                    Assert.AreEqual("unchanged", independent.GetValue("Sentinel"));
                    Assert.AreEqual(RegistryValueKind.String, independent.GetValueKind("Sentinel"));

                    adapter.DeleteValue(name, throwOnMissingValue: true);
                    Assert.AreSame(missing, independent.GetValue(name, missing));
                    Assert.AreEqual("unchanged", independent.GetValue("Sentinel"));
                    adapter.DeleteValue(name, throwOnMissingValue: false);
                    Assert.ThrowsExactly<ArgumentException>(() => adapter.DeleteValue(name, throwOnMissingValue: true));
                }
                finally
                {
                    Environment.SetEnvironmentVariable(expansionName, null, EnvironmentVariableTarget.Process);
                }
            });
        }

        [TestMethod]
        [TestCategory("OwnedUserRegistry")]
        public void PrivateUserKey_ReadOnlyHandleRejectsWriteWithoutMutation()
        {
            WithOwnedUserKey((subKey, independent) =>
            {
                const string name = "OwnedValue";
                independent.SetValue(name, "original", RegistryValueKind.String);
                using var adapter = WindowsEnvironmentVariablesEnvironment.Instance.OpenKey(RegistryHive.CurrentUser, subKey, writable: false);
                Assert.IsNotNull(adapter);

                var failure = Assert.ThrowsExactly<UnauthorizedAccessException>(() => adapter.SetValue(name, "replacement", RegistryValueKind.String));

                Assert.AreEqual("original", independent.GetValue(name));
                Assert.AreEqual(RegistryValueKind.String, independent.GetValueKind(name));
                TestContext.WriteLine($"Read-only registry write: {failure.GetType().FullName}; HResult=0x{failure.HResult:X8}");
            });
        }

        [TestMethod]
        [TestCategory("RequiresDisposableWindows")]
        [DataRow(false, RegistryValueKind.String)]
        [DataRow(false, RegistryValueKind.ExpandString)]
        [DataRow(true, RegistryValueKind.String)]
        [DataRow(true, RegistryValueKind.ExpandString)]
        public void RoundTrip_UsesRequestedScopeAndPreservesRawValueKind(bool machine, RegistryValueKind kind)
        {
            RequireDisposableGuest(machine);
            var name = $"PT_EV_{Guid.NewGuid():N}";
            var expansionName = $"PT_EV_EXPAND_{Guid.NewGuid():N}";
            var raw = $@"%{expansionName}%\bin;;C:\Owned;";
            const string expanded = @"C:\OwnedExpanded\bin;;C:\Owned;";
            var missing = new object();
            Assert.IsNull(Environment.GetEnvironmentVariable(expansionName, EnvironmentVariableTarget.Process));
            using var independent = OpenIndependentEnvironmentKey(machine, writable: true);
            using var otherScope = OpenIndependentEnvironmentKey(!machine, writable: false);
            Assert.AreSame(missing, independent.GetValue(name, missing));
            Assert.AreSame(missing, otherScope.GetValue(name, missing));
            using var adapter = WindowsEnvironmentVariablesEnvironment.Instance.OpenKey(
                machine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                machine ? MachineSubKey : "Environment",
                writable: true);
            Assert.IsNotNull(adapter);
            try
            {
                Environment.SetEnvironmentVariable(expansionName, @"C:\OwnedExpanded", EnvironmentVariableTarget.Process);
                adapter.SetValue(name, raw, kind);
                Assert.IsTrue(adapter.GetValueNames().Contains(name, StringComparer.Ordinal));
                Assert.AreEqual(raw, adapter.GetValue(name, missing, RegistryValueOptions.DoNotExpandEnvironmentNames));
                Assert.AreEqual(raw, independent.GetValue(name, missing, RegistryValueOptions.DoNotExpandEnvironmentNames));
                Assert.AreEqual(kind == RegistryValueKind.ExpandString ? expanded : raw, adapter.GetValue(name, missing, RegistryValueOptions.None));
                Assert.AreEqual(expanded, WindowsEnvironmentVariablesEnvironment.Instance.ExpandEnvironmentVariables(raw));
                Assert.AreEqual(kind, independent.GetValueKind(name));
                Assert.AreSame(missing, otherScope.GetValue(name, missing));

                adapter.DeleteValue(name, throwOnMissingValue: true);
                Assert.AreSame(missing, independent.GetValue(name, missing));
                adapter.DeleteValue(name, throwOnMissingValue: false);
                Assert.ThrowsExactly<ArgumentException>(() => adapter.DeleteValue(name, throwOnMissingValue: true));
            }
            finally
            {
                try
                {
                    independent.DeleteValue(name, throwOnMissingValue: false);
                }
                finally
                {
                    Environment.SetEnvironmentVariable(expansionName, null, EnvironmentVariableTarget.Process);
                }
            }
        }

        [TestMethod]
        [TestCategory("RequiresDisposableWindows")]
        [DataRow(false)]
        [DataRow(true)]
        public void ReadOnlyHandle_RejectsWriteWithoutCreatingValue(bool machine)
        {
            RequireDisposableGuest(machine);
            var name = $"PT_EV_{Guid.NewGuid():N}";
            var missing = new object();
            using var independent = OpenIndependentEnvironmentKey(machine, writable: true);
            Assert.AreSame(missing, independent.GetValue(name, missing));
            using var adapter = WindowsEnvironmentVariablesEnvironment.Instance.OpenKey(
                machine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                machine ? MachineSubKey : "Environment",
                writable: false);
            Assert.IsNotNull(adapter);
            try
            {
                Assert.ThrowsExactly<UnauthorizedAccessException>(() => adapter.SetValue(name, "rejected", RegistryValueKind.String));
                Assert.AreSame(missing, independent.GetValue(name, missing));
            }
            finally
            {
                independent.DeleteValue(name, throwOnMissingValue: false);
            }
        }

        [TestMethod]
        [TestCategory("HostSafeReadOnly")]
        [DataRow(false)]
        [DataRow(true)]
        public void MissingSubKey_ReturnsNullWithoutCreatingIt(bool machine)
        {
            var path = $@"Software\PowerToys_EV_{Guid.NewGuid():N}";
            using var adapter = WindowsEnvironmentVariablesEnvironment.Instance.OpenKey(
                machine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                path,
                writable: false);
            Assert.IsNull(adapter);
            using var independent = (machine ? Registry.LocalMachine : Registry.CurrentUser).OpenSubKey(path);
            Assert.IsNull(independent);
        }

        private void WithOwnedUserKey(Action<string, RegistryKey> assertions)
        {
            var subKey = $@"Software\PowerToys_EV_{Guid.NewGuid():N}";
            using (var existing = Registry.CurrentUser.OpenSubKey(subKey, writable: false))
            {
                Assert.IsNull(existing, "Refuse to reuse a pre-existing registry key.");
            }

            var independent = Registry.CurrentUser.CreateSubKey(subKey, writable: true);
            Assert.IsNotNull(independent);
            try
            {
                TestContext.WriteLine($"Owned registry fixture: HKCU\\{subKey}");
                assertions(subKey, independent);
            }
            finally
            {
                independent.Dispose();
                Registry.CurrentUser.DeleteSubKey(subKey, throwOnMissingSubKey: true);
                using var remaining = Registry.CurrentUser.OpenSubKey(subKey, writable: false);
                Assert.IsNull(remaining, "The exact owned registry key must be absent after cleanup.");
                TestContext.WriteLine($"Owned registry fixture deleted: HKCU\\{subKey}");
            }
        }

        private static RegistryKey OpenIndependentEnvironmentKey(bool machine, bool writable)
        {
            return (machine ? Registry.LocalMachine : Registry.CurrentUser)
                .OpenSubKey(machine ? MachineSubKey : "Environment", writable)
                ?? throw new InvalidOperationException("The disposable guest environment key is not provisioned.");
        }

        private static void RequireDisposableGuest(bool machine)
        {
            var expectedMachine = Environment.GetEnvironmentVariable(GuestMarker);
            if (string.IsNullOrWhiteSpace(expectedMachine)
                || !string.Equals(expectedMachine, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                Assert.Inconclusive("Real registry tests require explicit dispatch inside a prepared disposable Windows guest.");
            }

            if (machine)
            {
                using var identity = WindowsIdentity.GetCurrent();
                if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
                {
                    Assert.Inconclusive("Machine-scope adapter checks require the disposable guest administrator.");
                }
            }
        }
    }
}
