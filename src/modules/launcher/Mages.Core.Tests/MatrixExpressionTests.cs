namespace Mages.Core.Tests
{
    using Mages.Core.Ast.Expressions;
    using NUnit.Framework;

    [TestFixture]
    public class MatrixExpressionTests
    {
        [Test]
        public void EmptyMatrix()
        {
            var result = @"[]".ToExpression();

            Assert.IsInstanceOf<MatrixExpression>(result);

            var matrix = (MatrixExpression)result;

            Assert.AreEqual(0, matrix.Values.Length);
        }

        [Test]
        public void SingleElementMatrix()
        {
            var result = @"[2]".ToExpression();

            Assert.IsInstanceOf<MatrixExpression>(result);

            var matrix = (MatrixExpression)result;

            Assert.AreEqual(1, matrix.Values.Length);
            Assert.AreEqual(1, matrix.Values[0].Length);
            Assert.IsInstanceOf<ConstantExpression>(matrix.Values[0][0]);
        }

        [Test]
        public void SingleColumnVectorMatrix()
        {
            var result = @"[1,2,3]".ToExpression();

            Assert.IsInstanceOf<MatrixExpression>(result);

            var matrix = (MatrixExpression)result;

            Assert.AreEqual(1, matrix.Values.Length);
            Assert.AreEqual(3, matrix.Values[0].Length);
            Assert.IsInstanceOf<ConstantExpression>(matrix.Values[0][0]);
            Assert.IsInstanceOf<ConstantExpression>(matrix.Values[0][1]);
            Assert.IsInstanceOf<ConstantExpression>(matrix.Values[0][2]);
        }

        [Test]
        public void SingleRowVectorMatrix()
        {
            var result = @"[1;2;3]".ToExpression();

            Assert.IsInstanceOf<MatrixExpression>(result);

            var matrix = (MatrixExpression)result;

            Assert.AreEqual(3, matrix.Values.Length);
            Assert.AreEqual(1, matrix.Values[0].Length);
            Assert.AreEqual(1, matrix.Values[1].Length);
            Assert.AreEqual(1, matrix.Values[2].Length);
            Assert.IsInstanceOf<ConstantExpression>(matrix.Values[0][0]);
            Assert.IsInstanceOf<ConstantExpression>(matrix.Values[1][0]);
            Assert.IsInstanceOf<ConstantExpression>(matrix.Values[2][0]);
        }

        [Test]
        public void VectorWithSpacesMatrix()
        {
            var result = @"[1,2  ,   3,4]".ToExpression();

            Assert.IsInstanceOf<MatrixExpression>(result);

            var matrix = (MatrixExpression)result;

            Assert.AreEqual(1, matrix.Values.Length);
            Assert.AreEqual(4, matrix.Values[0].Length);
            Assert.IsInstanceOf<ConstantExpression>(matrix.Values[0][0]);
            Assert.IsInstanceOf<ConstantExpression>(matrix.Values[0][1]);
            Assert.IsInstanceOf<ConstantExpression>(matrix.Values[0][2]);
            Assert.IsInstanceOf<ConstantExpression>(matrix.Values[0][3]);
        }

        [Test]
        public void SquareMatrixOfConstants()
        {
            var result = @"[1,2  ;   3,4]".ToExpression();

            Assert.IsInstanceOf<MatrixExpression>(result);

            var matrix = (MatrixExpression)result;

            Assert.AreEqual(2, matrix.Values.Length);
            Assert.AreEqual(2, matrix.Values[0].Length);
            Assert.AreEqual(2, matrix.Values[1].Length);
            Assert.IsInstanceOf<ConstantExpression>(matrix.Values[0][0]);
            Assert.IsInstanceOf<ConstantExpression>(matrix.Values[0][1]);
            Assert.IsInstanceOf<ConstantExpression>(matrix.Values[1][0]);
            Assert.IsInstanceOf<ConstantExpression>(matrix.Values[1][1]);
        }

        [Test]
        public void DifferentExpressionsInRowVectorMatrix()
        {
            var result = @"[1+3;x;f(3);7*3]".ToExpression();

            Assert.IsInstanceOf<MatrixExpression>(result);

            var matrix = (MatrixExpression)result;

            Assert.AreEqual(4, matrix.Values.Length);
            Assert.AreEqual(1, matrix.Values[0].Length);
            Assert.IsInstanceOf<BinaryExpression.Add>(matrix.Values[0][0]);
            Assert.IsInstanceOf<VariableExpression>(matrix.Values[1][0]);
            Assert.IsInstanceOf<CallExpression>(matrix.Values[2][0]);
            Assert.IsInstanceOf<BinaryExpression.Multiply>(matrix.Values[3][0]);
        }

        [Test]
        public void DifferentExpressionsInColumnVectorMatrix()
        {
            var result = @"[1+3,x,f(3),7*3]".ToExpression();

            Assert.IsInstanceOf<MatrixExpression>(result);

            var matrix = (MatrixExpression)result;

            Assert.AreEqual(1, matrix.Values.Length);
            Assert.AreEqual(4, matrix.Values[0].Length);
            Assert.IsInstanceOf<BinaryExpression.Add>(matrix.Values[0][0]);
            Assert.IsInstanceOf<VariableExpression>(matrix.Values[0][1]);
            Assert.IsInstanceOf<CallExpression>(matrix.Values[0][2]);
            Assert.IsInstanceOf<BinaryExpression.Multiply>(matrix.Values[0][3]);
        }

        [Test]
        public void FunctionVectorAndArithmeticInVectorMatrix()
        {
            var result = @"[()=>3,[1,2,3,4],2+3,(1-2)*3]".ToExpression();

            Assert.IsInstanceOf<MatrixExpression>(result);

            var matrix = (MatrixExpression)result;

            Assert.AreEqual(1, matrix.Values.Length);
            Assert.AreEqual(4, matrix.Values[0].Length);
            Assert.IsInstanceOf<FunctionExpression>(matrix.Values[0][0]);
            Assert.IsInstanceOf<MatrixExpression>(matrix.Values[0][1]);
            Assert.IsInstanceOf<BinaryExpression.Add>(matrix.Values[0][2]);
            Assert.IsInstanceOf<BinaryExpression.Multiply>(matrix.Values[0][3]);
        }
    }
}
