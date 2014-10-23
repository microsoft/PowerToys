using System;
using System.Collections.Generic;
using System.Linq;

namespace Wox.Infrastructure.MFTSearch
{
    internal class MFTSearcherCache
    {
        private Dictionary<ulong, USNRecord> records = new Dictionary<ulong, USNRecord>(100000);
        private Lookup<string, string> recordsLookup;

        public MFTSearcherCache() { }

        public void AddRecord(List<USNRecord> record)
        {
            record.ForEach(AddRecord);
        }

        public void AddRecord(USNRecord record)
        {
            if(!records.ContainsKey(record.FRN)) records.Add(record.FRN, record);
        }

        public bool DeleteRecord(ulong frn)
        {
            return records.Remove(frn);
        }

        public void UpdateRecord(USNRecord record)
        {
            USNRecord firstOrDefault = records[record.FRN];
            if (firstOrDefault != null)
            {
                firstOrDefault.Name = record.Name;
                firstOrDefault.FullPath = record.FullPath;
                firstOrDefault.VolumeName = record.VolumeName;
            }
        }


        public List<USNRecord> FindByName(string filename)
        {
            filename = filename.ToLower();
            var query = from file in records.Values
                        where file.Name.ToLower().Contains(filename)
                        select file;
            return query.ToList();
        }

        public long RecordsCount
        {
            get { return records.Count; }
        }
        
        public Dictionary<ulong, USNRecord> GetAllRecords()
        {
            return records;
        }
    }
}
