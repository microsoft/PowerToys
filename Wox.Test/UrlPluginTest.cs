using NUnit.Framework;
using Wox.Plugin.Url;

namespace Wox.Test
{
    [TestFixture]
    public class UrlPluginTest
    {
        [Test]
        public void URLMatchTest()
        {
            var plugin = new Main();
            Assert.IsTrue(plugin.IsURL("http://www.google.com"));
            Assert.IsTrue(plugin.IsURL("https://www.google.com"));
            Assert.IsTrue(plugin.IsURL("http://google.com"));
            Assert.IsTrue(plugin.IsURL("www.google.com"));
            Assert.IsTrue(plugin.IsURL("google.com"));
            Assert.IsTrue(plugin.IsURL("http://localhost"));
            Assert.IsTrue(plugin.IsURL("https://localhost"));
            Assert.IsTrue(plugin.IsURL("http://localhost:80"));
            Assert.IsTrue(plugin.IsURL("https://localhost:80"));
            Assert.IsTrue(plugin.IsURL("http://110.10.10.10"));
            Assert.IsTrue(plugin.IsURL("110.10.10.10"));
            Assert.IsTrue(plugin.IsURL("ftp://110.10.10.10"));


            Assert.IsFalse(plugin.IsURL("wwww"));
            Assert.IsFalse(plugin.IsURL("wwww.c"));
            Assert.IsFalse(plugin.IsURL("wwww.c"));
        }
    }
}
