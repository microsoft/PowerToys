// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;

using MouseWithoutBorders.Class;

// <summary>
//     Impersonation.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
namespace MouseWithoutBorders.Core;

internal static class Launch
{
    private static bool RunElevated()
    {
        return WindowsIdentity.GetCurrent().Owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
    }

    internal static bool ImpersonateLoggedOnUserAndDoSomething(Action targetFunc)
    {
        if (Common.RunWithNoAdminRight)
        {
            targetFunc();
            return true;
        }
        else
        {
            // SuppressFlow fixes an issue on service mode, where WTSQueryUserToken runs successfully once and then fails
            // on subsequent calls. The reason appears to be an unknown issue with reverting the impersonation,
            // meaning that subsequent impersonation attempts run as the logged-on user and fail.
            // This is a workaround.
            using var asyncFlowControl = System.Threading.ExecutionContext.SuppressFlow();

            uint dwSessionId;
            IntPtr hUserToken = IntPtr.Zero, hUserTokenDup = IntPtr.Zero;
            try
            {
                dwSessionId = (uint)Process.GetCurrentProcess().SessionId;
                uint rv = NativeMethods.WTSQueryUserToken(dwSessionId, ref hUserToken);
                var lastError = rv == 0 ? Marshal.GetLastWin32Error() : 0;

                Logger.LogDebug($"{nameof(NativeMethods.WTSQueryUserToken)} returned {rv.ToString(CultureInfo.CurrentCulture)}");

                if (rv == 0)
                {
                    Logger.Log($"{nameof(NativeMethods.WTSQueryUserToken)} failed with: {lastError}.");
                    return false;
                }

                if (!NativeMethods.DuplicateToken(hUserToken, (int)NativeMethods.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, ref hUserTokenDup))
                {
                    Logger.TelemetryLogTrace($"{nameof(NativeMethods.DuplicateToken)} Failed! {Logger.GetStackTrace(new StackTrace())}", SeverityLevel.Warning);
                    _ = NativeMethods.CloseHandle(hUserToken);
                    _ = NativeMethods.CloseHandle(hUserTokenDup);
                    return false;
                }

                if (NativeMethods.ImpersonateLoggedOnUser(hUserTokenDup))
                {
                    targetFunc();
                    _ = NativeMethods.RevertToSelf();
                    _ = NativeMethods.CloseHandle(hUserToken);
                    _ = NativeMethods.CloseHandle(hUserTokenDup);
                    return true;
                }
                else
                {
                    Logger.Log("ImpersonateLoggedOnUser Failed!");
                    _ = NativeMethods.CloseHandle(hUserToken);
                    _ = NativeMethods.CloseHandle(hUserTokenDup);
                    return false;
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
                return false;
            }
        }
    }

    internal static int CreateProcessInInputDesktopSession(string commandLine, string arg, string desktop, short wShowWindow, bool lowIntegrity = false)

    // As user who runs explorer.exe
    {
        if (!Program.User.Contains("system", StringComparison.InvariantCultureIgnoreCase))
        {
            ProcessStartInfo s = new(commandLine, arg);
            s.WindowStyle = wShowWindow != 0 ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden;
            Process p = Process.Start(s);

            return p == null ? 0 : p.Id;
        }

        string commandLineWithArg = commandLine + " " + arg;
        int lastError;
        int dwSessionId;
        IntPtr hUserToken = IntPtr.Zero, hUserTokenDup = IntPtr.Zero;

        Logger.LogDebug("CreateProcessInInputDesktopSession called, launching " + commandLineWithArg + " on " + desktop);

        try
        {
            dwSessionId = Process.GetCurrentProcess().SessionId;

            // Get the user token used by DuplicateTokenEx
            lastError = (int)NativeMethods.WTSQueryUserToken((uint)dwSessionId, ref hUserToken);

            NativeMethods.STARTUPINFO si = default;
            si.cb = Marshal.SizeOf(si);
            si.lpDesktop = "winsta0\\" + desktop;
            si.wShowWindow = wShowWindow;

            NativeMethods.SECURITY_ATTRIBUTES sa = default;
            sa.Length = Marshal.SizeOf(sa);

            if (!NativeMethods.DuplicateTokenEx(hUserToken, NativeMethods.MAXIMUM_ALLOWED, ref sa, (int)NativeMethods.SECURITY_IMPERSONATION_LEVEL.SecurityIdentification, (int)NativeMethods.TOKEN_TYPE.TokenPrimary, ref hUserTokenDup))
            {
                lastError = Marshal.GetLastWin32Error();
                Logger.Log(string.Format(CultureInfo.CurrentCulture, "DuplicateTokenEx error: {0} Token does not have the privilege.", lastError));
                _ = NativeMethods.CloseHandle(hUserToken);
                return 0;
            }

            if (lowIntegrity)
            {
                NativeMethods.TOKEN_MANDATORY_LABEL tIL;

                // Low
                string sIntegritySid = "S-1-16-4096";

                bool rv = NativeMethods.ConvertStringSidToSid(sIntegritySid, out IntPtr pIntegritySid);

                if (!rv)
                {
                    Logger.Log("ConvertStringSidToSid failed");
                    _ = NativeMethods.CloseHandle(hUserToken);
                    _ = NativeMethods.CloseHandle(hUserTokenDup);
                    return 0;
                }

                tIL.Label.Attributes = NativeMethods.SE_GROUP_INTEGRITY;
                tIL.Label.Sid = pIntegritySid;

                rv = NativeMethods.SetTokenInformation(hUserTokenDup, NativeMethods.TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, ref tIL, (uint)Marshal.SizeOf(tIL) + (uint)IntPtr.Size);

                if (!rv)
                {
                    Logger.Log("SetTokenInformation failed");
                    _ = NativeMethods.CloseHandle(hUserToken);
                    _ = NativeMethods.CloseHandle(hUserTokenDup);
                    return 0;
                }
            }

            uint dwCreationFlags = NativeMethods.NORMAL_PRIORITY_CLASS | NativeMethods.CREATE_NEW_CONSOLE;
            IntPtr pEnv = IntPtr.Zero;

            if (NativeMethods.CreateEnvironmentBlock(ref pEnv, hUserTokenDup, true))
            {
                dwCreationFlags |= NativeMethods.CREATE_UNICODE_ENVIRONMENT;
            }
            else
            {
                pEnv = IntPtr.Zero;
            }

            _ = NativeMethods.CreateProcessAsUser(
                hUserTokenDup,        // client's access token
                null,                   // file to execute
                commandLineWithArg,     // command line
                ref sa,                 // pointer to process SECURITY_ATTRIBUTES
                ref sa,                 // pointer to thread SECURITY_ATTRIBUTES
                false,                  // handles are not inheritable
                (int)dwCreationFlags,   // creation flags
                pEnv,                   // pointer to new environment block
                null,                   // name of current directory
                ref si,                 // pointer to STARTUPINFO structure
                out NativeMethods.PROCESS_INFORMATION pi); // receives information about new process

            // GetLastError should be 0
            int iResultOfCreateProcessAsUser = Marshal.GetLastWin32Error();
            Logger.LogDebug("CreateProcessAsUser returned " + iResultOfCreateProcessAsUser.ToString(CultureInfo.CurrentCulture));

            // Close handles task
            _ = NativeMethods.CloseHandle(hUserToken);
            _ = NativeMethods.CloseHandle(hUserTokenDup);

            return (iResultOfCreateProcessAsUser == 0) ? (int)pi.dwProcessId : 0;
        }
        catch (Exception e)
        {
            Logger.Log(e);
            return 0;
        }
    }

#if CUSTOMIZE_LOGON_SCREEN
    internal static bool CreateLowIntegrityProcess(string commandLine, string args, int wait, bool killIfTimedOut, long limitedMem, short wShowWindow = 0)
    {
        int processId = CreateProcessInInputDesktopSession(commandLine, args, "default", wShowWindow, true);

        if (processId <= 0)
        {
            return false;
        }

        if (wait > 0)
        {
            if (limitedMem > 0)
            {
                int sec = 0;
                while (true)
                {
                    Process p;

                    try
                    {
                        if ((p = Process.GetProcessById(processId)) == null)
                        {
                            Logger.Log("Process exited!");
                            break;
                        }
                    }
                    catch (ArgumentException)
                    {
                        Logger.Log("GetProcessById.ArgumentException");
                        break;
                    }

                    if ((!p.HasExited && p.PrivateMemorySize64 > limitedMem) || (++sec > (wait / 1000)))
                    {
                        Logger.Log(string.Format(CultureInfo.CurrentCulture, "Process log (mem): {0}, {1}", sec, p.PrivateMemorySize64));
                        return false;
                    }

                    Thread.Sleep(1000);
                }
            }
            else
            {
                Process p;

                if ((p = Process.GetProcessById(processId)) == null)
                {
                    Logger.Log("Process exited!");
                }
                else if (NativeMethods.WaitForSingleObject(p.Handle, wait) != NativeMethods.WAIT_OBJECT_0 && killIfTimedOut)
                {
                    Logger.Log("Process log (time).");
                    TerminateProcessTree(p.Handle, (uint)processId, -1);
                    return false;
                }
            }
        }

        return true;
    }

    private static void TerminateProcessTree(IntPtr hProcess, uint processID, int exitCode)
    {
        if (processID > 0 && hProcess.ToInt32() > 0)
        {
            Process[] processes = Process.GetProcesses();
            int dwSessionId = Process.GetCurrentProcess().SessionId;

            foreach (Process p in processes)
            {
                if (p.SessionId == dwSessionId)
                {
                    NativeMethods.PROCESS_BASIC_INFORMATION processBasicInformation = default;

                    try
                    {
                        if (NativeMethods.NtQueryInformationProcess(p.Handle, 0, ref processBasicInformation, (uint)Marshal.SizeOf(processBasicInformation), out uint bytesWritten) >= 0)
                        {// NT_SUCCESS(...)
                            if (processBasicInformation.InheritedFromUniqueProcessId == processID)
                            {
                                TerminateProcessTree(p.Handle, processBasicInformation.UniqueProcessId, exitCode);
                            }
                        }
                    }
                    catch (InvalidOperationException e)
                    {
                        Logger.Log(e);
                        continue;
                    }
                    catch (Win32Exception e)
                    {
                        Logger.Log(e);
                        continue;
                    }
                }
            }

            _ = NativeMethods.TerminateProcess(hProcess, (IntPtr)exitCode);
        }
    }
#endif
}
