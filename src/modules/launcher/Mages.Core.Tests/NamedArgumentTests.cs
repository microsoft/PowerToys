namespace Mages.Core.Tests
{
    using NUnit.Framework;

    [TestFixture]
    public class NamedArgumentTests
    {
        [Test]
        public void ShufflingTwoNamedArgumentsInSameOrderDoesNotChangeResult()
        {
            var result = "fab = (a, b) => a / b; fab2 = fab | shuffle(\"a\", \"b\"); fab2(1, 2)".Eval();
            Assert.AreEqual(0.5, result);
        }

        [Test]
        public void ShufflingTwoNamedArgumentsInReversedOrderDoesChangeResult()
        {
            var result = "fab = (a, b) => a / b; fba = fab | shuffle(\"b\", \"a\"); fba(1, 2)".Eval();
            Assert.AreEqual(2.0, result);
        }

        [Test]
        public void ShufflingTwoNamedArgumentsInReversedIsStillYieldingBareFunction()
        {
            var result = "fab = (a, b) => a / b; fba = fab | shuffle(\"b\", \"a\"); fba() == fba".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void ShufflingTwoNamedArgumentsInReversedIsStillSensitiveToCurrying()
        {
            var result = "fab = (a, b) => a / b; fba = fab | shuffle(\"b\", \"a\"); fba(2)(6)".Eval();
            Assert.AreEqual(3.0, result);
        }

        [Test]
        public void ShufflingNamedArgumentsInSameOrderDoesNotChangeDefaultValue()
        {
            var result = "fab = (a = 2, b = 4) => a / b; fab2 = fab | shuffle(\"a\", \"b\"); fab2(1)".Eval();
            Assert.AreEqual(0.25, result);
        }

        [Test]
        public void ShufflingNamedArgumentsInDifferentOrderDoesNotChangeDefaultValue()
        {
            var result = "fab = (a = 2, b = 4) => a / b; fba = fab | shuffle(\"b\", \"a\"); fba(1)".Eval();
            Assert.AreEqual(2.0, result);
        }
    }
}
