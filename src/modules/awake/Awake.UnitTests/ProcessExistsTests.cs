// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Awake.Core;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
public class ProcessExistsTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void ProcessExists_InvalidPid_ReturnsFalse(int pid)
    {
        Assert.IsFalse(InvokeProcessExists(pid));
    }

    [TestMethod]
    public void ProcessExists_CurrentProcess_ReturnsTrue()
    {
        Assert.IsTrue(InvokeProcessExists(Environment.ProcessId));
    }

    [TestMethod]
    public async Task ProcessExists_InaccessibleSystemProcess_DefersToMonitorFailure()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Requires the Windows System process.");
        }

        try
        {
            using var process = Process.GetProcessById(4);
            _ = process.HasExited;
            Assert.Inconclusive("The System process is queryable in this environment.");
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 5)
        {
        }

        Assert.IsTrue(InvokeProcessExists(4), "An inaccessible process must not be reported as missing.");

        var result = new TaskCompletionSource<Win32Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        RunnerHelper.WaitForPowerToysRunner(
            4,
            () => result.TrySetResult(null),
            error => result.TrySetResult(error),
            nameof(ProcessExists_InaccessibleSystemProcess_DefersToMonitorFailure));

        var failure = await result.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsNotNull(failure, "The monitor must invoke its failure callback, not its exit callback.");
        Assert.AreEqual(5, failure.NativeErrorCode);
    }

    private static bool InvokeProcessExists(int pid)
    {
        var program = typeof(Manager).Assembly.GetType("Awake.Program", throwOnError: true)!;
        var method = program.GetMethod("ProcessExists", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (bool)method.Invoke(null, new object[] { pid })!;
    }
}
