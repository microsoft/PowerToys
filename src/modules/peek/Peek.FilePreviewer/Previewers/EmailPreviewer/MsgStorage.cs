// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Peek.FilePreviewer.Previewers.EmailPreviewer
{
    internal sealed class MsgStorage : IDisposable
    {
        private MsgStorageInterop.IStorage? _storage;

        private MsgStorage(MsgStorageInterop.IStorage storage)
        {
            _storage = storage;
        }

        public static MsgStorage Open(string path)
        {
            int result = MsgStorageInterop.StgOpenStorage(path, null, MsgStorageInterop.ReadMode, IntPtr.Zero, 0, out MsgStorageInterop.IStorage storage);
            Marshal.ThrowExceptionForHR(result);
            return new MsgStorage(storage);
        }

        public MsgStorage OpenStorage(string name)
        {
            GetStorage().OpenStorage(name, null, MsgStorageInterop.ReadMode, IntPtr.Zero, 0, out MsgStorageInterop.IStorage storage);
            return new MsgStorage(storage);
        }

        public IEnumerable<string> EnumerateStorages(string prefix)
        {
            GetStorage().EnumElements(0, IntPtr.Zero, 0, out MsgStorageInterop.IEnumSTATSTG enumerator);
            try
            {
                STATSTG[] item = new STATSTG[1];
                while (enumerator.Next(1, item, IntPtr.Zero) == 0)
                {
                    if (item[0].type == MsgStorageInterop.StorageType && item[0].pwcsName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        yield return item[0].pwcsName;
                    }
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(enumerator);
            }
        }

        public int ReadInt32(string propertyId)
        {
            byte[]? bytes = ReadStream($"__substg1.0_{propertyId}0003");
            return bytes?.Length >= sizeof(int) ? BitConverter.ToInt32(bytes, 0) : 0;
        }

        public string ReadString(string propertyId)
        {
            byte[]? unicode = ReadStream($"__substg1.0_{propertyId}001F");
            if (unicode != null)
            {
                return Encoding.Unicode.GetString(unicode).TrimEnd('\0');
            }

            byte[]? ansi = ReadStream($"__substg1.0_{propertyId}001E");
            return ansi == null ? string.Empty : Encoding.Latin1.GetString(ansi).TrimEnd('\0');
        }

        public byte[]? ReadStream(string name)
        {
            try
            {
                GetStorage().OpenStream(name, IntPtr.Zero, MsgStorageInterop.ReadMode, 0, out IStream stream);
                try
                {
                    stream.Stat(out STATSTG stat, 1);
                    if (stat.cbSize < 0 || stat.cbSize > int.MaxValue)
                    {
                        throw new InvalidDataException("The MSG property is too large to preview.");
                    }

                    byte[] buffer = new byte[(int)stat.cbSize];
                    IntPtr readPointer = Marshal.AllocCoTaskMem(sizeof(int));
                    try
                    {
                        stream.Read(buffer, buffer.Length, readPointer);
                        int bytesRead = Marshal.ReadInt32(readPointer);
                        return bytesRead == buffer.Length ? buffer : buffer[..bytesRead];
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(readPointer);
                    }
                }
                finally
                {
                    Marshal.FinalReleaseComObject(stream);
                }
            }
            catch (COMException)
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (_storage != null)
            {
                Marshal.FinalReleaseComObject(_storage);
                _storage = null;
            }

            GC.SuppressFinalize(this);
        }

        private MsgStorageInterop.IStorage GetStorage() => _storage ?? throw new ObjectDisposedException(nameof(MsgStorage));
    }
}
