using NUnit.Framework;
using Wox.Core.Updater;

namespace Wox.Test
{
    [TestFixture]
    public class SemanticVersionTest
    {
        [Test]
        public void CompareTest()
        {
            SemanticVersion v1 = new SemanticVersion(1, 1, 0);
            SemanticVersion v2 = new SemanticVersion(1, 2, 0);
            SemanticVersion v3 = new SemanticVersion(1, 1, 0);
            SemanticVersion v4 = new SemanticVersion("1.1.0");
            Assert.IsTrue(v1 < v2);
            Assert.IsTrue(v2 > v1);
            Assert.IsTrue(v1 == v3);
            Assert.IsTrue(v1.Equals(v3));
            Assert.IsTrue(v1 == v4);
        }
    }
}
