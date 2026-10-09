// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows.Forms;

using MouseWithoutBorders.Class;

[module: SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Scope = "member", Target = "MouseWithoutBorders.Common.#StartMouseWithoutBordersService()", Justification = "Dotnet port with style preservation")]

// <summary>
//     Service control code.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
namespace MouseWithoutBorders.Core;

internal static class Service
{
    private static bool shownErrMessage;
    private static DateTime lastStartServiceTime = DateTime.UtcNow;

    internal static void StartMouseWithoutBordersService(string desktopToRunMouseWithoutBordersOn = null, string startTag1 = "byapp", string startTag2 = null)
    {
        // NOTE(@yuyoyuppe): the new flow assumes we run both mwb processes directly from the svc.
        if (Common.RunWithNoAdminRight || true)
        {
            return;
        }

        Logger.Log($"{nameof(StartMouseWithoutBordersService)}: {Logger.GetStackTrace(new StackTrace())}.");

        Task task = Task.Run(() =>
        {
            Process[] ps = Process.GetProcessesByName("MouseWithoutBordersSvc");

            if (ps.Length != 0)
            {
                if (DateTime.UtcNow - lastStartServiceTime < TimeSpan.FromSeconds(5))
                {
                    Logger.Log($"{nameof(StartMouseWithoutBordersService)}: Aborted.");
                    return;
                }

                foreach (Process pp in ps)
                {
                    Logger.Log(string.Format(CultureInfo.InvariantCulture, "Killing process MouseWithoutBordersSvc {0}.", pp.Id));
                    pp.KillProcess();
                }
            }

            lastStartServiceTime = DateTime.UtcNow;
            ServiceController service = new("MouseWithoutBordersSvc");

            try
            {
                Logger.Log("Starting " + service.ServiceName);
            }
            catch (Exception)
            {
                if (!shownErrMessage)
                {
                    shownErrMessage = true;
                    _ = MessageBox.Show(
                        Application.ProductName + " is not installed yet, please run Setup.exe first!",
                        Application.ProductName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                return;
            }

            try
            {
                int c = 0;

                while (service.Status != ServiceControllerStatus.Stopped && c++ < 5)
                {
                    Thread.Sleep(1000);
                    service = new ServiceController("MouseWithoutBordersSvc");
                }

                if (string.IsNullOrWhiteSpace(desktopToRunMouseWithoutBordersOn))
                {
                    startTag2 ??= Process.GetCurrentProcess().SessionId.ToString(CultureInfo.InvariantCulture);
                    service.Start(new string[] { startTag1, startTag2 });
                }
                else
                {
                    service.Start(new string[] { desktopToRunMouseWithoutBordersOn });
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);

                // ERROR_SERVICE_ALREADY_RUNNING
                if (!(shownErrMessage || ((e?.InnerException as Win32Exception)?.NativeErrorCode == 1056)))
                {
                    shownErrMessage = true;
                    _ = MessageBox.Show(
                        "Cannot start service " + service.ServiceName + ": " + e.Message,
                        Common.BinaryName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                return;
            }
        });

        // Wait for the task while not blocking the UI thread.
        do
        {
            Common.MMSleep(1);

            if (task.IsCanceled || task.IsCompleted || task.IsFaulted)
            {
                break;
            }
        }
        while (true);
    }

    internal static void StartServiceAndSendLogoffSignal()
    {
        try
        {
            Process[] p = Process.GetProcessesByName("winlogon");
            Process me = Process.GetCurrentProcess();
            string myWinlogon = p?.FirstOrDefault(item => item.SessionId == me.SessionId)?.Id.ToString(CultureInfo.InvariantCulture) ?? null;

            if (string.IsNullOrWhiteSpace(myWinlogon))
            {
                StartMouseWithoutBordersService(null, "logoff");
            }
            else
            {
                StartMouseWithoutBordersService(null, "logoff", myWinlogon);
            }
        }
        catch (Exception e)
        {
            Logger.Log($"{nameof(StartServiceAndSendLogoffSignal)}: {e.Message}");
        }
    }
}
