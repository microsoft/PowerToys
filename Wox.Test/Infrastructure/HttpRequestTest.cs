using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Wox.Infrastructure;
using Wox.Infrastructure.Http;

namespace Wox.Test.Infrastructure
{
    [TestFixture]
    public class HttpRequestTest
    {
        [Test]
        public void RequestTest()
        {
            string results = HttpRequest.Get("https://www.bing.com");
            Assert.IsNotNullOrEmpty(results);
        }
    }
}
