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
            UrlPlugin urlPlugin = new UrlPlugin();
            Assert.IsTrue(urlPlugin.IsURL("http://www.google.com"));
            Assert.IsTrue(urlPlugin.IsURL("https://www.google.com"));
            Assert.IsTrue(urlPlugin.IsURL("http://google.com"));
            Assert.IsTrue(urlPlugin.IsURL("www.google.com"));
            Assert.IsTrue(urlPlugin.IsURL("google.com"));
            Assert.IsTrue(urlPlugin.IsURL("http://localhost"));
            Assert.IsTrue(urlPlugin.IsURL("https://localhost"));
            Assert.IsTrue(urlPlugin.IsURL("http://localhost:80"));
            Assert.IsTrue(urlPlugin.IsURL("https://localhost:80"));
            Assert.IsTrue(urlPlugin.IsURL("http://110.10.10.10"));
            Assert.IsTrue(urlPlugin.IsURL("110.10.10.10"));
            Assert.IsTrue(urlPlugin.IsURL("ftp://110.10.10.10"));


            Assert.IsFalse(urlPlugin.IsURL("wwww"));
            Assert.IsFalse(urlPlugin.IsURL("wwww.c"));
            Assert.IsFalse(urlPlugin.IsURL("wwww.c"));
        }
    }
}
