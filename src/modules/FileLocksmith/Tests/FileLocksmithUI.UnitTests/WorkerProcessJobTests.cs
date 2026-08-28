// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;

using PowerToys.FileLocksmithUI.Services;

namespace PowerToys.FileLocksmithUI.UnitTests
{
    [TestClass]
    public sealed class WorkerProcessJobTests
    {
        [TestMethod]
        public async Task DisposeTerminatesAssignedWorker()
        {
            using var process = Process.Start(new ProcessStartInfo("powershell.exe")
            {
                Arguments = "-NoLogo -NoProfile -NonInteractive -Command \"Start-Sleep -Seconds 30\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            Assert.IsNotNull(process);

            using (var job = WorkerProcessJob.Create())
            {
                job.Assign(process);
            }

            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(process.HasExited, "Closing the worker job did not terminate its process.");
        }
    }
}
