namespace Mages.Core.Tests
{
    using NUnit.Framework;

    [TestFixture]
    public class StringLiteralTests
    {
        [Test]
        public void StandardLiteralStringDoesNotEscapeTab()
        {
            var result = "@\"hi\\tthere\"".Eval();
            Assert.AreEqual("hi\\tthere", result);
        }

        [Test]
        public void StandardLiteralStringDoesNotEscapeNewline()
        {
            var result = "@\"hi\\nthere\"".Eval();
            Assert.AreEqual("hi\\nthere", result);
        }

        [Test]
        public void StandardLiteralStringDoesIncludeNewline()
        {
            var result = "@\"hi\nthere\"".Eval();
            Assert.AreEqual("hi\nthere", result);
        }

        [Test]
        public void StandardLiteralStringDoesNotEscapeBackslash()
        {
            var result = "@\"hi\\\\there\"".Eval();
            Assert.AreEqual("hi\\\\there", result);
        }

        [Test]
        public void StandardLiteralStringCanEscapeDoubleQuotationMark()
        {
            var result = "@\"hi \"\"there\"\"!\"".Eval();
            Assert.AreEqual("hi \"there\"!", result);
        }

        [Test]
        public void StandardLiteralStringDoesNotEncodeHex()
        {
            var result = "@\"hi\\x32there\"".Eval();
            Assert.AreEqual("hi\\x32there", result);
        }

        [Test]
        public void StandardLiteralStringDoesNotEncodeUnicode()
        {
            var result = "@\"hi\\u3242there\"".Eval();
            Assert.AreEqual("hi\\u3242there", result);
        }

        [Test]
        public void InterpolatedLiteralStringDoesNotEscapeTab()
        {
            var result = "@`hi\\tthere`".Eval();
            Assert.AreEqual("hi\\tthere", result);
        }

        [Test]
        public void InterpolatedLiteralStringDoesNotEscapeNewline()
        {
            var result = "@`hi\\nthere`".Eval();
            Assert.AreEqual("hi\\nthere", result);
        }

        [Test]
        public void InterpolatedLiteralStringDoesIncludeNewline()
        {
            var result = "@`hi\nthere`".Eval();
            Assert.AreEqual("hi\nthere", result);
        }

        [Test]
        public void InterpolatedLiteralStringDoesNotEscapeBackslash()
        {
            var result = "@`hi\\\\there`".Eval();
            Assert.AreEqual("hi\\\\there", result);
        }

        [Test]
        public void InterpolatedLiteralStringCanEscapeCurvedQuotationMark()
        {
            var result = "@`hi ``there``!`".Eval();
            Assert.AreEqual("hi `there`!", result);
        }

        [Test]
        public void InterpolatedLiteralStringCanStillYieldBinaryExpressions()
        {
            var result = "@`hi ``{2+3}``!`".Eval();
            Assert.AreEqual("hi `5`!", result);
        }

        [Test]
        public void InterpolatedLiteralStringCanStillYieldStringExpressions()
        {
            var result = "@`hi ``{\"Tom\"}``!`".Eval();
            Assert.AreEqual("hi `Tom`!", result);
        }
    }
}
