namespace Mages.Core.Tests
{
    using NUnit.Framework;

    [TestFixture]
    public class InterpolatedStringTests
    {
        [Test]
        public void UsingInterpolatedStringWithoutPlaceholders()
        {
            var result = "`this is some test\\r\\nfoo`".Eval();
            Assert.AreEqual("this is some test\r\nfoo", result);
        }

        [Test]
        public void UsingInterpolatedStringWithSimplePlaceholder()
        {
            var result = "x = 5; `five is {x}\\r\\nfoo`".Eval();
            Assert.AreEqual("five is 5\r\nfoo", result);
        }

        [Test]
        public void UsingInterpolatedStringWithMultiplePlaceholders()
        {
            var result = "x = 5; y = 7; z = 9; `five {x} seven {y} nine {z}`".Eval();
            Assert.AreEqual("five 5 seven 7 nine 9", result);
        }

        [Test]
        public void StartingExpressionWithAnInterpolatedStringNotEscapingBrackets()
        {
            var result = @"`{2+3}`".Eval();
            Assert.AreEqual("5", result);
        }

        [Test]
        public void StartingExpressionWithAnInterpolatedStringEscapingBrackets()
        {
            var result = @"`\{2+3\}`".Eval();
            Assert.AreEqual("{2+3}", result);
        }

        [Test]
        public void StartingExpressionWithALiteralInterpolatedStringNotEscapingBrackets()
        {
            var result = @"@`{2+3}`".Eval();
            Assert.AreEqual("5", result);
        }

        [Test]
        public void StartingExpressionWithALiteralInterpolatedStringEscapingBrackets()
        {
            var result = @"@`{{2+3}}`".Eval();
            Assert.AreEqual("{2+3}", result);
        }

        [Test]
        public void StartingExpressionWithAnInterpolatedStringMixingEscapingAndNotEscapingBrackets()
        {
            var result = @"`\{ The Result is {2+3}\}, right \{{sin(0)}\}`".Eval();
            Assert.AreEqual("{ The Result is 5}, right {0}", result);
        }

        [Test]
        public void UsingInterpolatedStringWithMultipleReversedPlaceholders()
        {
            var result = "x = 5; y = 7; z = 9; `five not {z} seven not {x} nine not {y}`".Eval();
            Assert.AreEqual("five not 9 seven not 5 nine not 7", result);
        }

        [Test]
        public void UsingInterpolatedStringWithComputedPlaceholder()
        {
            var result = "`five is {2 + 3}\\r\\nfoo`".Eval();
            Assert.AreEqual("five is 5\r\nfoo", result);
        }

        [Test]
        public void UsingInterpolatedStringWithMemberPlaceholder()
        {
            var result = "`five is {new { a: 5 }.a}\\r\\nfoo`".Eval();
            Assert.AreEqual("five is 5\r\nfoo", result);
        }

        [Test]
        public void UsingInterpolatedStringWithFunctionCallPlaceholder()
        {
            var result = "`five is {add(2, 3)}\\r\\nfoo`".Eval();
            Assert.AreEqual("five is 5\r\nfoo", result);
        }

        [Test]
        public void UsingInterpolatedStringWithCurriedFunctionCallPlaceholder()
        {
            var result = "`five is {mul(2.5)(2)}\\r\\nfoo`".Eval();
            Assert.AreEqual("five is 5\r\nfoo", result);
        }

        [Test]
        public void UsingInterpolatedStringWithMatrixPlaceholder()
        {
            var result = "`five is {[5]}\\r\\nfoo`".Eval();
            Assert.AreEqual("five is [5]\r\nfoo", result);
        }
    }
}
