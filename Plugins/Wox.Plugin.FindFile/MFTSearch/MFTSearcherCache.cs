using System;
using System.Collections.Generic;
using System.Linq;

namespace Wox.Plugin.FindFile.MFTSearch
{
    internal class MFTSearcherCache
    {
        public Dictionary<string, Dictionary<ulong, USNRecord>> VolumeRecords = new Dictionary<string, Dictionary<ulong, USNRecord>>();
        public static object locker = new object();

        public MFTSearcherCache() { }

        public bool ContainsVolume(string volume)
        {
            return VolumeRecords.ContainsKey(volume);
        }

        public void AddRecord(string volume, List<USNRecord> r)
        {
            EnsureVolumeExistInHashTable(volume);
            r.ForEach(x => VolumeRecords[volume].Add(x.FRN, x));
        }

        public void AddRecord(string volume, USNRecord record)
        {
            EnsureVolumeExistInHashTable(volume);
            if (!VolumeRecords[volume].ContainsKey(record.FRN))
            {
                lock (locker)
                {
                    VolumeRecords[volume].Add(record.FRN, record);
                }
            }
        }

        public void EnsureVolumeExistInHashTable(string volume)
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
                lock (locker)
                {
                    hashtable[volume].Remove(frn);
                }
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
                lock (locker)
                {
                    source[volume][record.FRN] = record;
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        public List<USNRecord> FindByName(string filename, long maxResult = -1)
        {
            List<USNRecord> result = new List<USNRecord>();
            lock (locker)
            {
                foreach (Dictionary<ulong, USNRecord> dictionary in VolumeRecords.Values)
                {
                    foreach (var usnRecord in dictionary)
                    {
                        if (usnRecord.Value.Name.IndexOf(filename, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.Add(usnRecord.Value);
                            if (maxResult > 0 && result.Count() >= maxResult) break;
                        }
                        if (maxResult > 0 && result.Count() >= maxResult) break;
                    }
                }
            }
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
