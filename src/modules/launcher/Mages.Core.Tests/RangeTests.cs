namespace Mages.Core.Tests
{
    using Mages.Core.Runtime;
    using NUnit.Framework;

    [TestFixture]
    public class RangeTests
    {
        [Test]
        public void PositiveRangeWithStep()
        {
            var range = Range.Create(1, 10, 1);
            Assert.AreEqual(10, range.GetCount());
            Assert.AreEqual(1.0, range[0, 0]);
            Assert.AreEqual(10.0, range[0, 9]);
        }

        [Test]
        public void OddPositiveRangeWithStep()
        {
            var range = Range.Create(1.5, 10.5, 2.3);
            Assert.AreEqual(4, range.GetCount());
            Assert.AreEqual(1.5, range[0, 0]);
            Assert.AreEqual(8.4, range[0, 3], 1e-4);
        }

        [Test]
        public void PositiveRangeWithoutStep()
        {
            var range = Range.Create(1, 10);
            Assert.AreEqual(10, range.GetCount());
            Assert.AreEqual(1.0, range[0, 0]);
            Assert.AreEqual(10.0, range[0, 9]);
        }

        [Test]
        public void OddPositiveRangeWithoutStep()
        {
            var range = Range.Create(1.5, 10.1);
            Assert.AreEqual(9, range.GetCount());
            Assert.AreEqual(1.5, range[0, 0]);
            Assert.AreEqual(9.5, range[0, 8]);
        }

        [Test]
        public void NegativeRangeWithStep()
        {
            var range = Range.Create(-1, -10, -1);
            Assert.AreEqual(10, range.GetCount());
            Assert.AreEqual(-1.0, range[0, 0]);
            Assert.AreEqual(-10.0, range[0, 9]);
        }

        [Test]
        public void NegativeRangeWithoutStep()
        {
            var range = Range.Create(-1, -10);
            Assert.AreEqual(10, range.GetCount());
            Assert.AreEqual(-1.0, range[0, 0]);
            Assert.AreEqual(-10.0, range[0, 9]);
        }

        [Test]
        public void EmptyRangeWithStep()
        {
            var range = Range.Create(-1, 10, -1);
            Assert.AreEqual(0, range.GetCount());
        }
    }
}
