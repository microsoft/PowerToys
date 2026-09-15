// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using PowerToys.FileLocksmithUI.Services;

namespace PowerToys.FileLocksmithUI.UnitTests
{
    [TestClass]
    public sealed class FileLocksmithQueryServiceTests
    {
        private static readonly string[] SelectedPaths = [@"C:\file.txt"];

        [TestMethod]
        public async Task FindProcessesAsyncReturnsWorkerResults()
        {
            const string output = """
                {"processes":[{"pid":123,"name":"process.exe","user":"user","files":["C:\\file.txt"]}]}
                """;
            var service = CreateService($"[Console]::Out.Write('{output}')");

            var result = await service.FindProcessesAsync(SelectedPaths, CancellationToken.None);

            Assert.AreEqual(FileLocksmithQueryStatus.Success, result.Status);
            Assert.HasCount(1, result.Processes);
            Assert.AreEqual(123U, result.Processes[0].Pid);
            Assert.AreEqual("process.exe", result.Processes[0].Name);
            Assert.AreEqual(@"C:\file.txt", result.Processes[0].Files[0]);
        }

        [TestMethod]
        public async Task FindProcessesAsyncReportsMalformedWorkerOutput()
        {
            var service = CreateService("[Console]::Out.Write('not-json')");

            var result = await service.FindProcessesAsync(SelectedPaths, CancellationToken.None);

            Assert.AreEqual(FileLocksmithQueryStatus.MalformedOutput, result.Status);
            Assert.IsEmpty(result.Processes);
        }

        [TestMethod]
        public async Task FindProcessesAsyncReportsFailedWorkerExitCode()
        {
            var service = CreateService("exit 17");

            var result = await service.FindProcessesAsync(SelectedPaths, CancellationToken.None);

            Assert.AreEqual(FileLocksmithQueryStatus.Failed, result.Status);
            Assert.AreEqual(17, result.ExitCode);
            Assert.IsEmpty(result.Processes);
        }

        [TestMethod]
        public async Task FindProcessesAsyncTimeoutTerminatesWorker()
        {
            var workerPid = 0;
            var service = CreateService(
                "Start-Sleep -Seconds 30",
                TimeSpan.FromMilliseconds(500),
                pid => workerPid = pid);

            var result = await service.FindProcessesAsync(SelectedPaths, CancellationToken.None);

            Assert.AreEqual(FileLocksmithQueryStatus.TimedOut, result.Status);
            Assert.AreNotEqual(0, workerPid);
            Assert.IsFalse(IsProcessRunning(workerPid), "The timed-out worker process was left running.");
        }

        [TestMethod]
        public async Task FindProcessesAsyncUsesBomFreeUtf8WithRealWorker()
        {
            var testPath = Path.Combine(Path.GetTempPath(), $"File Locksmith \u6d4b\u8bd5 {Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(testPath, string.Empty);

            try
            {
                var workerPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "FileLocksmithCLI.exe"));
                Assert.IsTrue(File.Exists(workerPath), $"The File Locksmith worker was not built at {workerPath}.");
                var service = new FileLocksmithQueryService(
                    () => FileLocksmithQueryService.CreateWorkerStartInfo(workerPath),
                    TimeSpan.FromMinutes(2),
                    null);

                var result = await service.FindProcessesAsync(new[] { testPath }, CancellationToken.None);

                Assert.AreEqual(FileLocksmithQueryStatus.Success, result.Status);
            }
            finally
            {
                File.Delete(testPath);
            }
        }

        private static FileLocksmithQueryService CreateService(
            string script,
            TimeSpan? timeout = null,
            Action<int>? processStarted = null)
        {
            return new FileLocksmithQueryService(
                () =>
                {
                    var startInfo = new ProcessStartInfo("powershell.exe")
                    {
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                    };
                    startInfo.ArgumentList.Add("-NoLogo");
                    startInfo.ArgumentList.Add("-NoProfile");
                    startInfo.ArgumentList.Add("-NonInteractive");
                    startInfo.ArgumentList.Add("-Command");
                    startInfo.ArgumentList.Add($"$null = [Console]::In.ReadToEnd(); {script}");
                    return startInfo;
                },
                timeout ?? TimeSpan.FromSeconds(10),
                processStarted);
        }

        private static bool IsProcessRunning(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
