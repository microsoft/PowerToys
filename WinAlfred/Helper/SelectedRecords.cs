using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Documents;
using WinAlfred.Plugin;

namespace WinAlfred.Helper
{
    public class SelectedRecords
    {
        private int hasAddedCount = 0;
        private Dictionary<string, int> dict = new Dictionary<string, int>();
        private string filePath = Directory.GetCurrentDirectory() + "\\selectedRecords.dat";
        private static readonly SelectedRecords instance = new SelectedRecords();

        private SelectedRecords()
        {
            LoadSelectedRecords();
        }

        public static SelectedRecords Instance
        {
            get
            {
                return instance;
            }
        }

        private void LoadSelectedRecords()
        {
            if (File.Exists(filePath))
            {
                FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                BinaryFormatter b = new BinaryFormatter();
                dict = (Dictionary<string, int>)b.Deserialize(fileStream);
                fileStream.Close();
            }

            if (dict.Count > 1000)
            {
                List<string> onlyOnceKeys = (from c in dict where c.Value == 1 select c.Key).ToList();
                foreach (string onlyOnceKey in onlyOnceKeys)
                {
                    dict.Remove(onlyOnceKey);
                }
            }
        }

        public void AddSelect(Result result)
        {
            hasAddedCount++;
            if (hasAddedCount == 10)
            {
                SaveSelectedRecords();
                hasAddedCount = 0;
            }

            if (dict.ContainsKey(result.ToString()))
            {
                dict[result.ToString()] += 1;
            }
            else
            {
                dict.Add(result.ToString(), 1);
            }
        }

        public int GetSelectedCount(Result result)
        {
            if (dict.ContainsKey(result.ToString()))
            {
                return dict[result.ToString()];
            }
            return 0;
        }

        private void SaveSelectedRecords()
        {
            FileStream fileStream = new FileStream("selectedRecords.dat", FileMode.Create);
            BinaryFormatter b = new BinaryFormatter();
            b.Serialize(fileStream, dict);
            fileStream.Close();
        }
    }
}
