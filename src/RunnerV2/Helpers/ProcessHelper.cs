// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace RunnerV2.Helpers
{
    internal static class ProcessHelper
    {
        internal static void ScheudleProcessKill(string processName, int msDelay = 500)
        {
            new Thread(async () =>
            {
                Process[] processes = Process.GetProcessesByName(processName);

                await Task.Delay(msDelay);
                foreach (var process in processes)
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
            }).Start();
        }
    }
}
