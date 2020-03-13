using NUnit.Framework;
using Wox.Core.Plugin;
using Wox.Infrastructure.Exception;

namespace Wox.Test.Plugins
{

    [TestFixture]
    public class PluginInitTest
    {
        [Test]
        public void PublicAPIIsNullTest()
        {
            //Assert.Throws(typeof(WoxFatalException), () => PluginManager.Initialize(null));
        }
    }
}
