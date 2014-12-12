using System;
using System.Runtime.InteropServices;

namespace Wox.Plugin.FindFile.MFTSearch {
    public class PInvokeWin32
    {

		public const UInt32 GENERIC_READ                     = 0x80000000;
		public const UInt32 GENERIC_WRITE                    = 0x40000000;
		public const UInt32 FILE_SHARE_READ                  = 0x00000001;
		public const UInt32 FILE_SHARE_WRITE                 = 0x00000002;
		public const UInt32 FILE_ATTRIBUTE_DIRECTORY         = 0x00000010;
		public const UInt32 OPEN_EXISTING                    = 3;
		public const UInt32 FILE_FLAG_BACKUP_SEMANTICS       = 0x02000000;
		public const Int32 INVALID_HANDLE_VALUE              = -1;
		public const UInt32 FSCTL_QUERY_USN_JOURNAL          = 0x000900f4;
		public const UInt32 FSCTL_ENUM_USN_DATA              = 0x000900b3;
		public const UInt32 FSCTL_CREATE_USN_JOURNAL         = 0x000900e7;
		public const UInt32 FSCTL_READ_USN_JOURNAL           = 0x000900bb;



		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess,
											   uint dwShareMode, IntPtr lpSecurityAttributes,
											   uint dwCreationDisposition, uint dwFlagsAndAttributes,
											   IntPtr hTemplateFile);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetFileInformationByHandle(IntPtr hFile,
															 out BY_HANDLE_FILE_INFORMATION lpFileInformation);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool CloseHandle(IntPtr hObject);

		[DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool DeviceIoControl(IntPtr hDevice,
												  UInt32 dwIoControlCode,
												  IntPtr lpInBuffer, Int32 nInBufferSize,
												  out USN_JOURNAL_DATA lpOutBuffer, Int32 nOutBufferSize,
												  out uint lpBytesReturned, IntPtr lpOverlapped);

		[DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool DeviceIoControl(IntPtr hDevice,
												  UInt32 dwIoControlCode,
												  IntPtr lpInBuffer, Int32 nInBufferSize,
												  IntPtr lpOutBuffer, Int32 nOutBufferSize,
												  out uint lpBytesReturned, IntPtr lpOverlapped);

		[DllImport("kernel32.dll")]
		public static extern void ZeroMemory(IntPtr ptr, Int32 size);

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct BY_HANDLE_FILE_INFORMATION {
			public uint FileAttributes;
			public FILETIME CreationTime;
			public FILETIME LastAccessTime;
			public FILETIME LastWriteTime;
			public uint VolumeSerialNumber;
			public uint FileSizeHigh;
			public uint FileSizeLow;
			public uint NumberOfLinks;
			public uint FileIndexHigh;
			public uint FileIndexLow;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct FILETIME {
			public uint DateTimeLow;
			public uint DateTimeHigh;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct USN_JOURNAL_DATA {
			public UInt64 UsnJournalID;
			public Int64 FirstUsn;
			public Int64 NextUsn;
			public Int64 LowestValidUsn;
			public Int64 MaxUsn;
			public UInt64 MaximumSize;
			public UInt64 AllocationDelta;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct MFT_ENUM_DATA {
			public UInt64 StartFileReferenceNumber;
			public Int64 LowUsn;
			public Int64 HighUsn;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct CREATE_USN_JOURNAL_DATA {
			public UInt64 MaximumSize;
			public UInt64 AllocationDelta;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct READ_USN_JOURNAL_DATA {
			public Int64 StartUsn;
			public UInt32 ReasonMask;
			public UInt32 ReturnOnlyOnClose;
			public UInt64 Timeout;
			public UInt64 BytesToWaitFor;
			public UInt64 UsnJournalID;
		}

		public class USN_RECORD {
			public UInt32 RecordLength;
			public UInt16 MajorVersion;
			public UInt16 MinorVersion;
			public UInt64 FRN;  // 8
			public UInt64 ParentFRN; // 16
			public Int64 Usn; // Need be care
			public UInt64 TimeStamp; // Need Be care
			public UInt32 Reason;
			public UInt32 SourceInfo;
			public UInt32 SecurityId;
			public UInt32 FileAttributes; // 52
			public UInt16 FileNameLength;
			public UInt16 FileNameOffset;
			public string FileName = string.Empty;

			private const int RecordLength_OFFSET = 0;
			private const int MajorVersion_OFFSET = 4;
			private const int MinorVersion_OFFSET = 6;
			private const int FileReferenceNumber_OFFSET = 8;
			private const int ParentFileReferenceNumber_OFFSET = 16;
			private const int Usn_OFFSET = 24;
			private const int TimeStamp_OFFSET = 32;
			private const int Reason_OFFSET = 40;
			private const int SourceInfo_OFFSET = 44;
			private const int SecurityId_OFFSET = 48;
			private const int FileAttributes_OFFSET = 52;
			private const int FileNameLength_OFFSET = 56;
			private const int FileNameOffset_OFFSET = 58;
			private const int FileName_OFFSET = 60;

			public USN_RECORD(IntPtr p) {
				this.RecordLength = (UInt32)Marshal.ReadInt32(p, RecordLength_OFFSET);
				this.MajorVersion = (UInt16)Marshal.ReadInt16(p, MajorVersion_OFFSET);
				this.MinorVersion = (UInt16)Marshal.ReadInt16(p, MinorVersion_OFFSET);
				this.FRN = (UInt64)Marshal.ReadInt64(p, FileReferenceNumber_OFFSET);
				this.ParentFRN = (UInt64)Marshal.ReadInt64(p, ParentFileReferenceNumber_OFFSET);
				this.Usn = Marshal.ReadInt64(p, Usn_OFFSET);
				this.TimeStamp = (UInt64)Marshal.ReadInt64(p, TimeStamp_OFFSET);
				this.Reason = (UInt32)Marshal.ReadInt32(p, Reason_OFFSET);
				this.SourceInfo = (UInt32)Marshal.ReadInt32(p, SourceInfo_OFFSET);
				this.SecurityId = (UInt32)Marshal.ReadInt32(p, SecurityId_OFFSET);
				this.FileAttributes = (UInt32)Marshal.ReadInt32(p, FileAttributes_OFFSET);
				this.FileNameLength = (UInt16)Marshal.ReadInt16(p, FileNameLength_OFFSET);
				this.FileNameOffset = (UInt16)Marshal.ReadInt16(p, FileNameOffset_OFFSET);

				this.FileName = Marshal.PtrToStringUni(new IntPtr(p.ToInt32() + this.FileNameOffset), this.FileNameLength / sizeof(char));
			}

			public bool IsFolder {
				get { return 0 != (FileAttributes & PInvokeWin32.FILE_ATTRIBUTE_DIRECTORY); }
			}
		}
	}
}
