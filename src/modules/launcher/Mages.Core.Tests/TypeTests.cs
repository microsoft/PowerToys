namespace Mages.Core.Tests
{
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class TypeTests
    {
        [Test]
        public void IsFunctionCanBeCurriedStringIsString()
        {
            var result = "is_string = is(\"String\"); is_string()()(\"Hi\")".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void IsFunctionCanBeCurriedNumberAintString()
        {
            var result = "is_number = is(\"Number\"); is_number()()(\"Hi\")".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void AsFunctionCanBeCurriedNumberAsString()
        {
            var result = "caster = as(\"String\"); caster()()(23.6)".Eval();
            Assert.AreEqual("23.6", result);
        }

        [Test]
        public void AsFunctionCanBeCurriedBooleanAsNumber()
        {
            var result = "cast = as(\"Number\"); cast()(true)".Eval();
            Assert.AreEqual(1.0, result);
        }

        [Test]
        public void IsFunctionStringIsString()
        {
            var result = "is(\"String\", \"Test string\") // true".Eval();
            Assert.AreEqual(true, result);
        }
                
        [Test]
        public void IsFunctionBooleanAintNumber()
        {
            var result = "is(\"Number\", true) // false".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void IsFunctionBooleanExpressionIsCurriedBoolean()
        {
            var result = "is_bool = is(\"Boolean\"); is_bool(14 ~= 7) // true".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void AsFunctionConvertsNumberToString()
        {
            var result = "as(\"String\", 3.5) // \"3.5\"".Eval();
            Assert.AreEqual("3.5", result);
        }

        [Test]
        public void AsFunctionConvertsBooleanToMatrix()
        {
            var result = "as(\"Matrix\", true) // [1]".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 1.0 } }, (Double[,])result);
        }

        [Test]
        public void AsFunctionConvertsNullToString()
        {
            var result = "as(\"String\", null)".Eval();
            Assert.AreEqual(String.Empty, result);
        }

        [Test]
        public void AsFunctionConvertsStringToNull()
        {
            var result = "as(\"Undefined\", \"Hi\")".Eval();
            Assert.AreEqual(null, result);
        }
    }
}
