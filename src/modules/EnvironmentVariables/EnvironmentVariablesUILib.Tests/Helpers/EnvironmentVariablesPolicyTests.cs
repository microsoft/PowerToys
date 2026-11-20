// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using EnvironmentVariablesUILib.Helpers;
using Microsoft.Win32;

namespace EnvironmentVariablesUILib.UnitTests.Helpers;

[TestClass]
public class EnvironmentVariablesPolicyTests
{
    private const string Utility = EnvironmentVariablesPolicy.UtilityValueName;
    private const string Global = EnvironmentVariablesPolicy.GlobalValueName;

    private static bool IsDisabled(params (RegistryHive Hive, string Name, object Value)[] values)
    {
        var map = new Dictionary<(RegistryHive, string), object>();
        foreach (var (hive, name, value) in values)
        {
            map[(hive, name)] = value;
        }

        return EnvironmentVariablesPolicy.IsDisabled((hive, name) => map.TryGetValue((hive, name), out var value) ? value : null!);
    }

    [TestMethod]
    public void NotConfigured_IsNotDisabled()
    {
        Assert.IsFalse(IsDisabled());
    }

    [TestMethod]
    public void UtilityDisabled_IsDisabled()
    {
        Assert.IsTrue(IsDisabled((RegistryHive.CurrentUser, Utility, 0)));
    }

    [TestMethod]
    public void GlobalDisabled_AppliesWhenUtilityNotConfigured()
    {
        Assert.IsTrue(IsDisabled((RegistryHive.LocalMachine, Global, 0)));
    }

    [TestMethod]
    public void UtilityEnabled_OverridesGlobalDisabled()
    {
        Assert.IsFalse(IsDisabled((RegistryHive.CurrentUser, Utility, 1), (RegistryHive.LocalMachine, Global, 0)));
    }

    [TestMethod]
    public void MachineValue_TakesPrecedenceOverUser()
    {
        Assert.IsFalse(IsDisabled((RegistryHive.LocalMachine, Utility, 1), (RegistryHive.CurrentUser, Utility, 0)));
        Assert.IsTrue(IsDisabled((RegistryHive.LocalMachine, Utility, 0), (RegistryHive.CurrentUser, Utility, 1)));
    }

    [TestMethod]
    public void InvalidUtilityValue_FallsBackToGlobal()
    {
        Assert.IsTrue(IsDisabled((RegistryHive.LocalMachine, Utility, 2), (RegistryHive.CurrentUser, Global, 0)));
        Assert.IsTrue(IsDisabled((RegistryHive.LocalMachine, Utility, "0"), (RegistryHive.CurrentUser, Global, 0)));
    }

    [TestMethod]
    public void InvalidMachineValue_DoesNotFallBackToUserValue()
    {
        Assert.IsFalse(IsDisabled((RegistryHive.LocalMachine, Utility, 2), (RegistryHive.CurrentUser, Utility, 0)));
    }
}
