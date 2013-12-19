using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace WinAlfred.Plugin.Everything
{
    public sealed class EverythingAPI
    {
        const int EVERYTHING_OK	= 0;
		const int EVERYTHING_ERROR_MEMORY = 1;
		const int EVERYTHING_ERROR_IPC = 2;
		const int EVERYTHING_ERROR_REGISTERCLASSEX = 3;
		const int EVERYTHING_ERROR_CREATEWINDOW = 4;
		const int EVERYTHING_ERROR_CREATETHREAD = 5;
		const int EVERYTHING_ERROR_INVALIDINDEX = 6;
		const int EVERYTHING_ERROR_INVALIDCALL = 7;

		[DllImport("Everything.dll")]
		private static extern int Everything_SetSearch(string lpSearchString);
		[DllImport("Everything.dll")]
		private static extern void Everything_SetMatchPath(bool bEnable);
		[DllImport("Everything.dll")]
		private static extern void Everything_SetMatchCase(bool bEnable);
		[DllImport("Everything.dll")]
		private static extern void Everything_SetMatchWholeWord(bool bEnable);
		[DllImport("Everything.dll")]
		private static extern void Everything_SetRegex(bool bEnable);
		[DllImport("Everything.dll")]
		private static extern void Everything_SetMax(int dwMax);
		[DllImport("Everything.dll")]
		private static extern void Everything_SetOffset(int dwOffset);

		[DllImport("Everything.dll")]
		private static extern bool Everything_GetMatchPath();
		[DllImport("Everything.dll")]
		private static extern bool Everything_GetMatchCase();
		[DllImport("Everything.dll")]
		private static extern bool Everything_GetMatchWholeWord();
		[DllImport("Everything.dll")]
		private static extern bool Everything_GetRegex();
		[DllImport("Everything.dll")]
		private static extern UInt32 Everything_GetMax();
		[DllImport("Everything.dll")]
		private static extern UInt32 Everything_GetOffset();
		[DllImport("Everything.dll")]
		private static extern string Everything_GetSearch();
		[DllImport("Everything.dll")]
		private static extern int Everything_GetLastError();

		[DllImport("Everything.dll")]
		private static extern bool Everything_Query();

		[DllImport("Everything.dll")]
		private static extern void Everything_SortResultsByPath();

		[DllImport("Everything.dll")]
		private static extern int Everything_GetNumFileResults();
		[DllImport("Everything.dll")]
		private static extern int Everything_GetNumFolderResults();
		[DllImport("Everything.dll")]
		private static extern int Everything_GetNumResults();
		[DllImport("Everything.dll")]
		private static extern int Everything_GetTotFileResults();
		[DllImport("Everything.dll")]
		private static extern int Everything_GetTotFolderResults();
		[DllImport("Everything.dll")]
		private static extern int Everything_GetTotResults();
		[DllImport("Everything.dll")]
		private static extern bool Everything_IsVolumeResult(int nIndex);
		[DllImport("Everything.dll")]
		private static extern bool Everything_IsFolderResult(int nIndex);
		[DllImport("Everything.dll")]
		private static extern bool Everything_IsFileResult(int nIndex);
		[DllImport("Everything.dll")]
		private static extern void Everything_GetResultFullPathName(int nIndex, StringBuilder lpString, int nMaxCount);
		[DllImport("Everything.dll")]
		private static extern void Everything_Reset();


		public void Search(string query)
		{
			int i;
			const int bufsize = 260; 
			StringBuilder buf = new StringBuilder(bufsize);

            Everything_SetSearch(query);
            Everything_Query();
            // loop through the results, adding each result to the listbox.
            for (i = 0; i < Everything_GetNumResults(); i++)
            {
                // get the result's full path and file name.
                Everything_GetResultFullPathName(i, buf, bufsize);
            }
		}
    }
}
