namespace Mages.Core.Tests
{
    using Mages.Core.Ast.Walkers;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;

    [TestFixture]
    public class YampTests
    {
        [Test]
        public void LineComment()
        {
            Test("2+3//This is a line-comment!\n-4", 1.0);
        }

        [Test]
        public void BlockComment()
        {
            Test("1-8* /* this is another comment */ 0.25", -1.0);
        }

        [Test]
        public void BlockCommentWithNewLines()
        {
            Test("1-8* /* this is \nanother comment\nwith new lines */ 0.5+4", 1.0);
        }

        [Test]
        public void LambdaExpressionWithInteger()
        {
            Test("(x => x^2)(2)", 4.0);
        }

        [Test]
        public void LambdaExpressionWithMatrices()
        {
            Test("((x, y) => x*y')([1,2,3],[1,2,3])", new Double[,] { { 14.0 } });
        }

        [Test]
        public void Subtraction()
        {
            Test("2-3-4", -5.0);
        }

        [Test]
        public void PowerAdditionAndDivides()
        {
            Test("(10^6*27)/8/1024", (27000000.0 / 8.0) / 1024.0);
        }

        [Test]
        public void NegateAndAdd()
        {
            Test("-2+3", 1.0);
        }

        [Test]
        public void FloatingPoint()
        {
            Test("2.2", 2.2);
        }

        [Test]
        public void AbbreviatedFloatingPointAddAndSubtract()
        {
            Test(".2+4-4+.2", 0.2 + 4 - 4 + 0.2);
        }

        [Test]
        public void DivideAndAddAndMultiply()
        {
            Test("3/3+1*4-5", 0.0);
        }

        [Test]
        public void IntegerArithmetic()
        {
            Test("7*3+5-13+2", 15.0);
        }

        [Test]
        public void BracketsAndDivide()
        {
            Test("(7*3+5-13+2)/3", 5.0);
        }

        [Test]
        public void ExactDivideByPrimeThree()
        {
            Test("15/3-5", 0.0);
        }

        [Test]
        public void BracketAndSubtractFollowRules()
        {
            Test("(7*3+5-13+2)/3-5", 0.0);
        }

        [Test]
        public void PowerOperationAndAdd()
        {
            Test("2^3+3", 11.0);
        }

        [Test]
        public void AddAndPowerOperation()
        {
            Test("3+2^3", 11.0);
        }

        [Test]
        public void MultiplyAndPowerOperation()
        {
            Test("2*3^3+7/2+1-4/2", 56.5);
        }

        [Test]
        public void PowerOperationWithBracket()
        {
            Test("2*3^(3-1)+7/2+1-4/2", 20.5);
        }

        [Test]
        public void BracketAndPowerOperation()
        {
            Test("(2-1)*3^(3-1)+7/2+1-4/2", 11.5);
        }

        [Test]
        public void MultiplyWithAbsoluteValue()
        {
            Test("3*abs(-2)*5", 30.0);
        }

        [Test]
        public void FactorialAndSubtract()
        {
            Test("5!-1000/5+4*20", 0.0);
        }

        [Test]
        public void ChainedPower()
        {
            Test("2^2^2^2", 65536.0);
        }

        [Test]
        public void DivideByBracket()
        {
            Test("2-(3*5)^2+7/(2-8)*2", -225.0 - 1.0 / 3.0);
        }

        [Test]
        public void NegativePowerWithoutBracket()
        {
            Test("-2^2", 4.0);
        }

        [Test]
        public void NegativePowerWithBracket()
        {
            Test("-(2^2)", -4.0);
        }

        [Test]
        public void NegateAndSubtract()
        {
            Test("-2-2", -4.0);
        }

        [Test]
        public void NegativeMultiplyWithoutBracket()
        {
            Test("-2*2", -4.0);
        }

        [Test]
        public void NegatePowerAndAdd()
        {
            Test("-2^2+4", 8.0);
        }

        [Test]
        public void UnitMultiplication()
        {
            Test("3-4*1-1", -2.0);
        }

        [Test]
        public void UnitPower()
        {
            Test("3-4^1-1", -2.0);
        }

        [Test]
        public void DivideChained()
        {
            Test("2/2/2", 0.5);
        }

        [Test]
        public void PowerAndPower()
        {
            Test("2^2^2", 16.0);
        }

        [Test]
        public void StrangeNumberCombination()
        {
            Test("0.212410080106903 * 500-0.00654415361812242 * 500-0.0337905933677912 * 500-0.182007882231707 * 500+131.208072980527", 126.24179842516818);
        }

        [Test]
        public void NegativeSquaredInBracket()
        {
            Test("(1 - (-1)^2)^0.5", 0.0);
        }

        [Test]
        public void NegativeSquaredAlright()
        {
            Test("(-25)^2", 625.0);
        }

        [Test]
        public void RaisedToNegativePower()
        {
            Test("2^-2", 0.25);
        }

        [Test]
        public void RaisedInverseChained()
        {
            Test("2^-1^-1", 0.5);
        }

        [Test]
        public void RaisedInverseWithBrackets()
        {
            Test("(2^-1)^-1", 2.0);
        }

        [Test]
        public void NegativeSquaredWithBracket()
        {
            Test("(-5)^2", 25.0);
        }

        [Test]
        public void PositiveSquaredWithBracket()
        {
            Test("(5)^2", 25.0);
        }

        [Test]
        public void NegativeSquaredBracketInteger()
        {
            Test("(-75)^2", Math.Pow(75.0, 2.0));
        }

        [Test]
        public void AbsoluteValueOfSubtractedMatrices()
        {
            Test("abs([2,3,1]-[1,3,1])", 1.0);
        }

        [Test]
        public void ExponentialAndPiCube()
        {
            Test("exp(1)^3-pi^3", Math.Pow(Math.E, 3.0) - Math.Pow(Math.PI, 3.0));
        }

        [Test]
        public void NegatePi()
        {
            Test("-pi", -Math.PI);
        }

        [Test]
        public void ScalarArithmeticsInMatrixLiteral()
        {
            Test("abs([2^2,2+3,-2,-2])", 7.0);
        }

        [Test]
        public void MatrixMultiplication()
        {
            Test("abs([3,2,1]*[1;2;3])", 10.0);
        }

        [Test]
        public void DifferentDimensionSameValue()
        {
            Test("abs([1;2;3])-abs([1,2,3])", 0.0);
        }

        [Test]
        public void ScalarTimesMatrix()
        {
            Test("abs(2*[1,2;1,2])", Math.Sqrt(40.0));
        }

        [Test]
        public void SubtractMatrices()
        {
            Test("abs([2,1;3,5]-[2,1;3,5]')", Math.Sqrt(8.0));
        }

        [Test]
        public void MultiplyVectors()
        {
            Test("[2,1,0]*[5;2;1]", new Double[,] { { 12.0 } });
        }

        [Test]
        public void RangeWithExplicitDelta()
        {
            Test("abs(1:1:3)", Math.Sqrt(1 + 4 + 9));
        }

        [Test]
        public void CommaIsNewColumn()
        {
            Test("abs([1,2,3])", Math.Sqrt(1 + 4 + 9));
        }

        [Test]
        public void SemicolonIsNewRow()
        {
            Test("abs([1;2;3])", Math.Sqrt(1 + 4 + 9));
        }

        [Test]
        public void SubtractionOfVectors()
        {
            Test("([3, 2, 1] - [1, 2, 3])(2)", -2.0);
        }

        [Test]
        public void AdditionOfVectors()
        {
            Test("[3, 2, 1] + [1, 2, 3]", new Double[,] { { 4.0, 4.0, 4.0 } });
        }

        [Test]
        public void TwoDimensionalIndex()
        {
            Test("[1,2,3;4,5,6;7,8,9](1,2)", 6.0);
        }

        [Test]
        public void SineOfMatrix()
        {
            Test("-sin([1,2,3])(1)", -Math.Sin(2));
        }

        [Test]
        public void LengthOfRange()
        {
            Test("length(-pi/4:0.1:pi/4)", 16.0);
        }

        [Test]
        public void TransposeMatrix()
        {
            Test("([1, 2; 3, 4]')(1,0)", 2.0);
        }

        [Test]
        public void ParseMandelbrotFunctionCallMissingArgumentShouldFail()
        {
            IsInvalid("mandelbrot(-2,,-2,2,100,100)");
        }

        [Test]
        public void ParseMatrixLiteralAndIndex()
        {
            IsValid("[1,2,3](2)");
        }

        [Test]
        public void ParseNegativePower()
        {
            IsValid("2^-2");
        }

        [Test]
        public void ParsePowerPowerShouldFail()
        {
            IsInvalid("2^^2");
        }

        [Test]
        public void ParseStandaloneMemberOperatorShouldFail()
        {
            IsInvalid(".");
        }

        [Test]
        public void ParseStandalonePlusOperatorShouldFail()
        {
            IsInvalid("+");
        }

        [Test]
        public void ParseStandaloneNegateOperatorShouldFail()
        {
            IsInvalid("~");
        }

        [Test]
        public void ParseFunctionCallWithMissingArgumentsShouldFail()
        {
            IsInvalid("newton(");
        }

        [Test]
        public void ParseIndexOperatorMissingShouldFail()
        {
            IsInvalid("a([4)");
        }

        [Test]
        public void ParseCallOperatorMissingShouldFail()
        {
            IsInvalid("f(");
        }

        [Test]
        public void EqualsFits()
        {
            Test("1 == 1", true);
        }

        [Test]
        public void EqualsFail()
        {
            Test("1 == 0", false);
        }

        [Test]
        public void NotEqualsFits()
        {
            Test("1 ~= 0", true);
        }

        [Test]
        public void NotEqualsFail()
        {
            Test("1 ~= 1", false);
        }

        [Test]
        public void AndFitsOne()
        {
            Test("1 && 1", true);
        }

        [Test]
        public void AndFailLeft()
        {
            Test("1 && 0", false);
        }

        [Test]
        public void AndFailRight()
        {
            Test("0 && 1", false);
        }

        [Test]
        public void AndFailsZero()
        {
            Test("0 && 0", false);
        }

        [Test]
        public void OrFitsOne()
        {
            Test("1 || 1", true);
        }

        [Test]
        public void OrFitsRight()
        {
            Test("0 || 1", true);
        }

        [Test]
        public void OrFailsZero()
        {
            Test("0 || 0", false);
        }

        [Test]
        public void OrFitsLeft()
        {
            Test("1 || 0", true);
        }

        [Test]
        public void AbsoluteOfMatrixSmaller()
        {
            Test("abs([1,2,3,4,5,6,7] < 5)", 2.0);
        }

        [Test]
        public void Greater()
        {
            Test("17>12", true);
        }

        [Test]
        public void Smaller()
        {
            Test("7<-1.5", false);
        }

        [Test]
        public void NegativeNumberEqualsNot()
        {
            Test("3-1~=4", true);
        }

        [Test]
        public void Ceiling()
        {
            Test("ceil(2.5)", 3.0);
        }

        [Test]
        public void Floor()
        {
            Test("floor(2.5)", 2.0);
        }

        [Test]
        public void ExponentialZero()
        {
            Test("exp(0) 10-5", 5.0);
        }

        [Test]
        public void ExponentialThree()
        {
            Test("exp(3) * 10", 10.0 * Math.Exp(3));
        }

        [Test]
        public void Cosine()
        {
            Test("cos(pi)", -1.0);
        }

        [Test]
        public void Sine()
        {
            Test("sin(pi/2)", 1.0);
        }

        [Test]
        public void Trigonometric()
        {
            Test("cos(0)*exp(0)*sin(3*pi/2)", -1.0);
        }

        [Test]
        public void Maximum()
        {
            Test("max([1,5,7,9,8])", 9.0);
        }

        private void Test(String sourceCode, Double[,] expected)
        {
            var result = (Double[,])sourceCode.Eval();
            CollectionAssert.AreEquivalent(expected, result);
        }

        private void Test(String sourceCode, Boolean expected)
        {
            var result = (Boolean)sourceCode.Eval();
            Assert.AreEqual(expected, result);
        }

        private void Test(String sourceCode, Double expected, Double tolerance = 0.0)
        {
            var result = (Double)sourceCode.Eval();
            Assert.AreEqual(expected, result, tolerance);
        }

        private void IsInvalid(String sourceCode)
        {
            var hasError = Validate(sourceCode);
            Assert.IsTrue(hasError);
        }

        private void IsValid(String sourceCode)
        {
            var hasError = Validate(sourceCode);
            Assert.IsFalse(hasError);
        }

        private Boolean Validate(String sourceCode)
        {
            var errors = new List<ParseError>();
            var walker = new ValidationTreeWalker(errors);
            var statement = sourceCode.ToStatement();
            statement.Accept(walker);
            return errors.Count > 0;
        }
    }
}
