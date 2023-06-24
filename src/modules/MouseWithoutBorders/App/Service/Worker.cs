// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MouseWithoutBordersService
{
    internal sealed class Worker : IHostedService
    {
        private readonly ILogger<Worker> _logger;
        private readonly Action<ILogger, string, Exception> _infoLogger;
        private readonly string processName = "PowerToys.MouseWithoutBorders";
        private readonly IHostApplicationLifetime _lifetime;

        private string[] _cmdArgs;
        private int appSessionId = -1;
        private string myBinary = Assembly.GetExecutingAssembly().Location;

        public Worker(ILogger<Worker> logger, IHostEnvironment environment, IHostApplicationLifetime lifeTime)
        {
            _cmdArgs = CmdArgs.Value;
            _lifetime = lifeTime;
            _logger = logger;
            _infoLogger = LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(0, "Info"),
                "{Message}");
        }

        private void Log(string message)
        {
            _infoLogger(_logger, message, null);
        }

        private void LogDebug(string message)
        {
#if DEBUG
            Log(message);
#endif
        }

        private int RunMeUnderSystemAccount(int noOfTry, string activeDesktop, string userLocalAppDataPath)
        {
            int rv = 0;

            try
            {
                string me = "\"" + Path.GetDirectoryName(myBinary) + "\\" + processName + "\"";
                int waitCount = 20;

                while (NativeMethods.WTSGetActiveConsoleSessionId() == 0xFFFFFFFF && waitCount > 0)
                {
                    waitCount--;
                    LogDebug("The session is detached/attached.");
                    Thread.Sleep(500);
                }

                LogDebug("====================");
                if (activeDesktop != null)
                {
                    LogDebug($"Executing {me} on [{activeDesktop}], {NativeMethods.WTSGetActiveConsoleSessionId()}");
                    rv += NativeMethods.CreateProcessAsSystemAccountOnSpecificDesktop(me + " \"" + activeDesktop + "\"" + userLocalAppDataPath, activeDesktop, noOfTry) ? 1 : 0;
                }
                else
                {
                    LogDebug($"Executing {me} winlogon, {NativeMethods.WTSGetActiveConsoleSessionId()}");
                    rv += NativeMethods.CreateProcessAsSystemAccountOnSpecificDesktop(me + " \"winlogon\" " + userLocalAppDataPath, "winlogon", noOfTry) ? 1 : 0;
                    LogDebug("====================");

                    // BEGIN: This may happen in some slow machine, this is a tentative fix
                    // http://social.msdn.microsoft.com/Forums/en-CA/clr/thread/9f00bdf0-3ea7-4a1f-b5a7-9b5bbc009888
                    Thread.Sleep(1000);

                    // END
                    LogDebug($"Executing {me} default, {NativeMethods.WTSGetActiveConsoleSessionId()}");
                    rv += NativeMethods.CreateProcessAsSystemAccountOnSpecificDesktop(me + " \"default\" " + userLocalAppDataPath, "default", noOfTry) ? 1 : 0;

                    if (appSessionId >= 0 && appSessionId != NativeMethods.WTSGetActiveConsoleSessionId())
                    {
                        Thread.Sleep(1000);
                        LogDebug($"Executing {me} default, {(appSessionId >= 0 ? appSessionId : (int)NativeMethods.WTSGetActiveConsoleSessionId())}");
                        rv += NativeMethods.CreateProcessAsSystemAccountOnSpecificDesktop(me + " \"default\" " + userLocalAppDataPath, "default", noOfTry, appSessionId) ? 1 : 0;

                        Thread.Sleep(1000);
                        LogDebug("Executing " + me + " \"winlogon\" on session " + appSessionId.ToString(CultureInfo.InvariantCulture));
                        rv += NativeMethods.CreateProcessAsSystemAccountOnSpecificDesktop(me + " \"winlogon\" " + userLocalAppDataPath, "winlogon", noOfTry, appSessionId) ? 1 : 0;
                        LogDebug("====================");
                    }
                }

                LogDebug("====================");
            }
            catch (Exception e)
            {
                Log($"Exception: {e.Message}");
            }

            return rv;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var args = _cmdArgs;
            string userLocalAppDataPath = args.Length > 1 && args[1] != null ? args[1].Trim() : null;

            int noOfTry = 30;
            bool isLoggingOff = false;
            bool isByWindows = false;
            string activeDesktop = null;
            int successRunCount, expectedRunCount, processCount;

            try
            {
                successRunCount = RunMeUnderSystemAccount(noOfTry, activeDesktop, userLocalAppDataPath);

                Process[] p = Process.GetProcessesByName(processName);
                processCount = p != null ? p.Length : 0;
                expectedRunCount = 2;
                LogDebug($"successRunCount = {successRunCount}, processCount = {processCount}");

                if (isLoggingOff || isByWindows)
                {
                    int c = 0;

                    while ((successRunCount < expectedRunCount || processCount < expectedRunCount) && c++ < 30)
                    {
                        Thread.Sleep(5000);
                        successRunCount = RunMeUnderSystemAccount(noOfTry, activeDesktop, userLocalAppDataPath);
                        Thread.Sleep(5000);
                        p = Process.GetProcessesByName(processName);
                        processCount = p != null ? p.Length : 0;

                        LogDebug($"successRunCount(2) = {successRunCount}, processCount = {processCount}");
                    }

                    Thread.Sleep(30000);

                    p = Process.GetProcessesByName(processName);
                    if (p == null || p.Length < 2)
                    {
                        LogDebug("Found none or one process, one more try...");
                        successRunCount = RunMeUnderSystemAccount(noOfTry, activeDesktop, userLocalAppDataPath);
                        Thread.Sleep(5000);
                        p = Process.GetProcessesByName(processName);
                        processCount = p != null ? p.Length : 0;
                        LogDebug($"successRunCount(3) = {successRunCount}, processCount = {processCount}");
                    }
                }
                else
                {
                    RunMeUnderSystemAccount(noOfTry, null, userLocalAppDataPath);
                }
            }
            catch (Exception ex)
            {
                Log($"Exception: {ex.Message}");
                Thread.Sleep(1000);
            }

            CmdArgs.StopServiceDelegate();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
