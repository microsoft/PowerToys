// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

using Accessibility;
using Wox.Plugin.Logger;

namespace Wox.Infrastructure
{
    public class ShellLinkHelper : IShellLinkHelper
    {
        [Flags]
        private enum SLGP_FLAGS
        {
            SLGP_SHORTPATH = 0x1,
            SLGP_UNCPRIORITY = 0x2,
            SLGP_RAWPATH = 0x4,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matching COM")]
        private struct WIN32_FIND_DATAW
        {
            public uint dwFileAttributes;
            public long ftCreationTime;
            public long ftLastAccessTime;
            public long ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        [Flags]
        public enum SLR_FLAGS
        {
            SLR_NO_UI = 0x1,
            SLR_ANY_MATCH = 0x2,
            SLR_UPDATE = 0x4,
            SLR_NOUPDATE = 0x8,
            SLR_NOSEARCH = 0x10,
            SLR_NOTRACK = 0x20,
            SLR_NOLINKINFO = 0x40,
            SLR_INVOKE_MSI = 0x80,
        }

        // Reference : http://www.pinvoke.net/default.aspx/Interfaces.IShellLinkW

        // The IShellLink interface allows Shell links to be created, modified, and resolved
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            /// <summary>Retrieves the path and file name of a Shell link object</summary>
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, ref WIN32_FIND_DATAW pfd, SLGP_FLAGS fFlags);

            /// <summary>Retrieves the list of item identifiers for a Shell link object</summary>
            void GetIDList(out IntPtr ppidl);

            /// <summary>Sets the pointer to an item identifier list (PIDL) for a Shell link object.</summary>
            void SetIDList(IntPtr pidl);

            /// <summary>Retrieves the description string for a Shell link object</summary>
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);

            /// <summary>Sets the description for a Shell link object. The description can be any application-defined string</summary>
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);

            /// <summary>Retrieves the name of the working directory for a Shell link object</summary>
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);

            /// <summary>Sets the name of the working directory for a Shell link object</summary>
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);

            /// <summary>Retrieves the command-line arguments associated with a Shell link object</summary>
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);

            /// <summary>Sets the command-line arguments for a Shell link object</summary>
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

            /// <summary>Retrieves the hot key for a Shell link object</summary>
            void GetHotkey(out short pwHotkey);

            /// <summary>Sets a hot key for a Shell link object</summary>
            void SetHotkey(short wHotkey);

            /// <summary>Retrieves the show command for a Shell link object</summary>
            void GetShowCmd(out int piShowCmd);

            /// <summary>Sets the show command for a Shell link object. The show command sets the initial show state of the window.</summary>
            void SetShowCmd(int iShowCmd);

            /// <summary>Retrieves the location (path and index) of the icon for a Shell link object</summary>
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);

            /// <summary>Sets the location (path and index) of the icon for a Shell link object</summary>
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);

            /// <summary>Sets the relative path to the Shell link object</summary>
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);

            /// <summary>Attempts to find the target of a Shell link, even if it has been moved or renamed</summary>
            void Resolve(ref Accessibility._RemotableHandle hwnd, SLR_FLAGS fFlags);

            /// <summary>Sets the path and file name of a Shell link object</summary>
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink
        {
        }

        // Contains the description of the app
        public string Description { get; set; } = string.Empty;

        // Contains the arguments to the app
        public string Arguments { get; set; } = string.Empty;

        public bool HasArguments { get; set; }

        // Retrieve the target path using Shell Link
        public string RetrieveTargetPath(string path)
        {
            var link = new ShellLink();
            const int STGM_READ = 0;

            try
            {
                ((IPersistFile)link).Load(path, STGM_READ);
            }
            catch (System.IO.FileNotFoundException ex)
            {
                Log.Exception("Path could not be retrieved " + path, ex, GetType(), path);
                Marshal.ReleaseComObject(link);
                return string.Empty;
            }
            catch (System.Exception ex)
            {
                Log.Exception("Exception loading path " + path, ex, GetType(), path);
                Marshal.ReleaseComObject(link);
                return string.Empty;
            }

            var hwnd = default(_RemotableHandle);
            ((IShellLinkW)link).Resolve(ref hwnd, 0);

            const int MAX_PATH = 260;
            StringBuilder buffer = new StringBuilder(MAX_PATH);

            var data = default(WIN32_FIND_DATAW);
            ((IShellLinkW)link).GetPath(buffer, buffer.Capacity, ref data, SLGP_FLAGS.SLGP_SHORTPATH);
            var target = buffer.ToString();

            // To set the app description
            if (!string.IsNullOrEmpty(target))
            {
                buffer = new StringBuilder(MAX_PATH);
                try
                {
                    ((IShellLinkW)link).GetDescription(buffer, MAX_PATH);
                    Description = buffer.ToString();
                }
                catch (System.Exception e)
                {
                    Log.Exception($"Failed to fetch description for {target}, {e.Message}", e, GetType());
                    Description = string.Empty;
                }

                StringBuilder argumentBuffer = new StringBuilder(MAX_PATH);
                ((IShellLinkW)link).GetArguments(argumentBuffer, argumentBuffer.Capacity);
                Arguments = argumentBuffer.ToString();

                // Set variable to true if the program takes in any arguments
                if (argumentBuffer.Length != 0)
                {
                    HasArguments = true;
                }
            }

            // To release unmanaged memory
            Marshal.ReleaseComObject(link);

            return target;
        }
    }
}
