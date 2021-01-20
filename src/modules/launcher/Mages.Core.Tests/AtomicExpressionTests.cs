namespace Mages.Core.Tests
{
    using Mages.Core.Ast.Expressions;
    using NUnit.Framework;

    [TestFixture]
    public class AtomicExpressionTests
    {
        [Test]
        public void UnknownCharacterIsInvalidExpression()
        {
            var expr = "$".ToExpression();
            Assert.IsInstanceOf<InvalidExpression>(expr);
        }

        [Test]
        public void EmptySourceIsEmptyExpression()
        {
            var expr = "".ToExpression();
            Assert.IsInstanceOf<EmptyExpression>(expr);
        }

        [Test]
        public void SpaceIsEmptyExpression()
        {
            var expr = " ".ToExpression();
            Assert.IsInstanceOf<EmptyExpression>(expr);
        }

        [Test]
        public void SpacesSourceIsEmptyExpression()
        {
            var expr = "\t \n   ".ToExpression();
            Assert.IsInstanceOf<EmptyExpression>(expr);
        }

        [Test]
        public void UnknownCharacterIsInvalidExpressionContainedInBinaryExpression()
        {
            var expr = "$+".ToExpression();
            Assert.IsInstanceOf<BinaryExpression>(expr);
            var left = ((BinaryExpression)expr).LValue;
            var right = ((BinaryExpression)expr).RValue;
            Assert.IsInstanceOf<InvalidExpression>(left);
            Assert.IsInstanceOf<EmptyExpression>(right);
        }

        [Test]
        public void TrueIsConstantExpression()
        {
            var expr = "true".ToExpression();
            Assert.IsInstanceOf<ConstantExpression>(expr);
        }

        [Test]
        public void FalseIsConstantExpression()
        {
            var expr = "false".ToExpression();
            Assert.IsInstanceOf<ConstantExpression>(expr);
        }

        [Test]
        public void ArbitraryIdentifierIsVariableExpression()
        {
            var expr = "a".ToExpression();
            Assert.IsInstanceOf<VariableExpression>(expr);
        }

        [Test]
        public void NumberIsConstantExpression()
        {
            var expr = "2.3".ToExpression();
            Assert.IsInstanceOf<ConstantExpression>(expr);
        }

        [Test]
        public void StringIsConstantExpression()
        {
            var expr = "\"hi there\"".ToExpression();
            Assert.IsInstanceOf<ConstantExpression>(expr);
        }
    }
}
