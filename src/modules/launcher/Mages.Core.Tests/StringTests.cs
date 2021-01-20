namespace Mages.Core.Tests
{
    using Mages.Core.Source;
    using Mages.Core.Tokens;
    using NUnit.Framework;

    [TestFixture]
    public class StringTests
    {
        [Test]
        public void StringScannerEmpty()
        {
            var source = "";
            var scanner = new StringScanner('"' + source + '"');
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new StringTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<StringToken>(result);
            Assert.AreEqual(source, result.Payload);
        }

        [Test]
        public void StringScannerHallo()
        {
            var source = "Hallo";
            var scanner = new StringScanner('"' + source + '"');
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new StringTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<StringToken>(result);
            Assert.AreEqual(source, result.Payload);
        }

        [Test]
        public void StringScannerUnicodeCharacterHeart()
        {
            var source = "\\u2764";
            var scanner = new StringScanner('"' + source + '"');
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new StringTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<StringToken>(result);
            Assert.AreEqual("❤", result.Payload);
        }

        [Test]
        public void StringScannerUnicodeCharacterInfinity()
        {
            var source = "\\u221e";
            var scanner = new StringScanner('"' + source + '"');
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new StringTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<StringToken>(result);
            Assert.AreEqual("∞", result.Payload);
        }

        [Test]
        public void StringScannerAsciiCharacterTilde()
        {
            var source = "\\x7e";
            var scanner = new StringScanner('"' + source + '"');
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new StringTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.IsInstanceOf<StringToken>(result);
            Assert.AreEqual("~", result.Payload);
        }

        [Test]
        public void StringAfterAndBeforeSpace()
        {
            var source = " \"foo\" ";
            var scanner = new StringScanner(source);
            var tokenizer = new GeneralTokenizer(null, new StringTokenizer(), null);
            var space1 = tokenizer.Next(scanner);
            var result = tokenizer.Next(scanner);
            var space2 = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(TokenType.Space, space1.Type);
            Assert.AreEqual(TokenType.String, result.Type);
            Assert.AreEqual(TokenType.Space, space2.Type);
            Assert.AreEqual(TokenType.End, end.Type);
            Assert.AreEqual("foo", result.Payload);
        }
    }
}
