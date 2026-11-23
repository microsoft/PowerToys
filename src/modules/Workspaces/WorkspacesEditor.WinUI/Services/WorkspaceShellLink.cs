// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using WorkspacesCsharpLibrary.Data;

namespace WorkspacesEditor.Services
{
    internal static class WorkspaceShellLink
    {
        private const string LauncherFileName = "PowerToys.WorkspacesLauncher.exe";
        private const int RawPath = 4;
        private static readonly string ShortcutInvokePoint = ((int)InvokePoint.Shortcut).ToString(CultureInfo.InvariantCulture);

        internal static byte[] Create(string launcherPath, Guid workspaceId, string iconPath, byte[] existing = null)
        {
            return WithShellLink(link =>
            {
                if (existing != null)
                {
                    // Retain shell properties the user can customize, such as a hotkey or window style.
                    LoadContents(link, existing);
                    if (!IsOwned(link, workspaceId))
                    {
                        throw new IOException("The workspace shortcut was replaced before it could be updated.");
                    }
                }

                string canonicalId = workspaceId.ToString("B");
                link.SetPath(launcherPath);
                link.SetWorkingDirectory(Path.GetDirectoryName(launcherPath));
                link.SetArguments($"{canonicalId} {ShortcutInvokePoint}");
                link.SetDescription($"Project Launcher {canonicalId}");
                link.SetIconLocation(iconPath, 0);

                IStream stream = CreateMemoryStream(null);
                try
                {
                    ((IPersistStream)link).Save(stream, true);
                    stream.Stat(out STATSTG stat, 1); // STATFLAG_NONAME
                    byte[] contents = new byte[checked((int)stat.cbSize)];
                    stream.Seek(0, (int)SeekOrigin.Begin, IntPtr.Zero);
                    stream.Read(contents, contents.Length, IntPtr.Zero);
                    return contents;
                }
                finally
                {
                    Marshal.FinalReleaseComObject(stream);
                }
            });
        }

        internal static bool IsOwned(byte[] contents, Guid workspaceId)
        {
            return WithShellLink(link =>
            {
                LoadContents(link, contents);
                return IsOwned(link, workspaceId);
            });
        }

        private static void LoadContents(IShellLinkW link, byte[] contents)
        {
            IStream stream = CreateMemoryStream(contents);
            try
            {
                ((IPersistStream)link).Load(stream);
            }
            catch (COMException ex) when (ex.HResult is unchecked((int)0x80004005) or unchecked((int)0x8003001E) or unchecked((int)0x800300FB) or unchecked((int)0x80030109))
            {
                // The file is already in memory. E_FAIL, STG_E_READFAULT, STG_E_INVALIDHEADER
                // and STG_E_DOCFILECORRUPT here describe invalid Shell Link content, not a file lock.
                throw new InvalidDataException("The desktop file is not a readable Shell Link.", ex);
            }
            finally
            {
                Marshal.FinalReleaseComObject(stream);
            }
        }

        internal static (bool Owned, string IconPath) Inspect(byte[] contents, Guid workspaceId)
        {
            return WithShellLink(link =>
            {
                LoadContents(link, contents);
                string iconPath = ReadString((buffer, capacity) => link.GetIconLocation(buffer, capacity, out _));
                return (IsOwned(link, workspaceId), iconPath);
            });
        }

        private static bool IsOwned(IShellLinkW link, Guid workspaceId)
        {
            // Read the stored target without resolving it. An upgrade may have moved the launcher.
            string target = ReadString((buffer, capacity) => link.GetPath(buffer, capacity, IntPtr.Zero, RawPath));
            if (!string.Equals(Path.GetFileName(target), LauncherFileName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] arguments = ReadString(link.GetArguments).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return arguments.Length == 2 &&
                arguments[0].Length == 38 &&
                Guid.TryParseExact(arguments[0], "B", out Guid id) && id == workspaceId &&
                arguments[1] == ShortcutInvokePoint;
        }

        private static string ReadString(Action<StringBuilder, int> read)
        {
            // Shell link StringData fields contain a 16-bit character count. Shell getters
            // can silently truncate to the supplied capacity, including launcher arguments.
            const int MaxCapacity = ushort.MaxValue + 1;
            var buffer = new StringBuilder(260);
            while (true)
            {
                int capacity = buffer.Capacity;
                read(buffer, capacity);
                if (buffer.Length < capacity - 1 || capacity == MaxCapacity)
                {
                    return buffer.ToString();
                }

                buffer.Clear();
                buffer.Capacity = Math.Min(capacity * 2, MaxCapacity);
            }
        }

        private static T WithShellLink<T>(Func<IShellLinkW, T> action)
        {
            var link = (IShellLinkW)new ShellLink();
            try
            {
                return action(link);
            }
            finally
            {
                Marshal.FinalReleaseComObject(link);
            }
        }

        private static IStream CreateMemoryStream(byte[] contents)
        {
            return SHCreateMemStream(contents, (uint)(contents?.Length ?? 0))
                ?? throw new IOException("The Shell could not create a stream for the workspace shortcut.");
        }

        [DllImport("shlwapi.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Interface)]
        private static extern IStream SHCreateMemStream([In] byte[] initialData, uint length);

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        internal class ShellLink
        {
        }

        [ComImport]
        [Guid("00000109-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPersistStream
        {
            void GetClassID(out Guid classId);

            [PreserveSig]
            int IsDirty();

            void Load(IStream stream);

            void Save(IStream stream, [MarshalAs(UnmanagedType.Bool)] bool clearDirty);

            void GetSizeMax(out ulong size);
        }

        // Keep the IShellLinkW vtable order, including the methods that the editor does not use.
        [ComImport]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int capacity, IntPtr findData, int flags);

            void GetIDList(out IntPtr itemIdList);

            void SetIDList(IntPtr itemIdList);

            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int capacity);

            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);

            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int capacity);

            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);

            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int capacity);

            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);

            void GetHotkey(out short hotkey);

            void SetHotkey(short hotkey);

            void GetShowCmd(out int showCommand);

            void SetShowCmd(int showCommand);

            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int capacity, out int index);

            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int index);

            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, int reserved);

            void Resolve(IntPtr window, int flags);

            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        }
    }
}
