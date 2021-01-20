namespace Mages.Core.Tests
{
    using Mages.Core.Ast;
    using Mages.Core.Ast.Expressions;
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class ImplicitMultiplicationTests
    {
        [Test]
        public void ExplicitMultiplicationInvolvingNumberAndIdentifier()
        {
            var expr = "2 * x".ToExpression();

            AssertMultiplication(expr, 2.0, "x");
        }

        [Test]
        public void ExplicitMultiplicationInvolvingTwoIdentifiers()
        {
            var expr = "x * y".ToExpression();

            AssertMultiplication(expr, "x", "y");
        }

        [Test]
        public void ImplicitMultiplicationInvolvingNumberAndIdentifierSeparatedBySpace()
        {
            var expr = "2 x".ToExpression();

            AssertMultiplication(expr, 2.0, "x");
        }

        [Test]
        public void ImplicitMultiplicationInvolvingTwoNumbersSeparatedBySpace()
        {
            var expr = "2 3".ToExpression();

            AssertMultiplication(expr, 2.0, 3.0);
        }

        [Test]
        public void ImplicitMultiplicationInvolvingTwoIdentifiersSeparatedBySpace()
        {
            var expr = "x y".ToExpression();

            AssertMultiplication(expr, "x", "y");
        }

        [Test]
        public void ImplicitMultiplicationInvolvingNumberAndMatrixSeparatedBySpace()
        {
            var expr = "2 [1,2,3]".ToExpression();

            AssertMultiplication<ConstantExpression, MatrixExpression>(expr,
                constant => (Double)constant.Value == 2.0,
                matrix => matrix.Values.Length == 1 && matrix.Values[0].Length == 3);
        }

        [Test]
        public void ImplicitMultiplicationInvolvingIdentifierAndMatrixSeparatedBySpace()
        {
            var expr = "x [1,2,3]".ToExpression();

            AssertMultiplication<VariableExpression, MatrixExpression>(expr,
                variable => variable.Name == "x",
                matrix => matrix.Values.Length == 1 && matrix.Values[0].Length == 3);
        }

        [Test]
        public void ImplicitMultiplicationInvolvingIdentifiersAndKeywordConstantSeparatedBySpace()
        {
            var expr = "n pi".ToExpression();

            AssertMultiplication<VariableExpression, ConstantExpression>(expr,
                variable => variable.Name == "n",
                variable => (Double)variable.Value == Math.PI);
        }

        [Test]
        public void ImplicitMultiplicationInvolvingNumberAndIdentifierNotSeparatedBySpace()
        {
            var expr = "2x".ToExpression();

            AssertMultiplication(expr, 2.0, "x");
        }

        [Test]
        public void ImplicitMultiplicationInvolvingIntegerAndFunctionCallSeparatedBySpace()
        {
            var expr = "2 exp(1)".ToExpression();

            AssertMultiplication<ConstantExpression, CallExpression>(expr,
                constant => (Double)constant.Value == 2.0,
                call => ((VariableExpression)call.Function).Name == "exp" && (Double)((ConstantExpression)call.Arguments.Arguments[0]).Value == 1.0);
        }

        [Test]
        public void ImplicitMultiplicationInvolvingNumberAndFunctionCallNotSeparatedBySpace()
        {
            var expr = "6.28sin(x)".ToExpression();

            AssertMultiplication(expr, 6.28, "sin", "x");
        }

        private static void AssertMultiplication(IExpression expr, String leftName, String rightName)
        {
            AssertMultiplication<VariableExpression, VariableExpression>(expr,
                variable => variable.Name == leftName,
                variable => variable.Name == rightName);
        }
        
        private static void AssertMultiplication(IExpression expr, Double value, String name)
        {
            AssertMultiplication<ConstantExpression, VariableExpression>(expr,
                constant => (Double)constant.Value == value,
                variable => variable.Name == name);
        }

        private static void AssertMultiplication(IExpression expr, Double leftValue, Double rightValue)
        {
            AssertMultiplication<ConstantExpression, ConstantExpression>(expr,
                constant => (Double)constant.Value == leftValue,
                constant => (Double)constant.Value == rightValue);
        }

        private static void AssertMultiplication(IExpression expr, Double value, String functionName, String functionArgument)
        {
            AssertMultiplication<ConstantExpression, CallExpression>(expr,
                constant => (Double)constant.Value == value,
                call => ((VariableExpression)call.Function).Name == functionName && ((VariableExpression)call.Arguments.Arguments[0]).Name == functionArgument);
        }

        private static void AssertMultiplication<TLeft, TRight>(IExpression expr, Predicate<TLeft> leftChecker, Predicate<TRight> rightChecker)
        {
            Assert.IsInstanceOf<BinaryExpression.Multiply>(expr);

            var binary = (BinaryExpression)expr;

            Assert.IsInstanceOf<TLeft>(binary.LValue);
            Assert.IsInstanceOf<TRight>(binary.RValue);

            Assert.IsTrue(leftChecker.Invoke((TLeft)binary.LValue));
            Assert.IsTrue(rightChecker.Invoke((TRight)binary.RValue));
        }
    }
}
