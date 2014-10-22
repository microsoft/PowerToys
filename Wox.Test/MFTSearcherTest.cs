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
            Console.WriteLine(string.Format("{0} file indexed, {1}ms has spent.", MFTSearcher.IndexedFileCount, searchtimeend.Subtract(searchtimestart).TotalMilliseconds));

            searchtimestart = DateTime.Now;
            List<MFTSearchRecord> mftSearchRecords = MFTSearcher.Search("q");
            searchtimeend = DateTime.Now;
            Console.WriteLine(string.Format("{0} file searched, {1}ms has spent.", mftSearchRecords.Count, searchtimeend.Subtract(searchtimestart).TotalMilliseconds));

            searchtimestart = DateTime.Now;
            mftSearchRecords = MFTSearcher.Search("ss");
            searchtimeend = DateTime.Now;
            Console.WriteLine(string.Format("{0} file searched, {1}ms has spent.", mftSearchRecords.Count, searchtimeend.Subtract(searchtimestart).TotalMilliseconds));
        }
    }
}
