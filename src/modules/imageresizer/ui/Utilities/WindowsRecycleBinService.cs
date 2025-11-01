// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using ImageResizer.Properties;
using ManagedCommon;
using static ImageResizer.Utilities.NativeMethods;

namespace ImageResizer.Utilities;

internal class WindowsRecycleBinService : IRecycleBinService
{
    private static readonly string NoRecycleBinExceptionMessage =
        Resources.NoRecycleBin_ExceptionMessage;

    public RecycleBinInfo QueryRecycleBin(string path = null)
    {
        // Query all drives by default.
        string root = null;

        if (path != null)
        {
            root = Path.GetPathRoot(path);

            if (string.IsNullOrEmpty(root))
            {
                throw new ArgumentException("Cannot determine drive root from path.", nameof(path));
            }
        }

        var info = new SHQUERYRBINFO()
        {
            cbSize = (uint)Marshal.SizeOf<SHQUERYRBINFO>(),
        };

        int result = SHQueryRecycleBinW(root, ref info);

        return result switch
        {
            E_FAIL => null,
            S_OK => new RecycleBinInfo(info.i64NumItems, info.i64Size),
            _ => throw new Win32Exception(result),
        };
    }

    public unsafe void DeleteToRecycleBin(string filePath)
    {
        if (!HasRecycleBin(filePath))
        {
            throw new NoRecycleBinException(NoRecycleBinExceptionMessage);
        }

        // Move to the Recycle Bin and warn about permanent deletes.
        var flags = (ushort)(FOF_ALLOWUNDO | FOF_SILENT | FOF_NOERRORUI | FOF_NO_CONFIRMATION | FOF_WANTNUKEWARNING);

        // Paths must be double-null terminated.
        string filePathTerminated = filePath + "\0\0";

        fixed (char* filePathChars = filePathTerminated)
        {
            var fileOp = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = filePathChars,
                fFlags = flags,
                hwnd = IntPtr.Zero,
                fAnyOperationsAborted = 0,
            };

            int result = SHFileOperationW(&fileOp);

            if (fileOp.fAnyOperationsAborted != 0)
            {
                throw new OperationCanceledException("The delete operation was cancelled.");
            }

            if (result != 0)
            {
                throw new Win32Exception(result);
            }
        }

        SendDeleteChangeNotification(filePath);
    }

    /// <summary>
    /// Informs shell listeners like Explorer windows that a delete operation has occurred.
    /// </summary>
    /// <param name="path">Full path to the file which was deleted.</param>
    private static void SendDeleteChangeNotification(string path)
    {
        IntPtr pathPtr = Marshal.StringToHGlobalUni(path);
        try
        {
            if (pathPtr == IntPtr.Zero)
            {
                Logger.LogError("Could not allocate memory for path string.");
            }
            else
            {
                SHChangeNotify(SHCNE_DELETE, SHCNF_PATH, pathPtr, IntPtr.Zero);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pathPtr);
        }
    }

    public bool HasRecycleBin(string path) => QueryRecycleBin(path) is not null;
}
