using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Wox.Plugin.FindFile.MFTSearch
{
    internal class VolumeMonitor
    {
        public Action<USNRecord> RecordAddedEvent;
        public Action<USNRecord> RecordDeletedEvent;
        public Action<USNRecord, USNRecord> RecordRenameEvent;

        public void Monitor(List<string> volumes, MFTSearcherCache db)
        {
            foreach (var volume in volumes)
            {
                if (string.IsNullOrEmpty(volume)) throw new InvalidOperationException("Volume cant't be null or empty string.");
                if (!db.ContainsVolume(volume)) throw new InvalidOperationException(string.Format("Volume {0} must be scaned first."));
                Thread th = new Thread(new ParameterizedThreadStart(MonitorThread));
                th.Start(new Dictionary<string, object> { { "Volume", volume }, { "MFTSearcherCache", db } });
            }
        }
        private PInvokeWin32.READ_USN_JOURNAL_DATA SetupInputData4JournalRead(string volume, uint reason)
        {
            IntPtr pMonitorVolume = MFTSearcher.GetVolumeJournalHandle(volume);
            uint bytesReturned = 0;
            PInvokeWin32.USN_JOURNAL_DATA ujd = new PInvokeWin32.USN_JOURNAL_DATA();
            MFTSearcher.QueryUSNJournal(pMonitorVolume, out ujd, out bytesReturned);

            // 构建输入参数
            PInvokeWin32.READ_USN_JOURNAL_DATA rujd = new PInvokeWin32.READ_USN_JOURNAL_DATA();
            rujd.StartUsn = ujd.NextUsn;
            rujd.ReasonMask = reason;
            rujd.ReturnOnlyOnClose = 1;
            rujd.Timeout = 0;
            rujd.BytesToWaitFor = 1;
            rujd.UsnJournalID = ujd.UsnJournalID;

            return rujd;
        }
        private void MonitorThread(object param)
        {

            MFTSearcherCache db = (param as Dictionary<string, object>)["MFTSearcherCache"] as MFTSearcherCache;
            string volume = (param as Dictionary<string, object>)["Volume"] as string;
            IntPtr pbuffer = Marshal.AllocHGlobal(0x1000);
            PInvokeWin32.READ_USN_JOURNAL_DATA rujd = SetupInputData4JournalRead(volume, 0xFFFFFFFF);
            UInt32 cbRead;
            IntPtr prujd;

            while (true)
            {
                prujd = Marshal.AllocHGlobal(Marshal.SizeOf(rujd));
                PInvokeWin32.ZeroMemory(prujd, Marshal.SizeOf(rujd));
                Marshal.StructureToPtr(rujd, prujd, true);

                Debug.WriteLine(string.Format("\nMoniting on {0}......", volume));
                IntPtr pVolume = MFTSearcher.GetVolumeJournalHandle(volume);

                bool fok = PInvokeWin32.DeviceIoControl(pVolume,
                    PInvokeWin32.FSCTL_READ_USN_JOURNAL,
                    prujd, Marshal.SizeOf(typeof(PInvokeWin32.READ_USN_JOURNAL_DATA)),
                    pbuffer, 0x1000, out cbRead, IntPtr.Zero);

                IntPtr pRealData = new IntPtr(pbuffer.ToInt32() + Marshal.SizeOf(typeof(Int64)));
                uint offset = 0;

                if (fok)
                {
                    while (offset + Marshal.SizeOf(typeof(Int64)) < cbRead)
                    {
                        PInvokeWin32.USN_RECORD usn = new PInvokeWin32.USN_RECORD(new IntPtr(pRealData.ToInt32() + (int)offset));
                        ProcessUSN(usn, volume, db);
                        offset += usn.RecordLength;
                    }
                }

                Marshal.FreeHGlobal(prujd);
                rujd.StartUsn = Marshal.ReadInt64(pbuffer);
            }
        }
        private void ProcessUSN(PInvokeWin32.USN_RECORD usn, string volume, MFTSearcherCache db)
        {
            var dbCached = db.FindByFrn(volume, usn.FRN);
            MFTSearcher.FillPath(volume, dbCached, db);
            Debug.WriteLine(string.Format("------USN[frn={0}]------", usn.FRN));
            Debug.WriteLine(string.Format("FileName={0}, USNChangeReason={1}", usn.FileName, USNChangeReason.ReasonPrettyFormat(usn.Reason)));
            Debug.WriteLine(string.Format("FileName[Cached]={0}", dbCached == null ? "NoCache" : dbCached.FullPath));
            Debug.WriteLine("--------------------------------------");

            if (MaskEqual(usn.Reason, USNChangeReason.USN_REASONS["USN_REASON_RENAME_NEW_NAME"]))
                ProcessRenameNewName(usn, volume, db);
            if ((usn.Reason & USNChangeReason.USN_REASONS["USN_REASON_FILE_CREATE"]) != 0)
                ProcessFileCreate(usn, volume, db);
            if (MaskEqual(usn.Reason, USNChangeReason.USN_REASONS["USN_REASON_FILE_DELETE"]))
                ProcessFileDelete(usn, volume, db);
        }
        private void ProcessFileDelete(PInvokeWin32.USN_RECORD usn, string volume, MFTSearcherCache db)
        {
            var cached = db.FindByFrn(volume, usn.FRN);
            if (cached == null)
            {
                return;
            }
            else
            {
                MFTSearcher.FillPath(volume, cached, db);
                var deleteok = db.DeleteRecord(volume, usn.FRN);
                Debug.WriteLine(string.Format(">>>> File {0} deleted {1}.", cached.FullPath, deleteok ? "successful" : "fail"));
                if (RecordDeletedEvent != null)
                    RecordDeletedEvent(cached);
            }
        }
        private void ProcessRenameNewName(PInvokeWin32.USN_RECORD usn, string volume, MFTSearcherCache db)
        {
            USNRecord newRecord = USNRecord.ParseUSN(volume, usn);
            //string fullpath = newRecord.Name;
            //db.FindRecordPath(newRecord, ref fullpath, db.GetVolumeRecords(volume));
            //newRecord.FullPath = fullpath;
            var oldRecord = db.FindByFrn(volume, usn.FRN);
            MFTSearcher.FillPath(volume, oldRecord, db);
            MFTSearcher.FillPath(volume, newRecord, db);
            Debug.WriteLine(string.Format(">>>> RenameFile {0} to {1}", oldRecord.FullPath, newRecord.FullPath));
            db.UpdateRecord(volume, newRecord);
            if (RecordRenameEvent != null) RecordRenameEvent(oldRecord, newRecord);
            if (newRecord.FullPath.Contains("$RECYCLE.BIN"))
            {
                Debug.WriteLine(string.Format(">>>> Means {0} moved to recycle.", oldRecord.FullPath));
            }
        }
        private void ProcessFileCreate(PInvokeWin32.USN_RECORD usn, string volume, MFTSearcherCache db)
        {
            USNRecord record = USNRecord.ParseUSN(volume, usn);
            //string fullpath = record.Name;
            //db.FindRecordPath(record, ref fullpath, db.GetVolumeRecords(volume));
            //record.FullPath = fullpath;
            db.AddRecord(volume, record);
            MFTSearcher.FillPath(volume, record, db);
            Debug.WriteLine(string.Format(">>>> NewFile: {0}", record.FullPath));
            if (RecordAddedEvent != null)
                RecordAddedEvent(record);
        }


        private bool MaskEqual(uint target, uint compare)
        {
            return (target & compare) != 0;
        }
    }
}
