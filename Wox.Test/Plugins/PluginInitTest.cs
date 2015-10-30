using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Wox.Core.Exception;
using Wox.Core.Plugin;
using Wox.Plugin;

namespace Wox.Test.Plugins
{

    [TestFixture]
    public class PluginInitTest
    {
        [Test]
        public void PublicAPIIsNullTest()
        {
            Assert.Throws(typeof(WoxCritialException), () => PluginManager.Init(null));
        }
    }
}
