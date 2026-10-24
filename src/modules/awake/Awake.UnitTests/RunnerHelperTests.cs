// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
public class RunnerHelperTests
{
    [TestMethod]
    public async Task WaitForPowerToysRunner_AlreadyExitedProcess_Completes()
    {
        int pid;
        using (var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 0") { CreateNoWindow = true, UseShellExecute = false })!)
        {
            pid = process.Id;
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(2000))
                    {
                        process.Kill();
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
                    }
                }
            }
        }

        var result = new TaskCompletionSource<Win32Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        RunnerHelper.WaitForPowerToysRunner(pid, () => result.TrySetResult(null), error => result.TrySetResult(error), nameof(WaitForPowerToysRunner_AlreadyExitedProcess_Completes));
        Assert.IsNull(await result.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [TestMethod]
    public async Task WaitForPowerToysRunner_SystemProcessAccessDenied_DoesNotComplete()
    {
        var result = new TaskCompletionSource<Win32Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        RunnerHelper.WaitForPowerToysRunner(4, () => result.TrySetResult(null), error => result.TrySetResult(error), nameof(WaitForPowerToysRunner_SystemProcessAccessDenied_DoesNotComplete));
        Win32Exception? failure = await result.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsNotNull(failure);
        Assert.AreEqual(5, failure.NativeErrorCode);
    }
}
