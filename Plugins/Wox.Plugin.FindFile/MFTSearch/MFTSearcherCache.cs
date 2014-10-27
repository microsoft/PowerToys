using System;
using System.Collections.Generic;
using System.Linq;

namespace Wox.Plugin.FindFile.MFTSearch
{
    internal class MFTSearcherCache
    {
        public Dictionary<string, Dictionary<ulong, USNRecord>> VolumeRecords = new Dictionary<string, Dictionary<ulong, USNRecord>>();

        public MFTSearcherCache() { }

        public bool ContainsVolume(string volume)
        {
            return VolumeRecords.ContainsKey(volume);
        }

        public void AddRecord(string volume, List<USNRecord> r)
        {
            CheckHashTableKey(volume);
            r.ForEach(x => VolumeRecords[volume].Add(x.FRN, x));
        }

        public void AddRecord(string volume, USNRecord record)
        {
            CheckHashTableKey(volume);
            VolumeRecords[volume].Add(record.FRN, record);
        }

        public void CheckHashTableKey(string volume)
        {
            if (!VolumeRecords.ContainsKey(volume))
                VolumeRecords.Add(volume, new Dictionary<ulong, USNRecord>());
        }

        public bool DeleteRecord(string volume, ulong frn)
        {
            bool result = false;
            result = DeleteRecordHashTableItem(VolumeRecords, volume, frn);
            return result;
        }

        private bool DeleteRecordHashTableItem(Dictionary<string, Dictionary<ulong, USNRecord>> hashtable, string volume, ulong frn)
        {
            if (hashtable.ContainsKey(volume) && hashtable[volume].ContainsKey(frn))
            {
                hashtable[volume].Remove(frn);
                return true;
            }
            else
            {
                return false;
            }
        }

        public void UpdateRecord(string volume, USNRecord record)
        {
            RealUpdateRecord(volume, VolumeRecords, record);
        }

        private bool RealUpdateRecord(string volume, Dictionary<string, Dictionary<ulong, USNRecord>> source, USNRecord record)
        {
            if (source.ContainsKey(volume) && source[volume].ContainsKey(record.FRN))
            {
                source[volume][record.FRN] = record;
                return true;
            }
            else
            {
                return false;
            }
        }

        public List<USNRecord> FindByName(string filename)
        {
            filename = filename.ToLower();
            var fileQuery = from filesInVolumeDic in VolumeRecords.Values
                            from eachFilePair in filesInVolumeDic
                            where eachFilePair.Value.Name.ToLower().Contains(filename)
                            select eachFilePair.Value;

            List<USNRecord> result = new List<USNRecord>();

            result.AddRange(fileQuery);

            return result;
        }

        public USNRecord FindByFrn(string volume, ulong frn)
        {
            if ((!VolumeRecords.ContainsKey(volume)))
                throw new Exception(string.Format("DB not contain the volume: {0}", volume));
            USNRecord result = null;
            VolumeRecords[volume].TryGetValue(frn, out result);
            return result;
        }

        public long RecordsCount
        {
            get { return VolumeRecords.Sum(x => x.Value.Count); }
        }

        public Dictionary<ulong, USNRecord> GetVolumeRecords(string volume)
        {
            Dictionary<ulong, USNRecord> result = null;
            VolumeRecords.TryGetValue(volume, out result);
            return result;
        }
    }
}
