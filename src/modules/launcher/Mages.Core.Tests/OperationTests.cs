namespace Mages.Core.Tests
{
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class OperationTests
    {
        [Test]
        public void BinaryAddWithNumbersYieldsNumber()
        {
            var result = "2 + 3".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void BinaryAddWithBooleansYieldsNumber()
        {
            var result = "true + false".Eval();
            Assert.AreEqual(1.0, result);
        }

        [Test]
        public void BinaryAddWithBooleanAndNumberYieldsNumber()
        {
            var result = "true + 3".Eval();
            Assert.AreEqual(4.0, result);
        }

        [Test]
        public void BinarySubtractWithBooleanAndNumberYieldsNumber()
        {
            var result = "4 - false".Eval();
            Assert.AreEqual(4.0, result);
        }

        [Test]
        public void BinaryMultiplyWithBooleanAndNumberYieldsNumber()
        {
            var result = "3 * true".Eval();
            Assert.AreEqual(3.0, result);
        }

        [Test]
        public void BinaryDivisionWithBooleanAndNumberYieldsNumber()
        {
            var result = "false / 5".Eval();
            Assert.AreEqual(0.0, result);
        }

        [Test]
        public void BinaryPowerWithBooleanAndNumberYieldsNumber()
        {
            var result = "2^true".Eval();
            Assert.AreEqual(2.0, result);
        }

        [Test]
        public void BinaryAddWithNumberAndUnknownVariableYieldsNull()
        {
            var result = "2 + a".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void BinaryAddWithUnknownVariablesYieldsNull()
        {
            var result = "a + b".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void BinaryPowerWithNumbersYieldsNumber()
        {
            var result = "2^3".Eval();
            Assert.AreEqual(8.0, result);
        }

        [Test]
        public void BinarySubtractWithNumbersYieldsNumber()
        {
            var result = "2 - 3.5".Eval();
            Assert.AreEqual(-1.5, result);
        }

        [Test]
        public void BinaryMultiplyWithNumbersYieldsNumber()
        {
            var result = "2.5 * 1.5".Eval();
            Assert.AreEqual(3.75, result);
        }

        [Test]
        public void BinaryDivideWithNumbersYieldsNumber()
        {
            var result = "4 / 2".Eval();
            Assert.AreEqual(2.0, result);
        }

        [Test]
        public void BinaryAddWithMatricessYieldsMatrix()
        {
            var result = "[1,2;3,4]+[4,3;2,1]".Eval();
            CollectionAssert.AreEqual(new Double[,] { { 5, 5 }, { 5, 5 } }, (Double[,])result);
        }

        [Test]
        public void BinarySubtractWithMatricesYieldsMatrix()
        {
            var result = "[3,2,1]-[1,0,-1]".Eval();
            CollectionAssert.AreEqual(new Double[,] { { 2, 2, 2 } }, (Double[,])result);
        }

        [Test]
        public void BinaryMultiplyWithMatricesYieldsMatrix()
        {
            var result = "[1,2;3,4]*[3;5]".Eval();
            CollectionAssert.AreEqual(new Double[,] { { 13 }, { 29 } }, (Double[,])result);
        }

        [Test]
        public void BinaryAndWithNumbersYieldsBoolean()
        {
            var result = "2 && 3".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void BinaryAndWithBooleansYieldsBoolean()
        {
            var result = "true && false".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void BinaryOrWithBooleansYieldsBoolean()
        {
            var result = "true || false".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void BinaryAndWithMatricesYieldsMatrix()
        {
            var result = "[1,0;0,0] && [1,1;1,0]".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 1.0, 0.0 }, { 0.0, 0.0 } }, (Double[,])result);
        }

        [Test]
        public void BinaryAndWithMatrixAndDoubleYieldsMatrix()
        {
            var result = "[1,0;0,0] && 1".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 1.0, 0.0 }, { 0.0, 0.0 } }, (Double[,])result);
        }

        [Test]
        public void BinaryAndWithMatrixAndBooleanYieldsMatrix()
        {
            var result = "[1,0;0,0] && true".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 1.0, 0.0 }, { 0.0, 0.0 } }, (Double[,])result);
        }

        [Test]
        public void BinaryOrWithMatricesYieldsMatrix()
        {
            var result = "[1,0;0,0] || [1,1;1,0]".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 1.0, 1.0 }, { 1.0, 0.0 } }, (Double[,])result);
        }

        [Test]
        public void BinaryEqWithMatrixAndDoubleYieldsMatrix()
        {
            var result = "[1,2;3,4] == 3".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 0.0, 0.0 }, { 1.0, 0.0 } }, (Double[,])result);
        }

        [Test]
        public void BinaryEqWithMatricesYieldsMatrix()
        {
            var result = "[1,2;3,4] == [2,3;4,4]".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 0.0, 0.0 }, { 0.0, 1.0 } }, (Double[,])result);
        }

        [Test]
        public void BinaryEqWithNumbersYieldsBoolean()
        {
            var result = "2 == 3".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void BinaryNeqWithNumbersYieldsBoolean()
        {
            var result = "2 ~= 3".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void BinaryNeqWithMatricesYieldsMatrix()
        {
            var result = "[1,2;3,4] ~= [2,3;4,4]".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 1.0, 1.0 }, { 1.0, 0.0 } }, (Double[,])result);
        }

        [Test]
        public void BinaryGeqWithMatricesYieldsMatrix()
        {
            var result = "[1,2;3,4] >= [2,3;4,4]".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 0.0, 0.0 }, { 0.0, 1.0 } }, (Double[,])result);
        }

        [Test]
        public void BinaryGtWithMatricesYieldsMatrix()
        {
            var result = "[1,2;3,4] > [2,3;4,4]".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 0.0, 0.0 }, { 0.0, 0.0 } }, (Double[,])result);
        }

        [Test]
        public void BinaryLtWithMatricesYieldsMatrix()
        {
            var result = "[1,2;3,4] < [2,3;4,4]".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 1.0, 1.0 }, { 1.0, 0.0 } }, (Double[,])result);
        }

        [Test]
        public void BinaryLtWithDoubleAndMatrixYieldsMatrix()
        {
            var result = "3 < [2,3;4,4]".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 0.0, 0.0 }, { 1.0, 1.0 } }, (Double[,])result);
        }

        [Test]
        public void BinaryGtWithDoubleAndMatrixYieldsMatrix()
        {
            var result = "4 > [2,3;4,4]".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 1.0, 1.0 }, { 0.0, 0.0 } }, (Double[,])result);
        }

        [Test]
        public void BinaryGeqWithDoubleAndMatrixYieldsMatrix()
        {
            var result = "4 >= [2,3;4,4]".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 1.0, 1.0 }, { 1.0, 1.0 } }, (Double[,])result);
        }

        [Test]
        public void BinaryNeqWithMatrixAndDoubleYieldsMatrix()
        {
            var result = "[1,2;3,4] ~= 3".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 1.0, 1.0 }, { 0.0, 1.0 } }, (Double[,])result);
        }

        [Test]
        public void MemberOperatorOnStringShouldYieldNothing()
        {
            var result = "\"hallo\".test".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void MemberOperatorOnNumberShouldYieldNothing()
        {
            var result = "(2.3).test".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void MemberOperatorOnBooleanShouldYieldNothing()
        {
            var result = "(true).test".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void MemberOperatorOnMatrixShouldYieldNothing()
        {
            var result = "[].test".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void MemberOperatorOnNothingShouldYieldNothing()
        {
            var result = "foo.test".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void MemberOperatorOnFunctionShouldYieldNothing()
        {
            var result = "(() => 2 + 3).test".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void MemberOperatorOnDictionaryLegitKeyShouldYieldValue()
        {
            var result = "new { test: 2 + 3 }.test".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void MemberOperatorOnDictionaryInvalidKeyShouldYieldNothing()
        {
            var result = "new { foo: 2 + 3 }.bar".Eval();
            Assert.IsNull(result);
        }

        [Test]
        public void NotWithBooleanIsOpposite()
        {
            var result = "~false".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void NotWithNullIsTrue()
        {
            var result = "~null".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void NotWithDoubleIsFalseIfNotZero()
        {
            var result = "~0.1".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void NotWithDoubleIsTrueIfZero()
        {
            var result = "~0.0".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void NotWithEmptyStringIsTrue()
        {
            var result = "~\"\"".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void NotWithNonEmptyStringIsFalse()
        {
            var result = "~\"Foo\"".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void NotWithFunctionIsFalse()
        {
            var result = "~(() => 5)".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void NotWithMatrixIsLogicMatrix()
        {
            var result = "~[0, 1; 4, 7]".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 1.0, 0.0 }, { 0.0, 0.0 } }, (Double[,])result);
        }

        [Test]
        public void PositiveWithMatrixIsIdentity()
        {
            var result = "+[0, 1; 4, 7]".Eval();
            CollectionAssert.AreEquivalent(new Double[,] { { 0.0, 1.0 }, { 4.0, 7.0 } }, (Double[,])result);
        }

        [Test]
        public void PositiveWithBooleanConvertsToNumber()
        {
            var result = "+true".Eval();
            Assert.AreEqual(1.0, result);
        }

        [Test]
        public void PositiveWithStringConvertsToNumber()
        {
            var result = "+\"123\"".Eval();
            Assert.AreEqual(123.0, result);
        }

        [Test]
        public void PositiveWithStringIsNaNIfNoNumber()
        {
            var result = "+\"foo\"".Eval();
            Assert.AreEqual(Double.NaN, result);
        }

        [Test]
        public void PositiveWithObjectIsNaN()
        {
            var result = "+new {}".Eval();
            Assert.AreEqual(Double.NaN, result);
        }

        [Test]
        public void PositiveWithFunctionIsNaN()
        {
            var result = "+(() => 5)".Eval();
            Assert.AreEqual(Double.NaN, result);
        }

        [Test]
        public void PositiveWithNullIsNaN()
        {
            var result = "+null".Eval();
            Assert.AreEqual(Double.NaN, result);
        }

        [Test]
        public void AbsWithNullIsNaN()
        {
            var result = "abs(null)".Eval();
            Assert.AreEqual(Double.NaN, result);
        }

        [Test]
        public void AbsWithStringTriesToUseNumber()
        {
            var result = "abs(\"-199\")".Eval();
            Assert.AreEqual(199.0, result);
        }

        [Test]
        public void AbsWithInvalidStringIsNaN()
        {
            var result = "abs(\"baz\")".Eval();
            Assert.AreEqual(Double.NaN, result);
        }

        [Test]
        public void AbsWithMatrixReturnsL2Norm()
        {
            var result = "abs([2,2;2,2])".Eval();
            Assert.AreEqual(4.0, result);
        }

        [Test]
        public void AnyWithCompletelyNonZeroMatrixIsTrue()
        {
            var result = "any([2,2;2,2])".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void AnyWithCompletelyZeroMatrixIsFalse()
        {
            var result = "any([0,0,0])".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void AnyWithOneAndZeroMatrixIsTrue()
        {
            var result = "any([0,1;0,0])".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void AllWithCompletelyNonZeroMatrixIsTrue()
        {
            var result = "all([2,2;2,2])".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void AllWithCompletelyZeroMatrixIsFalse()
        {
            var result = "all([0,0,0])".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void AllWithOneAndZeroMatrixIsFalse()
        {
            var result = "all([0,1;0,0])".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void RangeWithValidToAndFromAutoStep()
        {
            var result = "1:3".Eval() as Double[,];
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual(1.0, result[0, 0]);
            Assert.AreEqual(2.0, result[0, 1]);
            Assert.AreEqual(3.0, result[0, 2]);
        }

        [Test]
        public void RangeWithInvalidToAndValidFromAutoStep()
        {
            var result = "1:\"3k\"".Eval() as Double[,];
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void RangeWithValidToAndInvalidFromAutoStep()
        {
            var result = "\"3k\":1".Eval() as Double[,];
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void RangeWithValidToAndFromAndStep()
        {
            var result = "1:2:3".Eval() as Double[,];
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual(1.0, result[0, 0]);
            Assert.AreEqual(3.0, result[0, 1]);
        }

        [Test]
        public void RangeWithValidToAndInvalidFromAndValidStep()
        {
            var result = "\"foo\":2:3".Eval() as Double[,];
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void RangeWithValidToAndFromAndInvalidStep()
        {
            var result = "1:\"foo\":3".Eval() as Double[,];
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void RangeWithInvalidToAndValidFromAndValidStep()
        {
            var result = "1:2:\"foo\"".Eval() as Double[,];
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void PreIncrementWithVariableShouldWork()
        {
            var result = "x = 2; ++x".Eval();
            Assert.AreEqual(3.0, result);
        }

        [Test]
        public void PostIncrementWithVariableShouldWork()
        {
            var result = "x = 2; x++".Eval();
            Assert.AreEqual(2.0, result);
        }

        [Test]
        public void PreDoubleIncrementWithVariableShouldWork()
        {
            var result = "x = 2; ++x; ++x".Eval();
            Assert.AreEqual(4.0, result);
        }

        [Test]
        public void PostDoubleIncrementWithVariableShouldWork()
        {
            var result = "x = 2; x++; x++".Eval();
            Assert.AreEqual(3.0, result);
        }

        [Test]
        public void PreIncrementWithMatrixElementShouldWork()
        {
            var result = "x = [1,2,3]; ++x(0)".Eval();
            Assert.AreEqual(2.0, result);
        }

        [Test]
        public void PostIncrementWithMatrixElementShouldWork()
        {
            var result = "x = [1,2,3]; x(2)++".Eval();
            Assert.AreEqual(3.0, result);
        }

        [Test]
        public void PreDoubleIncrementWithMatrixElementShouldWork()
        {
            var result = "x = [1,5,3]; ++x(1); ++x(1)".Eval();
            Assert.AreEqual(7.0, result);
        }

        [Test]
        public void PostDoubleIncrementWithMatrixElementShouldWork()
        {
            var result = "x = [1,2,3]; x(1)++; x(1)++".Eval();
            Assert.AreEqual(3.0, result);
        }

        [Test]
        public void PreIncrementWithObjectValueShouldWork()
        {
            var result = "x = new { a: \"hi\", b: 7 }; ++x.b".Eval();
            Assert.AreEqual(8.0, result);
        }

        [Test]
        public void PostIncrementWithObjectValueShouldWork()
        {
            var result = "x = new { a: \"hi\", b: 7 }; x.b++".Eval();
            Assert.AreEqual(7.0, result);
        }

        [Test]
        public void PreDoubleIncrementWithObjectValueShouldWork()
        {
            var result = "x = new { a: \"hi\", b: 7 }; ++x.b; ++x.b".Eval();
            Assert.AreEqual(9.0, result);
        }

        [Test]
        public void PostDoubleIncrementWithObjectValueShouldWork()
        {
            var result = "x = new { a: \"hi\", b: 7 }; x.b++; x.b++".Eval();
            Assert.AreEqual(8.0, result);
        }

        [Test]
        public void PreDecrementWithVariableShouldWork()
        {
            var result = "x = 2; --x".Eval();
            Assert.AreEqual(1.0, result);
        }

        [Test]
        public void PostDecrementWithVariableShouldWork()
        {
            var result = "x = 2; x--".Eval();
            Assert.AreEqual(2.0, result);
        }

        [Test]
        public void PreDoubleDecrementWithVariableShouldWork()
        {
            var result = "x = 2; --x; --x".Eval();
            Assert.AreEqual(0.0, result);
        }

        [Test]
        public void PostDoubleDecrementWithVariableShouldWork()
        {
            var result = "x = 2; x--; x--".Eval();
            Assert.AreEqual(1.0, result);
        }

        [Test]
        public void PreDecrementWithMatrixElementShouldWork()
        {
            var result = "x = [1,2,3]; --x(0)".Eval();
            Assert.AreEqual(0.0, result);
        }

        [Test]
        public void PostDecrementWithMatrixElementShouldWork()
        {
            var result = "x = [1,2,3]; x(2)--".Eval();
            Assert.AreEqual(3.0, result);
        }

        [Test]
        public void PreDoubleDecrementWithMatrixElementShouldWork()
        {
            var result = "x = [1,5,3]; --x(1); --x(1)".Eval();
            Assert.AreEqual(3.0, result);
        }

        [Test]
        public void PostDoubleDecrementWithMatrixElementShouldWork()
        {
            var result = "x = [1,2,3]; x(1)--; x(1)--".Eval();
            Assert.AreEqual(1.0, result);
        }

        [Test]
        public void PreDecrementWithObjectValueShouldWork()
        {
            var result = "x = new { a: \"hi\", b: 7 }; --x.b".Eval();
            Assert.AreEqual(6.0, result);
        }

        [Test]
        public void PostDecrementWithObjectValueShouldWork()
        {
            var result = "x = new { a: \"hi\", b: 7 }; x.b--".Eval();
            Assert.AreEqual(7.0, result);
        }

        [Test]
        public void PreDoubleDecrementWithObjectValueShouldWork()
        {
            var result = "x = new { a: \"hi\", b: 7 }; --x.b; --x.b".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void PostDoubleDecrementWithObjectValueShouldWork()
        {
            var result = "x = new { a: \"hi\", b: 7 }; x.b--; x.b--".Eval();
            Assert.AreEqual(6.0, result);
        }

        [Test]
        public void InitMatrixWithNonExistingValuesYieldsNaN()
        {
            var result = "[a, b, c]".Eval() as Double[,];
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Length);
            Assert.IsNaN(result[0, 0]);
            Assert.IsNaN(result[0, 1]);
            Assert.IsNaN(result[0, 2]);
        }

        [Test]
        public void ReducerStandardFunctionsAreCurried()
        {
            var result = "sum() == sum".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void ArrayStandardFunctionsAreCurried()
        {
            var result = "sort() == sort".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void LogicalStandardFunctionsAreCurried()
        {
            var result = "isprime() == isprime".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void TrigonometricStandardFunctionsAreCurried()
        {
            var result = "sin() == sin".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void ArithmeticStandardFunctionsAreCurried()
        {
            var result = "sqrt() == sqrt".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void ComparisonStandardFunctionsAreCurried()
        {
            var result = "eq() == eq".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void OperatorStandardFunctionsAreCurried()
        {
            var result = "add() == add".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void UseCurriedAddOperatorFunction()
        {
            var result = "add(2)(3)".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void NullIsNothingIsTrue()
        {
            var result = "abc(foo) == null".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void SomethingAintNothingIsFalse()
        {
            var result = "3 == null".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void TypeOperatorOnNullYieldsUndefined()
        {
            var result = "&null".Eval();
            Assert.AreEqual("Undefined", result);
        }

        [Test]
        public void TypeOperatorOnResultOfTypeOperatorYieldsString()
        {
            var result = "& &null".Eval();
            Assert.AreEqual("String", result);
        }

        [Test]
        public void TypeOperatorOnMatrixYieldsMatrix()
        {
            var result = "&[1, 2, 3]".Eval();
            Assert.AreEqual("Matrix", result);
        }

        [Test]
        public void PipeOperatorOnAddYieldsFunction()
        {
            var result = "2 | add".Eval();
            Assert.IsInstanceOf<Function>(result);
        }

        [Test]
        public void PipeOperatorOnCurriedAddYieldsResult()
        {
            var result = "2 | add(3)".Eval();
            Assert.IsInstanceOf<Double>(result);
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void PipeOperatorAfterMinusOnCurriedMultiplyYieldsResult()
        {
            var result = "2 - 4 | mul(3)".Eval();
            Assert.IsInstanceOf<Double>(result);
            Assert.AreEqual(-6.0, result);
        }

        [Test]
        public void PipeOperatorIsLowerPrecendenceThanEquals()
        {
            var result = "3 == 4 | type".Eval();
            Assert.IsInstanceOf<String>(result);
            Assert.AreEqual("Boolean", result);
        }

        [Test]
        public void PipeOperatorIsLowerPrecendenceThanOr()
        {
            var result = "1 || 0 | type".Eval();
            Assert.IsInstanceOf<String>(result);
            Assert.AreEqual("Boolean", result);
        }

        [Test]
        public void PipeOperatorOnTypeYieldsResult()
        {
            var result = "2 | type".Eval();
            Assert.IsInstanceOf<String>(result);
            Assert.AreEqual("Number", result);
        }

        [Test]
        public void PipeOperatorOnTypeOfTypeYieldsString()
        {
            var result = "2 | type | type".Eval();
            Assert.IsInstanceOf<String>(result);
            Assert.AreEqual("String", result);
        }

        [Test]
        public void InvalidAssignmentAddWithNumberYieldsError()
        {
            Assert.Catch<ParseException>(() => "x = 0; x + = 2; x".Eval());
        }

        [Test]
        public void AssignmentAddWithNumberYieldsRightResult()
        {
            var result = "x = 0; x += 2; x".Eval();
            Assert.AreEqual(2.0, result);
        }

        [Test]
        public void AssignmentSubtractWithNumberYieldsRightResult()
        {
            var result = "x = 7; x -= 2; x".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void AssignmentMultiplyWithNumberYieldsRightResult()
        {
            var result = "x = 3; x *= 2; x".Eval();
            Assert.AreEqual(6.0, result);
        }

        [Test]
        public void InvalidAssignmentMultiplyWithNumberYieldsError()
        {
            Assert.Catch<ParseException>(() => "x = 0; x * = 2; x".Eval());
        }

        [Test]
        public void AssignmentPowerWithNumberYieldsRightResult()
        {
            var result = "x = 3; x ^= 2; x".Eval();
            Assert.AreEqual(9.0, result);
        }

        [Test]
        public void InvalidAssignmentPowerWithNumberYieldsError()
        {
            Assert.Catch<ParseException>(() => "x = 0; x ^ = 2; x".Eval());
        }

        [Test]
        public void AssignmentDivideWithNumberYieldsRightResult()
        {
            var result = "x = 10; x /= 2; x".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void AssignmentPipeWithNumberYieldsRightResult()
        {
            var result = "x = 4; x |= factorial; x".Eval();
            Assert.AreEqual(24.0, result);
        }

        [Test]
        public void InvalidAssignmentPipeWithNumberYieldsError()
        {
            Assert.Catch<ParseException>(() => "x = 0; x | = factorial; x".Eval());
        }

        [Test]
        public void AssignmentMultiplyTreatsRightSideAsOneExpression()
        {
            var result = "x = 12; x *= 1 + 2; x".Eval();
            Assert.AreEqual(36.0, result);
        }

        [Test]
        public void AssignmentAddAndAssignmentSubtractWithNumberYieldsRightResult()
        {
            var result = "x = 4; y = 3; x += y -= 5; x".Eval();
            Assert.AreEqual(2.0, result);
        }

        [Test]
        public void AssignmentMultiplyAndAssignmentPowerWithNumberYieldsRightResult()
        {
            var result = "x = 4; y = 3; x *= y ^= 2; x + y".Eval();
            Assert.AreEqual(45.0, result);
        }

        [Test]
        public void CallingAMemberFunctionCanAccessLocalScopedThis()
        {
            var result = @"var foo = new {
              a: 5,
              bar: () => this.a
            };
            foo.bar()".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void CallingAMemberFunctionUsesReferencedScopedThis()
        {
            var result = @"var foo = new {
              a: 5,
              bar: () => this.a
            };
            foo.a = false;
            foo.bar()".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void CallingAnAliasedMemberFunctionDoesNotLooseCapture()
        {
            var result = @"var foo = new {
              a: 5,
              bar: () => this.a
            };
            var f = foo.bar;
            foo.a = ""hallo"";
            f()".Eval();
            Assert.AreEqual("hallo", result);
        }

        [Test]
        public void CallingNestedMemberFunctionWorksOnNestedThis()
        {
            var result = @"var foo = new {
              a: 5,
              b: new { a: 4, bar: () => this.a }
            };
            foo.b.bar()".Eval();
            Assert.AreEqual(4.0, result);
        }

        [Test]
        public void PatternMatchingWithReturnYieldsRightResult()
        {
            var result = @"x = 9;
a = 0;
match(x) {
eq(8) {
 a+=1;
}
eq(9) {
 a+=2;
}
gt(8) {
 a+=3;
}
lt(10) {
 a+=4;
}
any {
 return a;
}
}; return 0".Eval();
            Assert.AreEqual(9.0, result);
        }

        [Test]
        public void PatternMatchingWithBreakYieldsRightResult()
        {
            var result = @"x = 9;
a = 0;
match(x) {
eq(8) {
 a+=1;
}
eq(9) {
 a+=2;
}
gt(8) {
 a+=3;
}
lt(10) {
 break;
}
any {
 a+=5;
}
}; return a".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void PatternMatchingWithContinueYieldsRightResult()
        {
            var result = @"x = 9;
a = 0;
match(x) {
eq(8) {
 a+=1;
}
eq(9) {
 a+=2;
}
gt(8) {
 a+=3;
}
lt(10) {
 continue;
 a+=4;
}
any {
 a+=5;
}
}; return a".Eval();
            Assert.AreEqual(10.0, result);
        }
    }
}
