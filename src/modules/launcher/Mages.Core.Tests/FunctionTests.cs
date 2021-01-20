namespace Mages.Core.Tests
{
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;

    [TestFixture]
    public class FunctionTests
    {
        [Test]
        public void LogicalFunctionsShouldYieldNumericMatrix()
        {
            var result = "isprime([3,4;5,7])".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 1.0, 0.0 }, { 1.0, 1.0 } }, (Double[,])result);
        }

        [Test]
        public void LogicalFunctionsShouldYieldBooleanValue()
        {
            var result = "isint(9)".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void TrigonometricFunctionsShouldYieldNumericVector()
        {
            var result = "sin([0, pi / 4, pi / 2])".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 0.0, Math.Sin(Math.PI / 4.0), Math.Sin(Math.PI / 2.0) } }, (Double[,])result);
        }

        [Test]
        public void TrigonometricFunctionsShouldYieldNumericValue()
        {
            var result = "cos(1)".Eval();
            Assert.AreEqual(Math.Cos(1.0), result);
        }

        [Test]
        public void ComparisonFunctionsShouldYieldNumericValue()
        {
            var result = "min(1)".Eval();
            Assert.AreEqual(1.0, result);
        }

        [Test]
        public void ComparisonFunctionsShouldReduceRowVectorToNumericValue()
        {
            var result = "max([1,2,30,4,5])".Eval();
            Assert.AreEqual(30.0, result);
        }

        [Test]
        public void ComparisonFunctionsShouldReduceColumnVectorToNumericValue()
        {
            var result = "min([1;2;3;-4;5])".Eval();
            Assert.AreEqual(-4.0, result);
        }

        [Test]
        public void ComparisonFunctionsShouldReduceMatrixToColumnVector()
        {
            var result = "min([1,2,3;3,4,5])".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 1.0 }, { 3.0 } }, (Double[,])result);
        }

        [Test]
        public void ComparisonFunctionsOfEmptyMatrixShouldBeAnEmptyMatrix()
        {
            var result = "sort([])".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { }, (Double[,])result);
        }

        [Test]
        public void CallAnUnknownFunctionShouldResultInNull()
        {
            var result = "footemp()".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void CreateMagesFunctionShouldBeClassicallyCallableWithRightTypes()
        {
            var foo = "(x, y) => x * y + y".Eval() as Function;
            var result = foo.Invoke(new Object[] { 2.0, 3.0 });
            Assert.AreEqual(9.0, result);
        }

        [Test]
        public void CreateMagesFunctionShouldNotBeClassicallyCallableWithoutRightTypes()
        {
            var foo = "(x, y) => x * y + y".Eval() as Function;
            var result = foo.Invoke(new Object[] { 2, 3 });
            Assert.IsNaN((Double)result);
        }

        [Test]
        public void CreateMagesFunctionShouldBeDirectlyCallableWithRightReturnType()
        {
            var foo = "(x, y) => x * y + y".Eval() as Function;
            var result = foo.Call<Double>(2, 3);
            Assert.AreEqual(9.0, result);
        }

        [Test]
        public void CreateMagesFunctionShouldBeDirectlyCallableWithWrongReturnType()
        {
            var foo = "(x, y) => x * y + y".Eval() as Function;
            var result = foo.Call<Boolean>(2, 3);
            Assert.AreEqual(default(Boolean), result);
        }

        [Test]
        public void CreateMagesFunctionShouldBeDirectlyCallableWithoutType()
        {
            var foo = "(x, y) => x * y + y".Eval() as Function;
            var result = foo.Call(2, 3);
            Assert.AreEqual(9.0, result);
        }

        [Test]
        public void CallStringWithValidIndexYieldsStringWithSingleCharacter()
        {
            var result = "\"test\"(2)".Eval();
            Assert.AreEqual("s", result);
        }

        [Test]
        public void CallStringWithIndexOutOfRangeYieldsNothing()
        {
            var result = "\"test\"(4)".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void CallStringWithInvalidIndexYieldsNothing()
        {
            var result = "\"test\"(\"1\")".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void CallStringWithNegativeIndexYieldsNothing()
        {
            var result = "\"test\"(-1)".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void CallObjectWithValidNameYieldsValue()
        {
            var result = "new { a: 29 }(\"a\")".Eval();
            Assert.AreEqual(29.0, result);
        }

        [Test]
        public void CallObjectWithUnknownNameYieldsNothing()
        {
            var result = "new { a: 29 }(\"b\")".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void CallObjectWithWithNonStringYieldsValue()
        {
            var result = "new { \"2\": 29 }(2)".Eval();
            Assert.AreEqual(29.0, result);
        }

        [Test]
        public void CallEmptyObjectWithUnknownNameYieldsNothing()
        {
            var result = "new { }(\"Test\")".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void CallMatrixWithSingleIntegerArgumentYieldsValue()
        {
            var result = "[1,2,3;4,5,6](4)".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void CallMatrixWithTwoIntegerArgumentsYieldsValue()
        {
            var result = "[1,2,3;4,5,6](1,1)".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void CallMatrixWithSingleOutOfBoundsArgumentYieldsNothing()
        {
            var result = "[1,2,3;4,5,6](9)".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void CallMatrixWithSecondOutOfBoundsArgumentYieldsNothing()
        {
            var result = "[1,2,3;4,5,6](1,3)".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void CallMatrixWithFirstOutOfBoundsArgumentYieldsNothing()
        {
            var result = "[1,2,3;4,5,6](3,1)".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void CallMatrixWithStringArgumentYieldsNothing()
        {
            var result = "[1,2,3;4,5,6](\"0\")".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void CallMatrixWithBooleanArgumentYieldsNothing()
        {
            var result = "[1,2,3;4,5,6](true)".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void CallFunctionWithStatementsReturningObject()
        {
            var result = "((x, y) => { var a = x + y; var b = x - y; return new { a: a, b: b}; })(2, 3)".Eval();
            var obj = result as IDictionary<String, Object>;
            Assert.IsNotNull(obj);
            Assert.AreEqual(5.0, obj["a"]);
            Assert.AreEqual(-1.0, obj["b"]);
        }

        [Test]
        public void CustomFunctionShouldBeCurried4Times()
        {
            var result = "f = (x,y,z)=>x+y^2+z^3; f()(1)(2)(3)".Eval();
            Assert.AreEqual(32.0, result);
        }

        [Test]
        public void CustomFunctionShouldBeCurriedEqualToOriginal()
        {
            var result = "f = (x,y,z)=>x+y^2+z^3; f() == f".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void CustomFunctionShouldBeCurried2Times()
        {
            var result = "f = (x,y,z)=>x+y^2+z^3; f(1)(3,3)".Eval();
            Assert.AreEqual(37.0, result);
        }

        [Test]
        public void VariableArgumentsWithImpliedArgsWithoutNaming()
        {
            var result = "f = ()=>length(args); f(1,2,3,\"hi\", true)".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void VariableArgumentsWithImpliedArgsDespiteNamedArguments()
        {
            var result = "f = (a,b)=>length(args); f(\"hi\", true)".Eval();
            Assert.AreEqual(2.0, result);
        }

        [Test]
        public void VariableArgumentsAccessWorks()
        {
            var result = "f = ()=>args(2); f(\"hi\", true, 42)".Eval();
            Assert.AreEqual(42.0, result);
        }

        [Test]
        public void VariableArgumentsNotExposedIfArgumentsNamedAccordingly()
        {
            var result = "f = (args)=>length(args); f(1, 2, 3, 4)".Eval();
            Assert.AreEqual(1.0, result);
        }

        [Test]
        public void VariableArgumentsOverwrittenIfLocalVariableExists()
        {
            var result = "f = ()=>{ var args = 1; return length(args); }; f(1, 2, 3, 4)".Eval();
            Assert.AreEqual(1.0, result);
        }

        [Test]
        public void EmptyListYieldsZeroEntries()
        {
            var result = "length(list())".Eval();
            Assert.AreEqual(0.0, result);
        }

        [Test]
        public void ListWithFourDifferentEntries()
        {
            var result = "length(list(1, true, [1,2,3], new { }))".Eval();
            Assert.AreEqual(4.0, result);
        }

        [Test]
        public void ListWithOneEntryIndexGetAccessor()
        {
            var result = "list(new { a : 5 })(0).a".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void ListWithOneEntryAddNewEntryWithIndexSetAccessor()
        {
            var result = "l = list(false); l(1) = \"foo\"; length(l)".Eval();
            Assert.AreEqual(2.0, result);
        }

        [Test]
        public void TypeOfNothingIsUndefined()
        {
            var result = "type(null)".Eval();
            Assert.AreEqual("Undefined", result);
        }

        [Test]
        public void TypeOfMatrixIsMatrix()
        {
            var result = "type([])".Eval();
            Assert.AreEqual("Matrix", result);
        }

        [Test]
        public void TypeOfDictionaryIsObject()
        {
            var result = "type(new {})".Eval();
            Assert.AreEqual("Object", result);
        }

        [Test]
        public void TypeOfStringIsString()
        {
            var result = "type(\"\")".Eval();
            Assert.AreEqual("String", result);
        }

        [Test]
        public void TypeOfBooleanIsBoolean()
        {
            var result = "type(true)".Eval();
            Assert.AreEqual("Boolean", result);
        }

        [Test]
        public void TypeOfDoubleIsNumber()
        {
            var result = "type(2.3)".Eval();
            Assert.AreEqual("Number", result);
        }

        [Test]
        public void TypeOfDelegateIsFunction()
        {
            var result = "type(() => {})".Eval();
            Assert.AreEqual("Function", result);
        }

        [Test]
        public void TypeIsCurriedForZeroArguments()
        {
            var result = "type() == type".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void RecursiveObjectShouldNotCrashJson()
        {
            var result = "x = new {}; x.y = x; json(x)".Eval();
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<String>(result);
        }

        [Test]
        public void FunctionWorkWithLexicalCaptures()
        {
            var result = "var f = () => { var a = 5; return () => a; }; var a = 3; var g = f(); g()".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void MapFunctionShouldReturnScalar()
        {
            var result = "map(x => x, 3)".Eval();
            Assert.AreEqual(3.0, result);
        }

        [Test]
        public void MapFunctionShouldReturnLengthOfEachValue()
        {
            var result = "map(length, new { a: \"hi\", b: \"foo\", c: \"here\" })".Eval() as IDictionary<String, Object>;

            Assert.IsNotNull(result);
            Assert.AreEqual(2.0, result["a"]);
            Assert.AreEqual(3.0, result["b"]);
            Assert.AreEqual(4.0, result["c"]);
        }

        [Test]
        public void MapFunctionShouldReturnLengthOfEachKey()
        {
            var result = "map((v, k) => length(k), new { eins: \"hi\", two: \"foo\", three: \"here\" })".Eval() as IDictionary<String, Object>;

            Assert.IsNotNull(result);
            Assert.AreEqual(4.0, result["eins"]);
            Assert.AreEqual(3.0, result["two"]);
            Assert.AreEqual(5.0, result["three"]);
        }

        [Test]
        public void MapFunctionShouldConvertMatrixToListObject()
        {
            var result = "map(factorial, [1, 2, 3; 4, 5, 6])".Eval() as IDictionary<String, Object>;

            Assert.IsNotNull(result);
            Assert.AreEqual(1.0, result["0"]);
            Assert.AreEqual(2.0, result["1"]);
            Assert.AreEqual(6.0, result["2"]);
            Assert.AreEqual(24.0, result["3"]);
            Assert.AreEqual(120.0, result["4"]);
            Assert.AreEqual(720.0, result["5"]);
        }

        [Test]
        public void GammaIsFactorialWithOffset()
        {
            var result = "gamma(7) - 6!".Eval();
            Assert.AreEqual(0.0, (Double)result, 1e9);
        }

        [Test]
        public void GammaIsAliasedCorrectly()
        {
            var result = "gamma() == gamma".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void SinhIsAliasedCorrectly()
        {
            var result = "sinh() == sinh".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void CoshIsAliasedCorrectly()
        {
            var result = "cosh() == cosh".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void TanhIsAliasedCorrectly()
        {
            var result = "tanh() == tanh".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void CothIsAliasedCorrectly()
        {
            var result = "coth() == coth".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void ArcothIsAliasedCorrectly()
        {
            var result = "arcoth() == arcoth".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void ArcsinIsAliasedCorrectly()
        {
            var result = "arcsin() == arcsin".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void ArsinhIsAliasedCorrectly()
        {
            var result = "arsinh() == arsinh".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void ArcoshIsAliasedCorrectly()
        {
            var result = "arcosh() == arcosh".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void ArcsecIsAliasedCorrectly()
        {
            var result = "arcsec() == arcsec".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void CscIsAliasedCorrectly()
        {
            var result = "csc() == csc".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void ArcschIsAliasedCorrectly()
        {
            var result = "arcsch() == arcsch".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void AnyWithOnlyFalseElementsIsFalse()
        {
            var result = "any(0, false, undefined)".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void AnyWithOneTrueElementsIsTrue()
        {
            var result = "any(0, false, undefined, 5)".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void AnyWithAllTrueElementsIsTrue()
        {
            var result = "any(1, true, `hello`, 5)".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void AllWithOnlyFalseElementsIsFalse()
        {
            var result = "all(0, false, undefined)".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void AllWithOneTrueElementsIsFalse()
        {
            var result = "all(0, false, undefined, 5)".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void AllWithAllTrueElementsIsTrue()
        {
            var result = "all(1, true, `hello`, 5)".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void MinWithMultipleArguments()
        {
            var result = "min(2, false, 5, -2, 9, 0)".Eval();
            Assert.AreEqual(-2.0, result);
        }

        [Test]
        public void MaxWithMultipleArguments()
        {
            var result = "max(2, true, 5, -2, 9, 0)".Eval();
            Assert.AreEqual(9.0, result);
        }

        [Test]
        public void SumWithMultipleArguments()
        {
            var result = "sum(2, true, 5, 2, -9, 0)".Eval();
            Assert.AreEqual(1.0, result);
        }

        [Test]
        public void RegexWithSimpleStringContainingNumber()
        {
            var result = "regex(`[0-9]+`, `Hello 20!`).success".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void RegexWithSimpleStringOnlyWords()
        {
            var result = "regex(`[0-9]+`, `Hello world!`).success".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void RegexCanBeChained()
        {
            var result = @"(`Hiho mum` | regex(""[A-Za-z]{4}"")).success".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void RegexIdentifierTokenMatches()
        {
            var result = "(`Hello there how are you` | regex(`[A-Za-z]+`)).matches.count".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void RegexIdentifierGroupMatchesCount()
        {
            var result = @"(`1.2.3.4` | regex(@`^([0-9]+)\.([0-9]+)\.([0-9]+)\.([0-9]+)$`)).matches(0).count".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void RegexIdentifierGroupMatchesValue()
        {
            var result = @"(`1.2.3.4` | regex(@`^([0-9]+)\.([0-9]+)\.([0-9]+)\.([0-9]+)$`)).matches(0)(1)".Eval();
            Assert.AreEqual("1", result);
        }

        [Test]
        public void RegexToCheckValidMailAddress()
        {
            var result = @"(`test@mail.com` | regex(@""^([a-zA-Z0-9_\-\.]+)@([a-zA-Z0-9_\-\.]+)\.([a-zA-Z]{2,5})$"")).success".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void RegexToCheckInvalidMailAddress()
        {
            var result = @"(`Jürgen Meier` | regex(@""^([a-zA-Z0-9_\-\.]+)@([a-zA-Z0-9_\-\.]+)\.([a-zA-Z]{2,5})$"")).success".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void ClampWithStandardMinCaseHit()
        {
            var result = "clamp(0, 5, -1)".Eval();
            Assert.AreEqual(0.0, result);
        }

        [Test]
        public void ClampWithStandardMaxCaseHit()
        {
            var result = "clamp(0, 5, 10)".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void ClampWithStandardValueCaseHit()
        {
            var result = "clamp(0, 5, 3)".Eval();
            Assert.AreEqual(3.0, result);
        }

        [Test]
        public void ClampWithoutArgumentsIsReference()
        {
            var result = "clamp() == clamp".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void ClampMinCaseHitWhenCurried()
        {
            var result = "5 | clamp(20, 30) ".Eval();
            Assert.AreEqual(20.0, result);
        }

        [Test]
        public void ClampWithStringMinCaseHit()
        {
            var result = "clamp(1, 4, \"\") ".Eval();
            Assert.AreEqual(" ", result);
        }

        [Test]
        public void ClampWithStringMaxCaseHit()
        {
            var result = "clamp(1, 3, \"florian\") ".Eval();
            Assert.AreEqual("flo", result);
        }

        [Test]
        public void ClampWithStringValueCaseHit()
        {
            var result = "clamp(1, 30, \"florian\") ".Eval();
            Assert.AreEqual("florian", result);
        }

        [Test]
        public void ClampWithMatrixAllCasesHit()
        {
            var result = "clamp(-5, 5, [1, 2; -10, -5; 10, 5])".Eval();
            CollectionAssert.AreEquivalent(new[,] { { 1.0, 2.0 }, { -5.0, -5.0 }, { 5.0, 5.0 } }, (Double[,])result);
        }

        [Test]
        public void ClipWithStringMinCaseHit()
        {
            var result = "clip(1, 4, \"\") ".Eval();
            Assert.AreEqual("    ", result);
        }

        [Test]
        public void ClipWithStringMaxCaseHit()
        {
            var result = "clip(1, 4, \"foo\") ".Eval();
            Assert.AreEqual("oo", result);
        }

        [Test]
        public void ClipWithStringNormalCaseHit()
        {
            var result = "clip(1, 4, \"foooooo\") ".Eval();
            Assert.AreEqual("oooo", result);
        }

        [Test]
        public void LerpWithInterpolationInBounds()
        {
            var result = "lerp(0, 5, 0.2)".Eval();
            Assert.AreEqual(1.0, result);
        }

        [Test]
        public void LerpWithInterpolationOutOfBounds()
        {
            var result = "lerp(0, 5, 1.2)".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void LerpWithInterpolationOfMatrix()
        {
            var result = "lerp(-5, 5, [0, 0.5, 0.75; 0.1, 0.2, 0.4])".Eval();
            CollectionAssert.AreEquivalent(new[,] { { -5.0, 0.0, 2.5 }, { -4.0, -3.0, -1.0 } }, (Double[,])result);
        }
    }
}
