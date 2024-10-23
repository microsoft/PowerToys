// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EverythingExtension;

public sealed class NativeMethods
    {
    [Flags]
    internal enum Request
    {
        FILE_NAME = 0x00000001,
        PATH = 0x00000002,
        FULL_PATH_AND_FILE_NAME = 0x00000004,
        EXTENSION = 0x00000008,
        SIZE = 0x00000010,
        DATE_CREATED = 0x00000020,
        DATE_MODIFIED = 0x00000040,
        DATE_ACCESSED = 0x00000080,
        ATTRIBUTES = 0x00000100,
        FILE_LIST_FILE_NAME = 0x00000200,
        RUN_COUNT = 0x00000400,
        DATE_RUN = 0x00000800,
        DATE_RECENTLY_CHANGED = 0x00001000,
        HIGHLIGHTED_FILE_NAME = 0x00002000,
        HIGHLIGHTED_PATH = 0x00004000,
        HIGHLIGHTED_FULL_PATH_AND_FILE_NAME = 0x00008000,
    }

    public enum Sort
    {
        NAME_ASCENDING = 1,
        NAME_DESCENDING,
        PATH_ASCENDING,
        PATH_DESCENDING,
        SIZE_ASCENDING,
        SIZE_DESCENDING,
        EXTENSION_ASCENDING,
        EXTENSION_DESCENDING,
        TYPE_NAME_ASCENDING,
        TYPE_NAME_DESCENDING,
        DATE_CREATED_ASCENDING,
        DATE_CREATED_DESCENDING,
        DATE_MODIFIED_ASCENDING,
        DATE_MODIFIED_DESCENDING,
        ATTRIBUTES_ASCENDING,
        ATTRIBUTES_DESCENDING,
        FILE_LIST_FILENAME_ASCENDING,
        FILE_LIST_FILENAME_DESCENDING,
        RUN_COUNT_ASCENDING,
        RUN_COUNT_DESCENDING,
        DATE_RECENTLY_CHANGED_ASCENDING,
        DATE_RECENTLY_CHANGED_DESCENDING,
        DATE_ACCESSED_ASCENDING,
        DATE_ACCESSED_DESCENDING,
        DATE_RUN_ASCENDING,
        DATE_RUN_DESCENDING,
    }

    [Flags]
    internal enum AssocF
    {
        NONE = 0x00000000,
        INIT_NOREMAPCLSID = 0x00000001,
        INIT_BYEXENAME = 0x00000002,
        INIT_DEFAULTTOSTAR = 0x00000004,
        INIT_DEFAULTTOFOLDER = 0x00000008,
        NOUSERSETTINGS = 0x00000010,
        NOTRUNCATE = 0x00000020,
        VERIFY = 0x00000040,
        REMAPRUNDLL = 0x00000080,
        NOFIXUPS = 0x00000100,
        IGNOREBASECLASS = 0x00000200,
        INIT_IGNOREUNKNOWN = 0x00000400,
        INIT_FIXED_PROGID = 0x00000800,
        IS_PROTOCOL = 0x00001000,
        INIT_FOR_FILE = 0x00002000,
    }

    internal enum AssocStr
    {
        COMMAND = 1,
        EXECUTABLE,
        FRIENDLYDOCNAME,
        FRIENDLYAPPNAME,
        NOOPEN,
        SHELLNEWVALUE,
        DDECOMMAND,
        DDEIFEXEC,
        DDEAPPLICATION,
        DDETOPIC,
        INFOTIP,
        QUICKTIP,
        TILEINFO,
        CONTENTTYPE,
        DEFAULTICON,
        SHELLEXTENSION,
        DROPTARGET,
        DELEGATEEXECUTE,
        SUPPORTED_URI_PROTOCOLS,
        PROGID,
        APPID,
        APPPUBLISHER,
        APPICONREFERENCE,
        MAX,
    }

    internal enum EverythingErrors : uint
    {
        EVERYTHING_OK = 0u,
        EVERYTHING_ERROR_MEMORY,
        EVERYTHING_ERROR_IPC,
        EVERYTHING_ERROR_REGISTERCLASSEX,
        EVERYTHING_ERROR_CREATEWINDOW,
        EVERYTHING_ERROR_CREATETHREAD,
        EVERYTHING_ERROR_INVALIDINDEX,
        EVERYTHING_ERROR_INVALIDCALL,
    }

    internal const string dllName = "Everything64.dll";

    [DllImport(dllName)]
    internal static extern uint Everything_GetNumResults();

    [DllImport(dllName)]
    [return: MarshalAs(UnmanagedType.Bool)]

    internal static extern bool Everything_GetMatchPath();

    [DllImport(dllName)]
    internal static extern uint Everything_GetMax();

    [DllImport(dllName)]
    internal static extern bool Everything_GetRegex();

    [DllImport(dllName, CharSet = CharSet.Unicode)]
    internal static extern IntPtr Everything_GetResultFileNameW(uint nIndex);

    [DllImport(dllName, CharSet = CharSet.Unicode)]
    internal static extern IntPtr Everything_GetResultPathW(uint nIndex);

    [DllImport(dllName)]
    internal static extern uint Everything_GetSort();

    [DllImport(dllName, CharSet = CharSet.Unicode)]
    internal static extern uint Everything_IncRunCountFromFileName(string lpFileName);

    [DllImport(dllName)]
    internal static extern bool Everything_IsFolderResult(uint index);

    [DllImport(dllName)]
    internal static extern bool Everything_QueryW([MarshalAs(UnmanagedType.Bool)] bool bWait);

    [DllImport(dllName)]
    internal static extern void Everything_SetMax(uint dwMax);

    [DllImport(dllName)]
    internal static extern void Everything_SetRegex([MarshalAs(UnmanagedType.Bool)] bool bEnable);

    [DllImport(dllName)]
    internal static extern void Everything_SetRequestFlags(Request RequestFlags);

    [DllImport(dllName, CharSet = CharSet.Unicode)]
    internal static extern void Everything_SetSearchW(string lpSearchString);

    [DllImport(dllName)]
    internal static extern bool Everything_SetMatchPath([MarshalAs(UnmanagedType.Bool)] bool bEnable);

    [DllImport(dllName)]
    internal static extern void Everything_SetSort(Sort SortType);

    [DllImport(dllName)]
    internal static extern uint Everything_GetLastError();
}
