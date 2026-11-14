// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;

namespace EnvironmentVariablesUILib.UnitTests.Helpers;

[TestClass]
public class HostServicesTests
{
    private sealed class FakeElevationHelper : IElevationHelper
    {
        public bool IsElevated => false;
    }

    [TestMethod]
    public void Initialize_WithoutLoggerOrTelemetry_UsesNoOpServices()
    {
        HostServices.Initialize(new FakeElevationHelper(), null!, null!);

        LoggerInstance.Logger.LogError("message", new System.InvalidOperationException());
        TelemetryInstance.Telemetry.LogEnvironmentVariablesVariableChangedEvent(VariablesSetType.User);
    }

    [TestMethod]
    public void Initialize_WithoutElevationHelper_UsesProcessElevation()
    {
        HostServices.Initialize(null!, null!, null!);

        Assert.IsInstanceOfType<ElevationHelper>(ElevationHelper.ElevationHelperInstance);
    }

    [TestMethod]
    public void SystemVariable_IsReadOnlyWhenHostIsNotElevated()
    {
        HostServices.Initialize(new FakeElevationHelper(), null!, null!);

        Assert.IsFalse(new Variable("Name", "Value", VariablesSetType.System).IsEditable);
        Assert.IsTrue(new Variable("Name", "Value", VariablesSetType.User).IsEditable);
    }
}
