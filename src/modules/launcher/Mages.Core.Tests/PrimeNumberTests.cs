namespace Mages.Core.Tests
{
    using Mages.Core.Runtime;
    using NUnit.Framework;

    [TestFixture]
    public class PrimeNumberTests
    {
        [Test]
        public void OneIsNotPrime()
        {
            var result = PrimeNumber.Check(1);
            Assert.False(result);
        }

        [Test]
        public void NegativeIsNotPrime()
        {
            var result = PrimeNumber.Check(-1);
            Assert.False(result);
        }

        [Test]
        public void ZeroIsNotPrime()
        {
            var result = PrimeNumber.Check(0);
            Assert.False(result);
        }

        [Test]
        public void EightIsNotPrime()
        {
            var result = PrimeNumber.Check(8);
            Assert.False(result);
        }

        [Test]
        public void ThirteenIsPrime()
        {
            var result = PrimeNumber.Check(13);
            Assert.True(result);
        }

        [Test]
        public void MediumIsPrime()
        {
            var result = PrimeNumber.Check(7919);
            Assert.True(result);
        }

        [Test]
        public void LargeIsNotPrime()
        {
            var result = PrimeNumber.Check(1373653);
            Assert.False(result);
        }

        [Test]
        public void LargeIsPrime()
        {
            var result = PrimeNumber.Check(10006721);
            Assert.True(result);
        }
    }
}
