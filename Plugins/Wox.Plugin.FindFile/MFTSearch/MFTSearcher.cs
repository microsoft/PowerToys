/*
 *  Thanks to the https://github.com/yiwenshengmei/MyEverything, we can bring MFT search to Wox
 * 
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Wox.Plugin.FindFile.MFTSearch
{

    public class MFTSearcher
    {
        private static MFTSearcherCache cache = new MFTSearcherCache();

        private static void IndexVolume(string volume)
        {
            cache.CheckHashTableKey(volume);
            EnumerateVolume(volume,cache.VolumeRecords[volume]);
        }

        public static void IndexAllVolumes()
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                IndexVolume(drive.Name.Replace("\\", ""));
            }
        }

        public static long IndexedFileCount
        {
            get { return cache.RecordsCount; }
        }

        public static List<MFTSearchRecord> Search(string item)
        {
            if (string.IsNullOrEmpty(item)) return new List<MFTSearchRecord>();

            List<USNRecord> found = cache.FindByName(item,100);
            found.ForEach(x => FillPath(x.VolumeName, x, cache));
            return found.ConvertAll(o => new MFTSearchRecord(o));
        }

        private static void AddVolumeRootRecord(string volumeName, Dictionary<ulong, USNRecord> files)
        {
            string rightVolumeName = string.Concat("\\\\.\\", volumeName);
            rightVolumeName = string.Concat(rightVolumeName, Path.DirectorySeparatorChar);
            IntPtr hRoot = PInvokeWin32.CreateFile(rightVolumeName,
                0,
                PInvokeWin32.FILE_SHARE_READ | PInvokeWin32.FILE_SHARE_WRITE,
                IntPtr.Zero,
                PInvokeWin32.OPEN_EXISTING,
                PInvokeWin32.FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero);

            if (hRoot.ToInt32() != PInvokeWin32.INVALID_HANDLE_VALUE)
            {
                PInvokeWin32.BY_HANDLE_FILE_INFORMATION fi = new PInvokeWin32.BY_HANDLE_FILE_INFORMATION();
                bool bRtn = PInvokeWin32.GetFileInformationByHandle(hRoot, out fi);
                if (bRtn)
                {
                    UInt64 fileIndexHigh = (UInt64)fi.FileIndexHigh;
                    UInt64 indexRoot = (fileIndexHigh << 32) | fi.FileIndexLow;

                    files.Add(indexRoot,new USNRecord
                    {
                        FRN = indexRoot,
                        Name = volumeName,
                        ParentFrn = 0,
                        IsVolumeRoot = true,
                        IsFolder = true,
                        VolumeName = volumeName
                    });
                }
                else
                {
                    throw new IOException("GetFileInformationbyHandle() returned invalid handle",
                        new Win32Exception(Marshal.GetLastWin32Error()));
                }
                PInvokeWin32.CloseHandle(hRoot);
            }
            else
            {
                throw new IOException("Unable to get root frn entry", new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }

        private static void EnumerateVolume(string volumeName, Dictionary<ulong, USNRecord> files)
        {
            IntPtr medBuffer = IntPtr.Zero;
            IntPtr pVolume = IntPtr.Zero;
            try
            {
                AddVolumeRootRecord(volumeName,files);
                pVolume = GetVolumeJournalHandle(volumeName);
                EnableVomuleJournal(pVolume);

                SetupMFTEnumInBuffer(ref medBuffer, pVolume);
                EnumerateFiles(volumeName, pVolume, medBuffer, files);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message, e);
                Exception innerException = e.InnerException;
                while (innerException != null)
                {
                    Console.WriteLine(innerException.Message, innerException);
                    innerException = innerException.InnerException;
                }
                throw new ApplicationException("Error in EnumerateVolume()", e);
            }
            finally
            {
                if (pVolume.ToInt32() != PInvokeWin32.INVALID_HANDLE_VALUE)
                {
                    PInvokeWin32.CloseHandle(pVolume);
                    if (medBuffer != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(medBuffer);
                    }
                }
            }
        }

        internal static IntPtr GetVolumeJournalHandle(string volumeName)
        {
            string vol = string.Concat("\\\\.\\", volumeName);
            IntPtr pVolume = PInvokeWin32.CreateFile(vol,
                    PInvokeWin32.GENERIC_READ | PInvokeWin32.GENERIC_WRITE,
                    PInvokeWin32.FILE_SHARE_READ | PInvokeWin32.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    PInvokeWin32.OPEN_EXISTING,
                    0,
                    IntPtr.Zero);
            if (pVolume.ToInt32() == PInvokeWin32.INVALID_HANDLE_VALUE)
            {
                throw new IOException(string.Format("CreateFile(\"{0}\") returned invalid handle", volumeName),
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }
            else
            {
                return pVolume;
            }
        }
        unsafe private static void EnableVomuleJournal(IntPtr pVolume)
        {
            UInt64 MaximumSize = 0x800000;
            UInt64 AllocationDelta = 0x100000;
            UInt32 cb;
            PInvokeWin32.CREATE_USN_JOURNAL_DATA cujd;
            cujd.MaximumSize = MaximumSize;
            cujd.AllocationDelta = AllocationDelta;

            int sizeCujd = Marshal.SizeOf(cujd);
            IntPtr cujdBuffer = Marshal.AllocHGlobal(sizeCujd);
            PInvokeWin32.ZeroMemory(cujdBuffer, sizeCujd);
            Marshal.StructureToPtr(cujd, cujdBuffer, true);

            bool fOk = PInvokeWin32.DeviceIoControl(pVolume, PInvokeWin32.FSCTL_CREATE_USN_JOURNAL,
                cujdBuffer, sizeCujd, IntPtr.Zero, 0, out cb, IntPtr.Zero);
            if (!fOk)
            {
                throw new IOException("DeviceIoControl() returned false", new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }
        unsafe internal static bool QueryUSNJournal(IntPtr pVolume, out PInvokeWin32.USN_JOURNAL_DATA ujd, out uint bytesReturned)
        {
            bool bOK = PInvokeWin32.DeviceIoControl(
                pVolume, PInvokeWin32.FSCTL_QUERY_USN_JOURNAL,
                IntPtr.Zero,
                0,
                out ujd,
                sizeof(PInvokeWin32.USN_JOURNAL_DATA),
                out bytesReturned,
                IntPtr.Zero
            );
            return bOK;
        }
        unsafe private static void SetupMFTEnumInBuffer(ref IntPtr medBuffer, IntPtr pVolume)
        {
            uint bytesReturned = 0;
            PInvokeWin32.USN_JOURNAL_DATA ujd = new PInvokeWin32.USN_JOURNAL_DATA();

            bool bOk = QueryUSNJournal(pVolume, out ujd, out bytesReturned);
            if (bOk)
            {
                PInvokeWin32.MFT_ENUM_DATA med;
                med.StartFileReferenceNumber = 0;
                med.LowUsn = 0;
                med.HighUsn = ujd.NextUsn;
                int sizeMftEnumData = Marshal.SizeOf(med);
                medBuffer = Marshal.AllocHGlobal(sizeMftEnumData);
                PInvokeWin32.ZeroMemory(medBuffer, sizeMftEnumData);
                Marshal.StructureToPtr(med, medBuffer, true);
            }
            else
            {
                throw new IOException("DeviceIoControl() returned false", new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }

        unsafe private static void EnumerateFiles(string volumeName, IntPtr pVolume, IntPtr medBuffer, Dictionary<ulong, USNRecord> files)
        {
            IntPtr pData = Marshal.AllocHGlobal(sizeof(UInt64) + 0x10000);
            PInvokeWin32.ZeroMemory(pData, sizeof(UInt64) + 0x10000);
            uint outBytesReturned = 0;

            while (false != PInvokeWin32.DeviceIoControl(pVolume, PInvokeWin32.FSCTL_ENUM_USN_DATA, medBuffer,
                                    sizeof(PInvokeWin32.MFT_ENUM_DATA), pData, sizeof(UInt64) + 0x10000, out outBytesReturned,
                                    IntPtr.Zero))
            {
                IntPtr pUsnRecord = new IntPtr(pData.ToInt32() + sizeof(Int64));
                while (outBytesReturned > 60)
                {
                    PInvokeWin32.USN_RECORD usn = new PInvokeWin32.USN_RECORD(pUsnRecord);

                    files.Add(usn.FRN,new USNRecord
                    {
                        Name = usn.FileName,
                        ParentFrn = usn.ParentFRN,
                        FRN = usn.FRN,
                        IsFolder = usn.IsFolder,
                        VolumeName = volumeName
                    });

                    pUsnRecord = new IntPtr(pUsnRecord.ToInt32() + usn.RecordLength);
                    outBytesReturned -= usn.RecordLength;
                }
                Marshal.WriteInt64(medBuffer, Marshal.ReadInt64(pData, 0));
            }
            Marshal.FreeHGlobal(pData);
        }

        internal static void FillPath(string volume, USNRecord record, MFTSearcherCache db)
        {
            if (record == null) return;
            var fdSource = db.GetVolumeRecords(volume);
            string fullpath = record.Name;
            FindRecordPath(record, ref fullpath, fdSource);
            record.FullPath = fullpath;
        }

        private static void FindRecordPath(USNRecord curRecord, ref string fullpath, Dictionary<ulong, USNRecord> fdSource)
        {
            if (curRecord.IsVolumeRoot) return;
            USNRecord nextRecord = null;
            if (!fdSource.TryGetValue(curRecord.ParentFrn, out nextRecord))
                return;
            fullpath = string.Format("{0}{1}{2}", nextRecord.Name, Path.DirectorySeparatorChar, fullpath);
            FindRecordPath(nextRecord, ref fullpath, fdSource);
        }
    }
}
