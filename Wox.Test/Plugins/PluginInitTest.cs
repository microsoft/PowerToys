using NUnit.Framework;
using Wox.Core.Exception;
using Wox.Core.Plugin;

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
