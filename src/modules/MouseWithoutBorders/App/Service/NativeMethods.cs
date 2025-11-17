// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security;
using System.ServiceProcess;

namespace MouseWithoutBordersService
{
    internal sealed class NativeMethods
    {
        private NativeMethods()
        {
        }

        private const string MY_KEY = @"SOFTWARE\Microsoft\MouseWithoutBorders";
        private const string MY_KEY_EX = @"S-1-5-19\SOFTWARE\Microsoft\MouseWithoutBorders";

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            internal int Length;
            internal IntPtr lpSecurityDescriptor;
            internal bool bInheritHandle;
        }

        private enum TOKEN_TYPE : int
        {
            TokenPrimary = 1,
            TokenImpersonation = 2,
        }

        private enum TOKEN_INFORMATION_CLASS : int
        {
            TokenUser = 1,
            TokenGroups,
            TokenPrivileges,
            TokenOwner,
            TokenPrimaryGroup,
            TokenDefaultDacl,
            TokenSource,
            TokenType,
            TokenImpersonationLevel,
            TokenStatistics,
            TokenRestrictedSids,
            TokenSessionId,
            TokenGroupsAndPrivileges,
            TokenSessionReference,
            TokenSandBoxInert,
            TokenAuditPolicy,
            TokenOrigin,
            MaxTokenInfoClass,  // MaxTokenInfoClass should always be the last enum
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            internal int cb;
            internal string lpReserved;
            internal string lpDesktop;
            internal string lpTitle;
            internal uint dwX;
            internal uint dwY;
            internal uint dwXSize;
            internal uint dwYSize;
            internal uint dwXCountChars;
            internal uint dwYCountChars;
            internal uint dwFillAttribute;
            internal uint dwFlags;
            internal short wShowWindow;
            internal short cbReserved2;
            internal IntPtr lpReserved2;
            internal IntPtr hStdInput;
            internal IntPtr hStdOutput;
            internal IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SID_AND_ATTRIBUTES
        {
            internal IntPtr Sid;
            internal int Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_MANDATORY_LABEL
        {
            internal SID_AND_ATTRIBUTES Label;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
        {
            internal IntPtr hProcess;
            internal IntPtr hThread;
            internal uint dwProcessId;
            internal uint dwThreadId;
        }

        private enum SECURITY_IMPERSONATION_LEVEL : int
        {
            SecurityAnonymous = 0,
            SecurityIdentification = 1,
            SecurityImpersonation = 2,
            SecurityDelegation = 3,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct LUID
        {
            internal int LowPart;
            internal int HighPart;
        }// end struct

        [StructLayout(LayoutKind.Sequential)]
        internal struct LUID_AND_ATTRIBUTES
        {
            internal LUID Luid;
            internal int Attributes;
        }// end struct

        [StructLayout(LayoutKind.Sequential)]
        internal struct TOKEN_PRIVILEGES
        {
            internal int PrivilegeCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            internal int[] Privileges;
        }

        private const int READ_CONTROL = 0x00020000;

        private const int STANDARD_RIGHTS_REQUIRED = 0x000F0000;

        private const int STANDARD_RIGHTS_READ = READ_CONTROL;
        private const int STANDARD_RIGHTS_WRITE = READ_CONTROL;
        private const int STANDARD_RIGHTS_EXECUTE = READ_CONTROL;

        private const int STANDARD_RIGHTS_ALL = 0x001F0000;

        private const int SPECIFIC_RIGHTS_ALL = 0x0000FFFF;

        private const int TOKEN_ASSIGN_PRIMARY = 0x0001;
        private const int TOKEN_DUPLICATE = 0x0002;
        private const int TOKEN_IMPERSONATE = 0x0004;
        private const int TOKEN_QUERY = 0x0008;
        private const int TOKEN_QUERY_SOURCE = 0x0010;
        private const int TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const int TOKEN_ADJUST_GROUPS = 0x0040;
        private const int TOKEN_ADJUST_DEFAULT = 0x0080;
        private const int TOKEN_ADJUST_SESSIONID = 0x0100;

        private const int TOKEN_ALL_ACCESS_P = STANDARD_RIGHTS_REQUIRED |
                                      TOKEN_ASSIGN_PRIMARY |
                                      TOKEN_DUPLICATE |
                                      TOKEN_IMPERSONATE |
                                      TOKEN_QUERY |
                                      TOKEN_QUERY_SOURCE |
                                      TOKEN_ADJUST_PRIVILEGES |
                                      TOKEN_ADJUST_GROUPS |
                                      TOKEN_ADJUST_DEFAULT;

        private const int TOKEN_ALL_ACCESS = TOKEN_ALL_ACCESS_P | TOKEN_ADJUST_SESSIONID;

        private const int TOKEN_READ = STANDARD_RIGHTS_READ | TOKEN_QUERY;

        private const int TOKEN_WRITE = STANDARD_RIGHTS_WRITE |
                                      TOKEN_ADJUST_PRIVILEGES |
                                      TOKEN_ADJUST_GROUPS |
                                      TOKEN_ADJUST_DEFAULT;

        private const int TOKEN_EXECUTE = STANDARD_RIGHTS_EXECUTE;

        private const uint MAXIMUM_ALLOWED = 0x2000000;

        private const int CREATE_NEW_PROCESS_GROUP = 0x00000200;
        private const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;

        private const int IDLE_PRIORITY_CLASS = 0x40;
        private const int NORMAL_PRIORITY_CLASS = 0x20;
        private const int HIGH_PRIORITY_CLASS = 0x80;
        private const int REALTIME_PRIORITY_CLASS = 0x100;

        private const int CREATE_NEW_CONSOLE = 0x00000010;

        private const string SE_DEBUG_NAME = "SeDebugPrivilege";
        private const string SE_RESTORE_NAME = "SeRestorePrivilege";
        private const string SE_BACKUP_NAME = "SeBackupPrivilege";

        private const int SE_PRIVILEGE_ENABLED = 0x0002;

        private const int ERROR_NOT_ALL_ASSIGNED = 1300;

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESSENTRY32
        {
            internal uint dwSize;
            internal uint cntUsage;
            internal uint th32ProcessID;
            internal IntPtr th32DefaultHeapID;
            internal uint th32ModuleID;
            internal uint cntThreads;
            internal uint th32ParentProcessID;
            internal int pcPriClassBase;
            internal uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            internal string szExeFile;
        }

        private const uint TH32CS_SNAPPROCESS = 0x00000002;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hSnapshot);

        [DllImport("kernel32.dll")]
        internal static extern uint WTSGetActiveConsoleSessionId();

        [SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "1", Justification = "Dotnet port with style preservation")]
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(IntPtr lpSystemName, string lpname, [MarshalAs(UnmanagedType.Struct)] ref LUID lpLuid);

        [SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", Justification = "Dotnet port with style preservation")]
        [DllImport("advapi32.dll", EntryPoint = "CreateProcessAsUser", SetLastError = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcessAsUser(IntPtr hToken, string lpApplicationName, string lpCommandLine, ref SECURITY_ATTRIBUTES lpProcessAttributes, ref SECURITY_ATTRIBUTES lpThreadAttributes, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("advapi32.dll", EntryPoint = "DuplicateTokenEx")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DuplicateTokenEx(IntPtr ExistingTokenHandle, uint dwDesiredAccess, ref SECURITY_ATTRIBUTES lpThreadAttributes, int TokenType, int ImpersonationLevel, ref IntPtr DuplicateTokenHandle);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

        [DllImport("advapi32", SetLastError = true)]
        [SuppressUnmanagedCodeSecurityAttribute]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(
            IntPtr ProcessHandle, // handle to process
            int DesiredAccess, // desired access to process
            ref IntPtr TokenHandle); // handle to open access token

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateEnvironmentBlock(ref IntPtr lpEnvironment, IntPtr hToken, [MarshalAs(UnmanagedType.Bool)] bool bInherit);

        [DllImport("sas.dll", SetLastError = true)]
        internal static extern IntPtr SendSAS([MarshalAs(UnmanagedType.Bool)] bool AsUser);

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Dotnet port with style preservation")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static bool CreateProcessAsSystemAccountOnSpecificDesktop(string CommandLine, string Desktop, int NoOfTry, int sessionId = -1)
        {
            int lastError;
            int dwSessionId = sessionId < 0 ? (int)WTSGetActiveConsoleSessionId() : sessionId;
            int winlogonPid = 0;
            IntPtr hUserTokenDup = IntPtr.Zero, hPToken = IntPtr.Zero, hProcess = IntPtr.Zero;

            try
            {
                int noTry = 0;

                // At startup, services may start before winlogon.
                do
                {
                    Process[] p = Process.GetProcessesByName("winlogon");
                    if (p != null)
                    {
                        for (int i = 0; i < p.Length; i++)
                        {
                            if (p[i].SessionId == dwSessionId)
                            {
                                winlogonPid = p[i].Id;
                                break;
                            }
                        }
                    }

                    noTry++;
                    if (winlogonPid == 0 && noTry < NoOfTry)
                    {
                        Thread.Sleep(1000);
                    }
                }
                while (winlogonPid == 0 && noTry < NoOfTry);

                if (winlogonPid == 0)
                {
                    return false;
                }

                STARTUPINFO si = default(STARTUPINFO);
                si.cb = (int)Marshal.SizeOf(si);
                si.lpDesktop = "winsta0\\" + Desktop;

                hProcess = OpenProcess(MAXIMUM_ALLOWED, false, (uint)winlogonPid);

                if (!OpenProcessToken(hProcess, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY | TOKEN_DUPLICATE | TOKEN_ASSIGN_PRIMARY | TOKEN_ADJUST_SESSIONID | TOKEN_READ | TOKEN_WRITE, ref hPToken))
                {
                    lastError = Marshal.GetLastWin32Error();
                    CloseHandle(hProcess);
                    return false;
                }

                SECURITY_ATTRIBUTES sa = default(SECURITY_ATTRIBUTES);
                sa.Length = Marshal.SizeOf(sa);

                if (!DuplicateTokenEx(hPToken, MAXIMUM_ALLOWED, ref sa, (int)SECURITY_IMPERSONATION_LEVEL.SecurityIdentification, (int)TOKEN_TYPE.TokenPrimary, ref hUserTokenDup))
                {
                    lastError = Marshal.GetLastWin32Error();
                    CloseHandle(hProcess);
                    CloseHandle(hPToken);
                    return false;
                }

                uint dwCreationFlags = NORMAL_PRIORITY_CLASS | CREATE_NEW_CONSOLE;
                IntPtr pEnv = IntPtr.Zero;

                if (CreateEnvironmentBlock(ref pEnv, hUserTokenDup, true))
                {
                    dwCreationFlags |= CREATE_UNICODE_ENVIRONMENT;
                }
                else
                {
                    lastError = Marshal.GetLastWin32Error();
                    pEnv = IntPtr.Zero;
                }

                // Launch the process
                PROCESS_INFORMATION pi = default(PROCESS_INFORMATION);
                bool rv = CreateProcessAsUser(
                    hUserTokenDup,            // client's access token
                    null,                   // file to execute
                    CommandLine,            // command line
                    ref sa,                 // pointer to process SECURITY_ATTRIBUTES
                    ref sa,                 // pointer to thread SECURITY_ATTRIBUTES
                    false,                  // handles are not inheritable
                    (int)dwCreationFlags,   // creation flags
                    pEnv,                   // pointer to new environment block
                    null,                   // name of current directory
                    ref si,                 // pointer to STARTUPINFO structure
                    out pi); // receives information about new process

                // GetLastError should be nonezero
                int createProcessAsUserRv = Marshal.GetLastWin32Error();

                // Close handles task
                CloseHandle(hProcess);
                CloseHandle(hUserTokenDup);
                CloseHandle(hPToken);

                if (rv)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

#if DESKTOP_STUFFS
        [DllImport("kernel32.dll")]
        private static extern UInt32 GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetThreadDesktop(UInt32 dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr OpenInputDesktop(UInt32 dwFlags, bool fInherit, UInt32 dwDesiredAccess);

        private const int UOI_FLAGS = 1;
        private const int UOI_NAME = 2;
        private const int UOI_TYPE = 3;
        private const int UOI_USER_SID = 4;
        private const UInt32 DESKTOP_WRITEOBJECTS = 0x0080;
        private const UInt32 DESKTOP_READOBJECTS = 0x0001;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetUserObjectInformation(IntPtr hObj, int nIndex, [Out] byte[] pvInfo, int nLength, out UInt32 lpnLengthNeeded);

        internal static string GetMyDesktop()
        {
            UInt32 nLengthNeeded;
            byte[] arThreadDesktop = new byte[256];
            IntPtr hD = GetThreadDesktop(GetCurrentThreadId());
            if (hD != IntPtr.Zero)
            {
                GetUserObjectInformation(hD, UOI_NAME, arThreadDesktop, arThreadDesktop.Length, out nLengthNeeded);
                return ASCIIEncoding.ASCII.GetString(arThreadDesktop);
            }
            return "";
        }

        internal static string GetInputDesktop()
        {
            UInt32 nLengthNeeded;
            byte[] arInputDesktop = new byte[256];
            IntPtr hD = OpenInputDesktop(0, false, DESKTOP_READOBJECTS);
            if (hD != IntPtr.Zero)
            {
                GetUserObjectInformation(hD, UOI_NAME, arInputDesktop, arInputDesktop.Length, out nLengthNeeded);
                return ASCIIEncoding.ASCII.GetString(arInputDesktop);
            }
            return "";
        }
#endif
    }
}
