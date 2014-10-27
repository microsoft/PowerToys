using System;

namespace Wox.Plugin.FindFile.MFTSearch
{
    public class USNRecord
    {

        public string Name { get; set; }
        public ulong FRN { get; set; }
        public UInt64 ParentFrn { get; set; }
        public string FullPath { get; set; }
        public bool IsVolumeRoot { get; set; }
        public bool IsFolder { get; set; }
        public string VolumeName { get; set; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(FullPath) ? Name : FullPath;
        }

        public static USNRecord ParseUSN(string volume, PInvokeWin32.USN_RECORD usn)
        {
            return new USNRecord
            {
                FRN = usn.FRN,
                Name = usn.FileName,
                ParentFrn = usn.ParentFRN,
                IsFolder = usn.IsFolder,
                VolumeName = volume
            };
        }
    }
}
