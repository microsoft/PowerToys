namespace Mages.Core.Tests
{
    using Mages.Core.Source;
    using Mages.Core.Tokens;
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class TokenizerTests
    {
        [Test]
        public void TokenizerEmptyCode()
        {
            var scanner = new StringScanner(String.Empty);
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var token = tokenizer.Next(scanner);
            var duplicate = tokenizer.Next(scanner);
            Assert.AreEqual(1, token.Start.Column);
            Assert.AreEqual(1, token.Start.Row);
            Assert.AreEqual(1, duplicate.Start.Column);
            Assert.AreEqual(1, duplicate.Start.Row);
            Assert.IsInstanceOf<EndToken>(token);
            Assert.IsInstanceOf<EndToken>(duplicate);
        }

        [Test]
        public void TokenizerSimpleIdentifier()
        {
            var source = "test";
            var scanner = new StringScanner(source);
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var str = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(1, str.Start.Column);
            Assert.AreEqual(1, str.Start.Row);
            Assert.AreEqual(5, end.Start.Column);
            Assert.AreEqual(1, end.Start.Row);
            Assert.IsInstanceOf<IdentToken>(str);
            Assert.AreEqual(source, str.Payload);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerSimpleStringLiteral()
        {
            var source = "test";
            var scanner = new StringScanner('"' + source + '"');
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var str = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(1, str.Start.Column);
            Assert.AreEqual(1, str.Start.Row);
            Assert.AreEqual(7, end.Start.Column);
            Assert.AreEqual(1, end.Start.Row);
            Assert.IsInstanceOf<StringToken>(str);
            Assert.AreEqual(source, str.Payload);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerEmptyStringLiteral()
        {
            var source = "";
            var scanner = new StringScanner('"' + source + '"');
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var str = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(1, str.Start.Column);
            Assert.AreEqual(1, str.Start.Row);
            Assert.AreEqual(3, end.Start.Column);
            Assert.AreEqual(1, end.Start.Row);
            Assert.IsInstanceOf<StringToken>(str);
            Assert.AreEqual(source, str.Payload);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerEscapeSequenceInStringLiteral()
        {
            var source = @"\n\f\r\b\?\0";
            var scanner = new StringScanner('"' + source + '"');
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var str = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(1, str.Start.Column);
            Assert.AreEqual(1, str.Start.Row);
            Assert.AreEqual(15, end.Start.Column);
            Assert.AreEqual(1, end.Start.Row);
            Assert.IsInstanceOf<StringToken>(str);
            Assert.AreEqual("\n\f\r\b?\0", str.Payload);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerUnicodeEscapeSequenceInStringLiteral()
        {
            var source = @"hi\u1234there";
            var scanner = new StringScanner('"' + source + '"');
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var str = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(1, str.Start.Column);
            Assert.AreEqual(1, str.Start.Row);
            Assert.AreEqual(16, end.Start.Column);
            Assert.AreEqual(1, end.Start.Row);
            Assert.IsInstanceOf<StringToken>(str);
            Assert.AreEqual("hi\u1234there", str.Payload);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerAsciiEscapeSequenceInStringLiteral()
        {
            var source = @"hi \x84 there";
            var scanner = new StringScanner('"' + source + '"');
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var str = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(1, str.Start.Column);
            Assert.AreEqual(1, str.Start.Row);
            Assert.AreEqual(16, end.Start.Column);
            Assert.AreEqual(1, end.Start.Row);
            Assert.IsInstanceOf<StringToken>(str);
            Assert.AreEqual("hi \x84 there", str.Payload);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerSingleDoubleQuoteCharacter()
        {
            var scanner = new StringScanner('"'.ToString());
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var str = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(1, str.Start.Column);
            Assert.AreEqual(1, str.Start.Row);
            Assert.AreEqual(2, end.Start.Column);
            Assert.AreEqual(1, end.Start.Row);
            Assert.IsInstanceOf<StringToken>(str);
            Assert.AreEqual("", str.Payload);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerSimpleArtihmeticPlusExpression()
        {
            var scanner = new StringScanner("2+3");
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var two = tokenizer.Next(scanner);
            var plus = tokenizer.Next(scanner);
            var three = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(1, two.Start.Column);
            Assert.AreEqual(1, two.Start.Row);
            Assert.AreEqual(2, plus.Start.Column);
            Assert.AreEqual(1, plus.Start.Row);
            Assert.AreEqual(3, three.Start.Column);
            Assert.AreEqual(1, three.Start.Row);
            Assert.AreEqual(4, end.Start.Column);
            Assert.AreEqual(1, end.Start.Row);
            Assert.IsInstanceOf<NumberToken>(two);
            Assert.IsInstanceOf<NumberToken>(three);
            Assert.IsInstanceOf<OperatorToken>(plus);
            Assert.AreEqual(2.0, ((NumberToken)two).Value);
            Assert.AreEqual(3.0, ((NumberToken)three).Value);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerSimpleArtihmeticPowerExpression()
        {
            var scanner = new StringScanner("2^.4");
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var two = tokenizer.Next(scanner);
            var power = tokenizer.Next(scanner);
            var dotfour = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(1, two.Start.Column);
            Assert.AreEqual(1, two.Start.Row);
            Assert.AreEqual(2, power.Start.Column);
            Assert.AreEqual(1, power.Start.Row);
            Assert.AreEqual(3, dotfour.Start.Column);
            Assert.AreEqual(1, dotfour.Start.Row);
            Assert.AreEqual(5, end.Start.Column);
            Assert.AreEqual(1, end.Start.Row);
            Assert.IsInstanceOf<NumberToken>(two);
            Assert.IsInstanceOf<NumberToken>(dotfour);
            Assert.IsInstanceOf<OperatorToken>(power);
            Assert.AreEqual(2.0, ((NumberToken)two).Value);
            Assert.AreEqual(0.4, ((NumberToken)dotfour).Value);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerSimpleGroupingExpression()
        {
            var scanner = new StringScanner("(2)");
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var open = tokenizer.Next(scanner);
            var two = tokenizer.Next(scanner);
            var close = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(1, open.Start.Column);
            Assert.AreEqual(1, open.Start.Row);
            Assert.AreEqual(2, two.Start.Column);
            Assert.AreEqual(1, two.Start.Row);
            Assert.AreEqual(3, close.Start.Column);
            Assert.AreEqual(1, close.Start.Row);
            Assert.AreEqual(4, end.Start.Column);
            Assert.AreEqual(1, end.Start.Row);
            Assert.IsInstanceOf<NumberToken>(two);
            Assert.AreEqual(TokenType.OpenGroup, open.Type);
            Assert.AreEqual(TokenType.CloseGroup, close.Type);
            Assert.AreEqual(2.0, ((NumberToken)two).Value);
            Assert.AreEqual("(", open.Payload);
            Assert.AreEqual(")", close.Payload);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerComplexExpression()
        {
            var scanner = new StringScanner("var a = (17 * 5 + 3 ^ x - z(i) / 3i);");
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var _var = tokenizer.Next(scanner);
            var ws1 = tokenizer.Next(scanner);
            var id = tokenizer.Next(scanner);
            var ws2 = tokenizer.Next(scanner);
            var eq = tokenizer.Next(scanner);
            var ws3 = tokenizer.Next(scanner);
            var open = tokenizer.Next(scanner);
            var seventeen = tokenizer.Next(scanner);
            var ws4 = tokenizer.Next(scanner);
            var multiply = tokenizer.Next(scanner);
            var ws5 = tokenizer.Next(scanner);
            var five = tokenizer.Next(scanner);
            var ws6 = tokenizer.Next(scanner);
            var add = tokenizer.Next(scanner);
            var ws7 = tokenizer.Next(scanner);
            var three = tokenizer.Next(scanner);
            var ws8 = tokenizer.Next(scanner);
            var power = tokenizer.Next(scanner);
            var ws9 = tokenizer.Next(scanner);
            var x = tokenizer.Next(scanner);
            var ws10 = tokenizer.Next(scanner);
            var sub = tokenizer.Next(scanner);
            var ws11 = tokenizer.Next(scanner);
            var z = tokenizer.Next(scanner);
            var cbo = tokenizer.Next(scanner);
            var i = tokenizer.Next(scanner);
            var cbc = tokenizer.Next(scanner);
            var ws12 = tokenizer.Next(scanner);
            var div = tokenizer.Next(scanner);
            var ws13 = tokenizer.Next(scanner);
            var cmplxThree = tokenizer.Next(scanner);
            var cmplxI = tokenizer.Next(scanner);
            var close = tokenizer.Next(scanner);
            var sc = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);

            Assert.AreEqual(TokenType.Keyword, _var.Type);
            Assert.AreEqual(TokenType.Space, ws1.Type);
            Assert.AreEqual(TokenType.Identifier, id.Type);
            Assert.AreEqual(TokenType.Space, ws2.Type);
            Assert.AreEqual(TokenType.Assignment, eq.Type);
            Assert.AreEqual(TokenType.Space, ws3.Type);
            Assert.AreEqual(TokenType.OpenGroup, open.Type);
            Assert.AreEqual(TokenType.Number, seventeen.Type);
            Assert.AreEqual(TokenType.Space, ws4.Type);
            Assert.AreEqual(TokenType.Multiply, multiply.Type);
            Assert.AreEqual(TokenType.Space, ws5.Type);
            Assert.AreEqual(TokenType.Number, five.Type);
            Assert.AreEqual(TokenType.Space, ws6.Type);
            Assert.AreEqual(TokenType.Add, add.Type);
            Assert.AreEqual(TokenType.Number, three.Type);
            Assert.AreEqual(TokenType.Space, ws8.Type);
            Assert.AreEqual(TokenType.Power, power.Type);
            Assert.AreEqual(TokenType.Space, ws9.Type);
            Assert.AreEqual(TokenType.Identifier, x.Type);
            Assert.AreEqual(TokenType.Space, ws10.Type);
            Assert.AreEqual(TokenType.Subtract, sub.Type);
            Assert.AreEqual(TokenType.Space, ws11.Type);
            Assert.AreEqual(TokenType.Identifier, z.Type);
            Assert.AreEqual(TokenType.OpenGroup, cbo.Type);
            Assert.AreEqual(TokenType.Identifier, i.Type);
            Assert.AreEqual(TokenType.CloseGroup, cbc.Type);
            Assert.AreEqual(TokenType.Space, ws12.Type);
            Assert.AreEqual(TokenType.RightDivide, div.Type);
            Assert.AreEqual(TokenType.Space, ws13.Type);
            Assert.AreEqual(TokenType.Number, cmplxThree.Type);
            Assert.AreEqual(TokenType.Identifier, cmplxI.Type);
            Assert.AreEqual(TokenType.CloseGroup, close.Type);
            Assert.AreEqual(TokenType.SemiColon, sc.Type);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerBrackets()
        {
            var scanner = new StringScanner("([{}])");
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var openR = tokenizer.Next(scanner);
            var openS = tokenizer.Next(scanner);
            var openC = tokenizer.Next(scanner);
            var closeC = tokenizer.Next(scanner);
            var closeS = tokenizer.Next(scanner);
            var closeR = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(TokenType.OpenGroup, openR.Type);
            Assert.AreEqual(TokenType.CloseGroup, closeR.Type);
            Assert.AreEqual(TokenType.OpenScope, openC.Type);
            Assert.AreEqual(TokenType.CloseScope, closeC.Type);
            Assert.AreEqual(TokenType.OpenList, openS.Type);
            Assert.AreEqual(TokenType.CloseList, closeS.Type);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerMergingSimple()
        {
            var scanner = new StringScanner("+=");
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var token1 = tokenizer.Next(scanner);
            var token2 = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(1, token1.Start.Column);
            Assert.AreEqual(2, token2.Start.Column);
            Assert.AreEqual(3, end.Start.Column);
            Assert.AreEqual(TokenType.Add, token1.Type);
            Assert.AreEqual(TokenType.Assignment, token2.Type);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerMergingWithout()
        {
            var scanner = new StringScanner("+ =");
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var plus = tokenizer.Next(scanner);
            var ws = tokenizer.Next(scanner);
            var assignment = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(1, plus.Start.Column);
            Assert.AreEqual(2, ws.Start.Column);
            Assert.AreEqual(3, assignment.Start.Column);
            Assert.AreEqual(4, end.Start.Column);
            Assert.AreEqual(TokenType.Add, plus.Type);
            Assert.AreEqual(TokenType.Space, ws.Type);
            Assert.AreEqual(TokenType.Assignment, assignment.Type);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerMergingDotOperator()
        {
            var scanner = new StringScanner(".*=");
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var token1 = tokenizer.Next(scanner);
            var token2 = tokenizer.Next(scanner);
            var token3 = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(1, token1.Start.Column);
            Assert.AreEqual(2, token2.Start.Column);
            Assert.AreEqual(3, token3.Start.Column);
            Assert.AreEqual(4, end.Start.Column);
            Assert.AreEqual(TokenType.Dot, token1.Type);
            Assert.AreEqual(TokenType.Multiply, token2.Type);
            Assert.AreEqual(TokenType.Assignment, token3.Type);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerVarKeyword()
        {
            var source = "var";
            var scanner = new StringScanner(source);
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var str = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(1, str.Start.Column);
            Assert.AreEqual(1, str.Start.Row);
            Assert.AreEqual(4, end.Start.Column);
            Assert.AreEqual(1, end.Start.Row);
            Assert.IsInstanceOf<IdentToken>(str);
            Assert.AreEqual(source, str.Payload);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokenizerReturnKeyword()
        {
            var source = "return";
            var scanner = new StringScanner(source);
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var str = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);
            Assert.AreEqual(1, str.Start.Column);
            Assert.AreEqual(1, str.Start.Row);
            Assert.AreEqual(7, end.Start.Column);
            Assert.AreEqual(1, end.Start.Row);
            Assert.AreEqual(TokenType.Keyword, str.Type);
            Assert.AreEqual(source, str.Payload);
            Assert.IsInstanceOf<EndToken>(end);
        }

        [Test]
        public void TokensLookAheadIsWorkingCorrectly()
        {
            var source = "a+2.3-1e5^2&&k||l<true>=false $,?c++:--d";
            var scanner = new StringScanner(source);
            var tokenizer = new GeneralTokenizer(new NumberTokenizer(), new StringTokenizer(), new CommentTokenizer());
            var a = tokenizer.Next(scanner);
            var plus = tokenizer.Next(scanner);
            var num = tokenizer.Next(scanner);
            var subtract = tokenizer.Next(scanner);
            var scientific = tokenizer.Next(scanner);
            var power = tokenizer.Next(scanner);
            var two = tokenizer.Next(scanner);
            var and = tokenizer.Next(scanner);
            var k = tokenizer.Next(scanner);
            var or = tokenizer.Next(scanner);
            var l = tokenizer.Next(scanner);
            var smaller = tokenizer.Next(scanner);
            var btrue = tokenizer.Next(scanner);
            var greaterEqual = tokenizer.Next(scanner);
            var bfalse = tokenizer.Next(scanner);
            var space = tokenizer.Next(scanner);
            var dollar = tokenizer.Next(scanner);
            var comma = tokenizer.Next(scanner);
            var condition = tokenizer.Next(scanner);
            var c = tokenizer.Next(scanner);
            var increment = tokenizer.Next(scanner);
            var colon = tokenizer.Next(scanner);
            var decrement = tokenizer.Next(scanner);
            var d = tokenizer.Next(scanner);
            var end = tokenizer.Next(scanner);

            Assert.AreEqual(TokenType.Identifier, a.Type);
            Assert.AreEqual(TokenType.Add, plus.Type);
            Assert.AreEqual(TokenType.Number, num.Type);
            Assert.AreEqual(TokenType.Subtract, subtract.Type);
            Assert.AreEqual(TokenType.Number, scientific.Type);
            Assert.AreEqual(TokenType.Power, power.Type);
            Assert.AreEqual(TokenType.Number, two.Type);
            Assert.AreEqual(TokenType.And, and.Type);
            Assert.AreEqual(TokenType.Identifier, k.Type);
            Assert.AreEqual(TokenType.Or, or.Type);
            Assert.AreEqual(TokenType.Identifier, l.Type);
            Assert.AreEqual(TokenType.Less, smaller.Type);
            Assert.AreEqual(TokenType.Keyword, btrue.Type);
            Assert.AreEqual(TokenType.GreaterEqual, greaterEqual.Type);
            Assert.AreEqual(TokenType.Keyword, bfalse.Type);
            Assert.AreEqual(TokenType.Space, space.Type);
            Assert.AreEqual(TokenType.Unknown, dollar.Type);
            Assert.AreEqual(TokenType.Comma, comma.Type);
            Assert.AreEqual(TokenType.Condition, condition.Type);
            Assert.AreEqual(TokenType.Identifier, c.Type);
            Assert.AreEqual(TokenType.Increment, increment.Type);
            Assert.AreEqual(TokenType.Colon, colon.Type);
            Assert.AreEqual(TokenType.Decrement, decrement.Type);
            Assert.AreEqual(TokenType.Identifier, d.Type);
            Assert.IsInstanceOf<EndToken>(end);
        }
    }
}
