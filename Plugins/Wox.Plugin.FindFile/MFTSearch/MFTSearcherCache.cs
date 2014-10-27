using System;
using System.Collections.Generic;
using System.Linq;

namespace Wox.Infrastructure.MFTSearch
{
    internal class MFTSearcherCache
    {
        private Dictionary<string, Dictionary<ulong, USNRecord>> _volumes_files = new Dictionary<string, Dictionary<ulong, USNRecord>>();
        private Dictionary<string, Dictionary<ulong, USNRecord>> _volumes_folders = new Dictionary<string, Dictionary<ulong, USNRecord>>();

        public MFTSearcherCache() { }

        public bool ContainsVolume(string volume)
        {
            return _volumes_files.ContainsKey(volume) && _volumes_folders.ContainsKey(volume);
        }
        public void AddRecord(string volume, List<USNRecord> r, USNRecordType type)
        {
            if (type == USNRecordType.File)
            {
                CheckHashTableKey(_volumes_files, volume);
                r.ForEach(x => _volumes_files[volume].Add(x.FRN, x));
            }
            else
            {
                CheckHashTableKey(_volumes_folders, volume);
                r.ForEach(x => _volumes_folders[volume].Add(x.FRN, x));
            }
        }
        public void AddRecord(string volume, USNRecord record, USNRecordType type)
        {
            if (type == USNRecordType.File)
            {
                CheckHashTableKey(_volumes_files, volume);
                _volumes_files[volume].Add(record.FRN, record);
            }
            else
            {
                CheckHashTableKey(_volumes_folders, volume);
                _volumes_folders[volume].Add(record.FRN, record);
            }
        }
        private void CheckHashTableKey(Dictionary<string, Dictionary<ulong, USNRecord>> hashtable, string key)
        {
            if (!hashtable.ContainsKey(key))
                hashtable.Add(key, new Dictionary<ulong, USNRecord>());
        }
        public bool DeleteRecord(string volume, ulong frn)
        {
            bool result = false;
            result = DeleteRecordHashTableItem(_volumes_files, volume, frn);
            if (!result) result = DeleteRecordHashTableItem(_volumes_folders, volume, frn);
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
        public void UpdateRecord(string volume, USNRecord record, USNRecordType type)
        {
            if (type == USNRecordType.File)
                RealUpdateRecord(volume, _volumes_files, record);
            else
                RealUpdateRecord(volume, _volumes_folders, record);
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
            var fileQuery = from filesInVolumeDic in _volumes_files.Values
                            from eachFilePair in filesInVolumeDic
                            where eachFilePair.Value.Name.ToLower().Contains(filename)
                            select eachFilePair.Value;

            var folderQuery = from fldsInVolumeDic in _volumes_folders.Values
                              from eachFldPair in fldsInVolumeDic
                              where eachFldPair.Value.Name.ToLower().Contains(filename)
                              select eachFldPair.Value;

            List<USNRecord> result = new List<USNRecord>();

            result.AddRange(fileQuery);
            result.AddRange(folderQuery);

            return result;
        }
        public USNRecord FindByFrn(string volume, ulong frn)
        {
            if ((!_volumes_files.ContainsKey(volume)) || (!_volumes_folders.ContainsKey(volume)))
                throw new Exception(string.Format("DB not contain the volume: {0}", volume));
            USNRecord result = null;
            _volumes_files[volume].TryGetValue(frn, out result);
            if (result != null) return result;
            _volumes_folders[volume].TryGetValue(frn, out result);
            return result;
        }
        public long FileCount
        {
            get { return _volumes_files.Sum(x => x.Value.Count); }
        }
        public long FolderCount
        {
            get { return _volumes_folders.Sum(x => x.Value.Count); }
        }
        public Dictionary<ulong, USNRecord> GetFolderSource(string volume)
        {
            Dictionary<ulong, USNRecord> result = null;
            _volumes_folders.TryGetValue(volume, out result);
            return result;
        }
    }
}
