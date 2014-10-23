using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Wox.Infrastructure.MFTSearch;

namespace Wox.Test
{
    [TestFixture]
    public class MFTSearcherTest
    {
        [Test]
        public void MatchTest()
        {
            var searchtimestart = DateTime.Now;
            MFTSearcher.IndexAllVolumes();
            var searchtimeend = DateTime.Now;
            Console.WriteLine(string.Format("{0} file indexed, {1}ms has spent.", MFTSearcher.IndexedRecordsCount, searchtimeend.Subtract(searchtimestart).TotalMilliseconds));

            searchtimestart = DateTime.Now;
            List<MFTSearchRecord> mftSearchRecords = MFTSearcher.Search("q");
            searchtimeend = DateTime.Now;
            Console.WriteLine(string.Format("{0} file searched, {1}ms has spent.", mftSearchRecords.Count, searchtimeend.Subtract(searchtimestart).TotalMilliseconds));

            searchtimestart = DateTime.Now;
            mftSearchRecords = MFTSearcher.Search("ss");
            searchtimeend = DateTime.Now;
            Console.WriteLine(string.Format("{0} file searched, {1}ms has spent.", mftSearchRecords.Count, searchtimeend.Subtract(searchtimestart).TotalMilliseconds));
        }

        [Test]
        public void MemoryTest()
        {
            long oldWorkingSet = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            MFTSearcher.IndexAllVolumes();
            long newWorkingSet = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            Console.WriteLine(string.Format("Index: {0}M", (newWorkingSet - oldWorkingSet)/(1024*1024)));

            oldWorkingSet = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            MFTSearcher.Search("q");
            newWorkingSet = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            Console.WriteLine(string.Format("Search: {0}M", (newWorkingSet - oldWorkingSet) / (1024 * 1024)));
        }
    }
}
