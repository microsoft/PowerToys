namespace Mages.Core.Tests
{
    using Mages.Core.Source;
    using Mages.Core.Tokens;
    using NUnit.Framework;

    [TestFixture]
    public class NumberTests
    {
        [Test]
        public void NumberScannerZero()
        {
            var source = "0";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new NumberTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<NumberToken>(result);
            Assert.AreEqual(0.0, ((NumberToken)result).Value);
        }

        [Test]
        public void NumberScannerZeroDotZero()
        {
            var source = "0.0";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new NumberTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<NumberToken>(result);
            Assert.AreEqual(0.0, ((NumberToken)result).Value);
        }

        [Test]
        public void NumberScannerTrailingDot()
        {
            var source = "3.";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new NumberTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<NumberToken>(result);
            Assert.AreEqual(3.0, ((NumberToken)result).Value);
        }

        [Test]
        public void NumberScannerScientificMinus()
        {
            var source = "1e-1";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new NumberTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<NumberToken>(result);
            Assert.AreEqual(0.1, ((NumberToken)result).Value);
        }

        [Test]
        public void NumberScannerLargeValue()
        {
            var source = "9223372036854775807";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new NumberTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<NumberToken>(result);
            Assert.AreEqual(9223372036854776000.0, ((NumberToken)result).Value);
        }

        [Test]
        public void NumberScannerScientificPlus()
        {
            var source = "1e+1";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new NumberTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<NumberToken>(result);
            Assert.AreEqual(10.0, ((NumberToken)result).Value);
        }

        [Test]
        public void NumberScannerScientificDotShouldStop()
        {
            var source = "1e1.2";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new NumberTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<NumberToken>(result);
            Assert.AreEqual(10.0, ((NumberToken)result).Value);
        }

        [Test]
        public void NumberScannerInteger()
        {
            var source = "12345678";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new NumberTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<NumberToken>(result);
            Assert.AreEqual(12345678.0, ((NumberToken)result).Value);
        }

        [Test]
        public void NumberScannerExponential()
        {
            var source = "1e2";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new NumberTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<NumberToken>(result);
            Assert.AreEqual(100.0, ((NumberToken)result).Value);
        }

        [Test]
        public void NumberScannerComplex()
        {
            var source = "5i";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new NumberTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<NumberToken>(result);
            Assert.AreEqual(5.0, ((NumberToken)result).Value);
        }

        [Test]
        public void NumberScannerFloat()
        {
            var source = "1.58";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new NumberTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<NumberToken>(result);
            Assert.AreEqual(1.58, ((NumberToken)result).Value);
        }

        [Test]
        public void NumberScannerZeroFloat()
        {
            var source = "0.012340";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new NumberTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<NumberToken>(result);
            Assert.AreEqual(0.012340, ((NumberToken)result).Value);
        }

        [Test]
        public void NumberScannerHex()
        {
            var source = "0x1a";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new NumberTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<NumberToken>(result);
            Assert.AreEqual(26, ((NumberToken)result).Value);
        }

        [Test]
        public void NumberScannerBinary()
        {
            var source = "0b01100011";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new NumberTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<NumberToken>(result);
            Assert.AreEqual(99, ((NumberToken)result).Value);
        }

        [Test]
        public void NumberScannerDotMultiply()
        {
            var source = "1.*8";
            var scanner = new StringScanner(source);
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), null, null);
            var one = tokenizer.Next(scanner);
            var mul = tokenizer.Next(scanner);
            var eight = tokenizer.Next(scanner);
            Assert.IsInstanceOf<NumberToken>(one);
            Assert.AreEqual(1, ((NumberToken)one).Value);
            Assert.AreEqual(TokenType.Multiply, mul.Type);
            Assert.IsInstanceOf<NumberToken>(eight);
            Assert.AreEqual(8, ((NumberToken)eight).Value);
        }

        [Test]
        public void NumberAfterAndBeforeSpace()
        {
            var source = " 1.2e5 ";
            var scanner = new StringScanner(source);
            var comment = new GeneralTokenizer(new NumberTokenizer(), null, null);
            var space1 = comment.Next(scanner);
            var result = comment.Next(scanner);
            var space2 = comment.Next(scanner);
            var end = comment.Next(scanner);
            Assert.AreEqual(TokenType.Space, space1.Type);
            Assert.AreEqual(TokenType.Number, result.Type);
            Assert.AreEqual(TokenType.Space, space2.Type);
            Assert.AreEqual(TokenType.End, end.Type);
            Assert.AreEqual("120000", result.Payload);
        }

        [Test]
        public void DigitsOnlyNumberWithOverflowForInt32()
        {
            var source = "0.212410080106903";
            var scanner = new StringScanner(source);
            var comment = new GeneralTokenizer(new NumberTokenizer(), null, null);
            var result = comment.Next(scanner);
            var end = comment.Next(scanner);
            Assert.AreEqual(TokenType.Number, result.Type);
            Assert.AreEqual(TokenType.End, end.Type);
            Assert.AreEqual("0.212410080106903", result.Payload);
        }

        [Test]
        public void IntegerDigitsNumberWithOverflowForInt32()
        {
            var source = "131.208072980527";
            var scanner = new StringScanner(source);
            var comment = new GeneralTokenizer(new NumberTokenizer(), null, null);
            var result = comment.Next(scanner);
            var end = comment.Next(scanner);
            Assert.AreEqual(TokenType.Number, result.Type);
            Assert.AreEqual(TokenType.End, end.Type);
            Assert.AreEqual("131.208072980527", result.Payload);
        }
    }
}
