using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;

namespace Wox.Infrastructure
{
    /*
     * http://undoc.airesoft.co.uk/shell32.dll/ShellExecCmdLine.php
     * 
     * IDA pseudocodes:
     * * CRunDlg::OKPushed https://gist.github.com/anonymous/bb0581d062ae82169768
     * * ShellExecuteCmdLine https://gist.github.com/anonymous/7efeac7a89498e2667d5
     * * PromptForMedia https://gist.github.com/anonymous/4900265ca20a98da0947
     * 
     * leaked Windows 2000 source codes:
     * * rundlg.cpp https://gist.github.com/anonymous/d97e9490e095a40651b0
     * * exec.c https://gist.github.com/anonymous/594d62eb684cf5ff3052
     */

    public static class WindowsShellRun
    {

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern string PathGetArgs([In] string pszPath);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern void PathRemoveArgs([MarshalAs(UnmanagedType.LPTStr)]StringBuilder lpszPath);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern void PathUnquoteSpaces([MarshalAs(UnmanagedType.LPTStr)]StringBuilder lpsz);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern int PathGetDriveNumber([In] string lpsz);

        const int SHPPFW_IGNOREFILENAME = 4;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHPathPrepareForWrite(IntPtr hwnd,
           [MarshalAs(UnmanagedType.IUnknown)] object punkEnableModless,
           string pszPath, uint dwFlags);

        static bool PromptForMedia(String cmd, out int driveId)
        {
            StringBuilder sb = new StringBuilder(cmd, cmd.Length);
            PathRemoveArgs(sb);
            PathUnquoteSpaces(sb);

            if ((driveId = PathGetDriveNumber(sb.ToString())) != -1 &&
                SHPathPrepareForWrite(IntPtr.Zero, 0, sb.ToString(), SHPPFW_IGNOREFILENAME) < 0)
            // if replace IntPtr.Zero with a form handle, 
            // it will show a dialog waiting for media insert
            // check it here: https://f.cloud.github.com/assets/158528/2008562/6bb65164-874d-11e3-8f66-c8a4773bd5f2.png
            {
                return false;
            }
            return true;
        }

        [Flags]
        enum ShellExecCmdLineFlags
        {
            SECL_USEFULLPATHDIR = 0x1,
            SECL_NO_UI = 0x2,
            SECL_4 = 0x4,
            SECL_LOG_USAGE = 0x8,
            SECL_USE_IDLIST = 0x10,
            SECL__IGNORE_ERROR = 0x20,
            SECL_RUNAS = 0x40
        }

        const int URLIS_URL = 0;
        const int URLIS_FILEURL = 3;

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern bool UrlIs(string pszUrl, int UrlIs);

        static void ShellExecCmdLine(IntPtr hInstance, IntPtr hwnd, string command, string startDir, global::System.Diagnostics.ProcessWindowStyle nShow, ShellExecCmdLineFlags dwSeclFlags)
        {
            string cmd = command;
            string args = null;
            if (UrlIs(command, URLIS_URL))
                cmd = command;
            else
            {
                if (global::System.Environment.OSVersion.Version.Major >= 6)
                    EvaluateSystemAndUserCommandLine(cmd, startDir, out cmd, out args, dwSeclFlags);
                else
                    EvaluateUserCommandLine(cmd, startDir, out cmd, out args);
            }

            if (!UrlIs(cmd, URLIS_URL)
                && (
                    (dwSeclFlags & ShellExecCmdLineFlags.SECL_USEFULLPATHDIR) == ShellExecCmdLineFlags.SECL_USEFULLPATHDIR
                    || startDir == null 
                    || startDir.Length == 0))
            {
                string dir = QualifyWorkingDir(cmd);
                if (dir != null)
                    startDir = dir;
            }

            global::System.Diagnostics.ProcessStartInfo startInfo = new global::System.Diagnostics.ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.Arguments = args;
            startInfo.FileName = cmd;
            startInfo.WindowStyle = global::System.Diagnostics.ProcessWindowStyle.Normal;
            startInfo.ErrorDialog = (dwSeclFlags | ShellExecCmdLineFlags.SECL_NO_UI) == 0;
            startInfo.ErrorDialogParentHandle = hwnd;

            try
            {
                global::System.Diagnostics.Process.Start(startInfo);
            }
            catch (Exception e)
            {
                if (!startInfo.ErrorDialog)
                    throw e;
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHEvaluateSystemCommandTemplate(
          string pszCmdTemplate,
          out IntPtr ppszApplication,
          out IntPtr ppszCommandLine,
          out IntPtr ppszParameters
        );

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern void PathQualify(
          [MarshalAs(UnmanagedType.LPWStr)] StringBuilder psz
        );

        static string QualifyWorkingDir(string pszPath)
        {
            // special case to make sure the working dir gets set right:
            //   1) no working dir specified
            //   2) a drive or a root path, or a relative path specified
            // derive the working dir from the qualified path. this is to make
            // sure the working dir for setup programs "A:setup" is set right

            if (pszPath.IndexOfAny(new char[] { '\\', ':'}) >= 0)
            {
                // build working dir based on qualified path
                if (Directory.Exists(pszPath))
                    return pszPath;

                pszPath = Path.GetDirectoryName(pszPath);
                if (pszPath != null)
                    try
                    {
                        return Path.GetFullPath(pszPath);
                    }
                    catch
                    {
                        return pszPath;
                    }
            }

            return null;
        }

        static bool CopyCommand(string pszCommand, string pszDir, out string pszOut)
        {
            pszOut = pszCommand;
            if (pszCommand[0] != '"')
            {
                if (UrlIs(pszCommand, URLIS_URL))
                {
                    //  urls never have params...
                    if (UrlIs(pszCommand, URLIS_FILEURL))
                        pszOut = new Uri(pszCommand).LocalPath; // PathCreateFromUrl(pszCommand, pszOut, &cchOut, 0);
                    else
                        pszOut = pszCommand;

                    return false;
                }
                else
                {
                    try
                    {
                        pszOut = Path.GetFullPath(pszCommand); // PathQualifyDef(pszOut, pszDir, 0);
                    }
                    catch
                    {
                        pszOut = pszCommand;
                    }

                    // TODO: deal with attributes
                    if (File.Exists(pszOut) || Directory.Exists(pszOut)) // PathFileExistsAndAttributes(pszOut, NULL)
                        return false;
                }
            }
            pszOut = pszCommand;
            return true;
        }

        static void EvaluateUserCommandLine(string command, string startDir, out string cmd, out string args)
        {
            if (CopyCommand(command, startDir, out cmd))
            {
                //  there might be args in that command
                args = PathGetArgs(cmd);
                if (args != null)
                    cmd = cmd.Substring(0, cmd.Length - args.Length).Trim();
            }
            else
                args = null;

            StringBuilder buffer = new StringBuilder(cmd);
            PathUnquoteSpaces(buffer);
            cmd = buffer.ToString();
        }

        static void EvaluateSystemAndUserCommandLine(string command, string startDir, out string cmd, out string args, ShellExecCmdLineFlags dwSeclFlags)
        {
            IntPtr pcmd, pcmdl, parg;
            int result = SHEvaluateSystemCommandTemplate(command, out pcmd, out pcmdl, out parg);
            if (result < 0)
            {
                if ((dwSeclFlags & ShellExecCmdLineFlags.SECL__IGNORE_ERROR) == 0)
                    throwHRESULT(result);

                EvaluateUserCommandLine(command, startDir, out cmd, out args);
            }
            else
            {
                cmd = Marshal.PtrToStringUni(pcmd);
                args = Marshal.PtrToStringUni(parg);
                Marshal.FreeCoTaskMem(pcmd);
                Marshal.FreeCoTaskMem(pcmdl);
                Marshal.FreeCoTaskMem(parg);
            }
        }

        static int throwHRESULT(int hresult)
        {
            throw new global::System.IO.IOException(
                new global::System.ComponentModel.Win32Exception(hresult ^ -0x7FF90000).Message,
                hresult);
        }

        public static void Start(string cmd)
        {
            Start(cmd, false);
        }

        public static void Start(string cmd, bool showErrorDialog)
        {
            Start(cmd, false, IntPtr.Zero);
        }

        public static void Start(string cmd, bool showErrorDialog, IntPtr errorDialogHwnd)
        {
            cmd = cmd.Trim(); // PathRemoveBlanks
            cmd = Environment.ExpandEnvironmentVariables(cmd); // SHExpandEnvironmentStrings
            int driveId = -1;
            if (PromptForMedia(cmd, out driveId))
            {
                ShellExecCmdLine(
                    IntPtr.Zero,
                    errorDialogHwnd,
                    cmd,
                    null, // i have no ideas about this field
                    global::System.Diagnostics.ProcessWindowStyle.Normal,
                    ShellExecCmdLineFlags.SECL__IGNORE_ERROR | ShellExecCmdLineFlags.SECL_USE_IDLIST | ShellExecCmdLineFlags.SECL_LOG_USAGE | (showErrorDialog ? 0 : ShellExecCmdLineFlags.SECL_NO_UI)
                );
            }
            else
            {   // Device not ready 0x80070015
                throw new global::System.IO.IOException(
                    new global::System.ComponentModel.Win32Exception(0x15).Message,
                    -0x7FF90000 | 0x15);
            }
        }
    }
}
