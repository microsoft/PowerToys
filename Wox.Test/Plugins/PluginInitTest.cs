using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
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
        public void CouldNotFindUserProfileTest()
        {
            var api = new Mock<IPublicAPI>();
            Environment.SetEnvironmentVariable("USERPROFILE", "");
            Assert.Throws(typeof(WoxCritialException), () => PluginManager.Init(api.Object));
        }

        [Test]
        public void PublicAPIIsNullTest()
        {
            Assert.Throws(typeof(WoxCritialException), () => PluginManager.Init(null));
        }
    }
}
