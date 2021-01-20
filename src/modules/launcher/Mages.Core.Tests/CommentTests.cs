namespace Mages.Core.Tests
{
    using Mages.Core.Source;
    using Mages.Core.Tokens;
    using NUnit.Framework;

    [TestFixture]
    public class CommentTests
    {
        [Test]
        public void CommentScannerSlash()
        {
            var source = "/";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new CommentTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.AreEqual(TokenType.RightDivide, result.Type);
        }

        [Test]
        public void CommentScannerLineComment()
        {
            var content = "This is my line";
            var source = "//" + content;
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new CommentTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.AreEqual(TokenType.Comment, result.Type);
            Assert.AreEqual(content, result.Payload);
        }

        [Test]
        public void CommentScannerBlockComment()
        {
            var content = "This is my block";
            var source = "/*" + content + "*/";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new CommentTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.AreEqual(TokenType.Comment, result.Type);
            Assert.AreEqual(content, result.Payload);
        }

        [Test]
        public void CommentScannerLineCommentMultiple()
        {
            var content = "This is my line";
            var source = "//" + content + "\n...";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new CommentTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.AreEqual(TokenType.Comment, result.Type);
            Assert.AreEqual(content, result.Payload);
        }

        [Test]
        public void CommentScannerBlockCommentMultipleLines()
        {
            var content = "This\nis\nmy\nblock";
            var source = "/*" + content + "*/";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new CommentTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.AreEqual(TokenType.Comment, result.Type);
            Assert.AreEqual(content, result.Payload);
        }

        [Test]
        public void CommentScannerBlockCommentMultiple()
        {
            var content = "This\nis\nmy\nblock";
            var source = "/*" + content + "*/\n...";
            var scanner = new StringScanner(source);
            Assert.IsTrue(scanner.MoveNext());
            var tokenizer = new CommentTokenizer();
            var result = tokenizer.Next(scanner);
            Assert.AreEqual(TokenType.Comment, result.Type);
            Assert.AreEqual(content, result.Payload);
        }

        [Test]
        public void CommentBlockAfterAndBeforeSpace()
        {
            var source = " /* foo */ ";
            var scanner = new StringScanner(source);
            var tokenizer = new GeneralTokenizer(null, null, new CommentTokenizer());
            var space1 = tokenizer.Next(scanner);
            var result = tokenizer.Next(scanner);
            var space2 = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(TokenType.Space, space1.Type);
            Assert.AreEqual(TokenType.Comment, result.Type);
            Assert.AreEqual(TokenType.Space, space2.Type);
            Assert.AreEqual(TokenType.End, end.Type);
            Assert.AreEqual(" foo ", result.Payload);
        }

        [Test]
        public void CommentLineAfterAndBeforeSpace()
        {
            var source = " // foo \n ";
            var scanner = new StringScanner(source);
            var tokenizer = new GeneralTokenizer(null, null, new CommentTokenizer());
            var space1 = tokenizer.Next(scanner);
            var result = tokenizer.Next(scanner);
            var space2 = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(TokenType.Space, space1.Type);
            Assert.AreEqual(TokenType.Comment, result.Type);
            Assert.AreEqual(TokenType.Space, space2.Type);
            Assert.AreEqual(TokenType.End, end.Type);
            Assert.AreEqual(" foo ", result.Payload);
        }
    }
}
