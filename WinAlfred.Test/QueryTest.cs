using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using WinAlfred.Plugin;

namespace WinAlfred.Test
{
    public class QueryTest
    {
        [Test]
        public void QueryActionTest()
        {
            Query q = new Query("this");
            Assert.AreEqual(q.ActionName,"this");

            q = new Query("ev file.txt");
            Assert.AreEqual(q.ActionName,"ev");
            Assert.AreEqual(q.ActionParameters.Count,1);
            Assert.AreEqual(q.ActionParameters[0],"file.txt");

            q = new Query("ev file.txt file2.txt");
            Assert.AreEqual(q.ActionName,"ev");
            Assert.AreEqual(q.ActionParameters.Count,2);
            Assert.AreEqual(q.ActionParameters[1],"file2.txt");
        }
    }
}
