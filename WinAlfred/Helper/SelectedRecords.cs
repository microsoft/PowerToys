using WinAlfred.Plugin;

namespace WinAlfred.Helper
{
    public class SelectedRecords
    {
        private int hasAddedCount = 0;

        public void LoadSelectedRecords()
        {
            
        }

        public void AddSelect(Result result)
        {
            hasAddedCount++;
            if (hasAddedCount == 10)
            {
                SaveSelectedRecords();
                hasAddedCount = 0;
            }




        }

        public int GetSelectedCount(Result result)
        {
            return 0;
        }

        public void SaveSelectedRecords()
        {
            
        }
    }
}
