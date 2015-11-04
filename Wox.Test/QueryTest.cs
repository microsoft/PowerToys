using NUnit.Framework;
using Wox.Core.Plugin;
using Wox.Plugin;

namespace Wox.Test
{
    public class QueryTest
    {
        [Test]
        [Ignore("Current query is tightly integrated with GUI, can't be tested.")]
        public void ExclusivePluginQueryTest()
        {
            Query q = PluginManager.QueryInit("> file.txt file2 file3");

            Assert.AreEqual(q.FirstSearch, "file.txt");
            Assert.AreEqual(q.SecondSearch, "file2");
            Assert.AreEqual(q.ThirdSearch, "file3");
            Assert.AreEqual(q.SecondToEndSearch, "file2 file3");
        }

        [Test]
        [Ignore("Current query is tightly integrated with GUI, can't be tested.")]
        public void GenericPluginQueryTest()
        {
            Query q = PluginManager.QueryInit("file.txt file2 file3");

            Assert.AreEqual(q.FirstSearch, "file.txt");
            Assert.AreEqual(q.SecondSearch, "file2");
            Assert.AreEqual(q.ThirdSearch, "file3");
            Assert.AreEqual(q.SecondToEndSearch, "file2 file3");
        }
    }
}
