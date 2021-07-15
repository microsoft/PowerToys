using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Community.PowerToys.Run.Plugin.Everything.SearchHelper
{
    internal sealed class EverythingApiDllImport
    {
        internal const string DllPath = "Everything64.dll";

        [DllImport(DllPath, CharSet = CharSet.Unicode)]
        internal static extern int Everything_SetSearchW(string lpSearchString);

        [DllImport(DllPath)]
        internal static extern void Everything_SetMatchPath(bool bEnable);

        [DllImport(DllPath)]
        internal static extern void Everything_SetMatchCase(bool bEnable);

        [DllImport(DllPath)]
        internal static extern void Everything_SetMatchWholeWord(bool bEnable);

        [DllImport(DllPath)]
        internal static extern void Everything_SetRegex(bool bEnable);

        [DllImport(DllPath)]
        internal static extern void Everything_SetMax(int dwMax);

        [DllImport(DllPath)]
        internal static extern void Everything_SetOffset(int dwOffset);

        [DllImport(DllPath)]
        internal static extern bool Everything_GetMatchPath();

        [DllImport(DllPath)]
        internal static extern bool Everything_GetMatchCase();

        [DllImport(DllPath)]
        internal static extern bool Everything_GetMatchWholeWord();

        [DllImport(DllPath)]
        internal static extern bool Everything_GetRegex();

        [DllImport(DllPath)]
        internal static extern uint Everything_GetMax();

        [DllImport(DllPath)]
        internal static extern uint Everything_GetOffset();

        [DllImport(DllPath, CharSet = CharSet.Unicode)]
        internal static extern string Everything_GetSearchW();

        [DllImport(DllPath)]
        internal static extern EverythingApi.StateCode Everything_GetLastError();

        [DllImport(DllPath, CharSet = CharSet.Unicode)]
        internal static extern bool Everything_QueryW(bool bWait);

        [DllImport(DllPath)]
        internal static extern void Everything_SortResultsByPath();

        [DllImport(DllPath)]
        internal static extern int Everything_GetNumFileResults();

        [DllImport(DllPath)]
        internal static extern int Everything_GetNumFolderResults();

        [DllImport(DllPath)]
        internal static extern int Everything_GetNumResults();

        [DllImport(DllPath)]
        internal static extern int Everything_GetTotFileResults();

        [DllImport(DllPath)]
        internal static extern int Everything_GetTotFolderResults();

        [DllImport(DllPath)]
        internal static extern int Everything_GetTotResults();

        [DllImport(DllPath)]
        internal static extern bool Everything_IsVolumeResult(int nIndex);

        [DllImport(DllPath)]
        internal static extern bool Everything_IsFolderResult(int nIndex);

        [DllImport(DllPath)]
        internal static extern bool Everything_IsFileResult(int nIndex);

        [DllImport(DllPath, CharSet = CharSet.Unicode)]
        internal static extern void Everything_GetResultFullPathNameW(int nIndex, StringBuilder lpString, int nMaxCount);

        [DllImport(DllPath, CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultFileNameW(int nIndex);

        [DllImport(DllPath, CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultHighlightedPathW(int nIndex);

        [DllImport(DllPath, CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultHighlightedFileNameW(int nIndex);

        [DllImport(DllPath, CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultHighlightedFullPathAndFileNameW(int nIndex);

        [DllImport(DllPath)]
        public static extern int Everything_GetMajorVersion();

        [DllImport(DllPath)]
        public static extern int Everything_GetMinorVersion();

        [DllImport(DllPath)]
        public static extern int Everything_GetRevision();

        [DllImport(DllPath)]
        public static extern void Everything_SetRequestFlags(EverythingApi.RequestFlag flag);

        [DllImport(DllPath)]
        internal static extern void Everything_Reset();
    }
}
