// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Peek.FilePreviewer.Previewers.EmailPreviewer
{
    internal static class MsgStorageInterop
    {
        public const int ReadMode = 0x20;
        public const int StorageType = 1;

        [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
        public static extern int StgOpenStorage(string name, IStorage? priority, int mode, IntPtr exclude, int reserved, out IStorage storage);

        [ComImport]
        [Guid("0000000D-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IEnumSTATSTG
        {
            [PreserveSig]
            int Next(int count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] STATSTG[] elements, IntPtr fetched);

            void Skip(int count);

            void Reset();

            void Clone(out IEnumSTATSTG enumerator);
        }

        [ComImport]
        [Guid("0000000B-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IStorage
        {
            void CreateStream([MarshalAs(UnmanagedType.LPWStr)] string name, int mode, int reserved1, int reserved2, out IStream stream);

            void OpenStream([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr reserved1, int mode, int reserved2, out IStream stream);

            void CreateStorage([MarshalAs(UnmanagedType.LPWStr)] string name, int mode, int reserved1, int reserved2, out IStorage storage);

            void OpenStorage([MarshalAs(UnmanagedType.LPWStr)] string name, IStorage? priority, int mode, IntPtr exclude, int reserved, out IStorage storage);

            void CopyTo(int ciidExclude, IntPtr rgiidExclude, IntPtr snbExclude, IStorage destination);

            void MoveElementTo([MarshalAs(UnmanagedType.LPWStr)] string name, IStorage destination, [MarshalAs(UnmanagedType.LPWStr)] string newName, int flags);

            void Commit(int flags);

            void Revert();

            void EnumElements(int reserved1, IntPtr reserved2, int reserved3, out IEnumSTATSTG enumerator);

            void DestroyElement([MarshalAs(UnmanagedType.LPWStr)] string name);

            void RenameElement([MarshalAs(UnmanagedType.LPWStr)] string oldName, [MarshalAs(UnmanagedType.LPWStr)] string newName);

            void SetElementTimes([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr creationTime, IntPtr accessTime, IntPtr modificationTime);

            void SetClass(ref Guid clsid);

            void SetStateBits(int stateBits, int mask);

            void Stat(out STATSTG stat, int flags);
        }
    }
}
