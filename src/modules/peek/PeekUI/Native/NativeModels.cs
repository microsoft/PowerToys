// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace PeekUI.Native
{
    public class NativeModels
    {
        public const int GwlExStyle = -20;
        public const int WsExToolWindow = 0x00000080;

        public enum PerceivedType
        {
            Folder = -1,
            Unknown = 0,
            Image = 2,
            Video = 4,
            Document = 6,
        }

        public enum Perceived
        {
            Undefined = 0x0000,
            Softcoded = 0x0001,
            Hardcoded = 0x0002,
            NativeSupport = 0x0004,
            GdiPlus = 0x0010,
            WMSDK = 0x0020,
            ZipFolder = 0x0040,
        }

        public enum HResult
        {
            Ok = 0x0000,
            False = 0x0001,
            InvalidArguments = unchecked((int)0x80070057),
            OutOfMemory = unchecked((int)0x8007000E),
            NoInterface = unchecked((int)0x80004002),
            Fail = unchecked((int)0x80004005),
            ExtractionFailed = unchecked((int)0x8004B200),
            ElementNotFound = unchecked((int)0x80070490),
            TypeElementNotFound = unchecked((int)0x8002802B),
            NoObject = unchecked((int)0x800401E5),
            Win32ErrorCanceled = 1223,
            Canceled = unchecked((int)0x800704C7),
            ResourceInUse = unchecked((int)0x800700AA),
            AccessDenied = unchecked((int)0x80030005),
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Input
        {
            public InputType Type;
            public InputUnion Data;

            public static int Size
            {
                get { return Marshal.SizeOf(typeof(Input)); }
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MouseInput Mi;

            [FieldOffset(0)]
            public KeybdInput Ki;

            [FieldOffset(0)]
            public HardwareInput Hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MouseInput
        {
            public int Dx;
            public int Dy;
            public int MouseData;
            public uint DwFlags;
            public uint Time;
            public UIntPtr DwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KeybdInput
        {
            public short WVk;
            public short WScan;
            public uint DwFlags;
            public int Time;
            public UIntPtr DwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HardwareInput
        {
            public int UMsg;
            public short WParamL;
            public short WParamH;
        }

        public enum InputType : uint
        {
            InputMouse = 0,
            InputKeyboard = 1,
            InputHardware = 2,
        }

        public enum Sigdn : uint
        {
            NormalDisplay = 0,
            ParentRelativeParsing = 0x80018001,
            ParentRelativeForAddressBar = 0x8001c001,
            DesktopAbsoluteParsing = 0x80028000,
            ParentRelativeEditing = 0x80031001,
            DesktopAbsoluteEditing = 0x8004c000,
            FileSysPath = 0x80058000,
            Url = 0x80068000,
        }

        public enum DwmWindowAttributed
        {
            DwmaWindowCornerPreference = 33,
        }

        // The DWM_WINDOW_CORNER_PREFERENCE enum for DwmSetWindowAttribute's third parameter, which tells the function
        // what value of the enum to set.
        public enum DwmWindowCornerPreference
        {
            DwmCpDefault = 0,
            DwmCpDoNotRound = 1,
            DwmCpRound = 2,
            DwmCpRoundSmall = 3,
        }

        [Flags]
        public enum ThumbnailOptions
        {
            None = 0x00,
            BiggerSizeOk = 0x01,
            InMemoryOnly = 0x02,
            IconOnly = 0x04,
            ThumbnailOnly = 0x08,
            InCacheOnly = 0x10,
            ScaleUp = 0x100,
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellItemImageFactory
        {
            [PreserveSig]
            HResult GetImage(
            [In, MarshalAs(UnmanagedType.Struct)] NativeSize size,
            [In] ThumbnailOptions flags,
            [Out] out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NativeSize
        {
            private int width;
            private int height;

            public int Width
            {
                set { width = value; }
            }

            public int Height
            {
                set { height = value; }
            }
        }
    }
}
