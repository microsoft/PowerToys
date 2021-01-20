namespace Mages.Core.Tests
{
    using Mages.Core.Ast.Expressions;
    using Mages.Core.Ast.Statements;
    using NUnit.Framework;

    [TestFixture]
    public class FunctionExpressionTests
    {
        [Test]
        public void ParseSimpleFunction()
        {
            var result = "()=>new{}".ToExpression();
            
            Assert.IsInstanceOf<FunctionExpression>(result);

            var fx = (FunctionExpression)result;
            Assert.AreEqual(0, fx.Parameters.Parameters.Length);

            Assert.IsInstanceOf<SimpleStatement>(fx.Body);

            var body = (SimpleStatement)fx.Body;

            Assert.IsInstanceOf<ObjectExpression>(body.Expression);
        }

        [Test]
        public void ParseSimpleFunctionWithImplicitReturn()
        {
            var result = "()=>2*3".ToExpression();

            Assert.IsInstanceOf<FunctionExpression>(result);

            var fx = (FunctionExpression)result;

            Assert.AreEqual(0, fx.Parameters.Parameters.Length);

            Assert.IsInstanceOf<SimpleStatement>(fx.Body);

            var body = (SimpleStatement)fx.Body;
            Assert.IsInstanceOf<BinaryExpression.Multiply>(body.Expression);

            var multiply = (BinaryExpression)body.Expression;

            Assert.IsInstanceOf<ConstantExpression>(multiply.LValue);
            Assert.IsInstanceOf<ConstantExpression>(multiply.RValue);
        }

        [Test]
        public void ParseSimpleFunctionWithOneArgument()
        {
            var result = "(x) => new{}".ToExpression();

            Assert.IsInstanceOf<FunctionExpression>(result);

            var fx = (FunctionExpression)result;
            Assert.AreEqual(1, fx.Parameters.Parameters.Length);
            Assert.IsInstanceOf<VariableExpression>(fx.Parameters.Parameters[0]);

            var x = (VariableExpression)fx.Parameters.Parameters[0];
            Assert.AreEqual("x", x.Name);

            Assert.IsInstanceOf<SimpleStatement>(fx.Body);

            var body = (SimpleStatement)fx.Body;
            Assert.IsInstanceOf<ObjectExpression>(body.Expression);
        }

        [Test]
        public void ParseSimpleFunctionWithTwoArguments()
        {
            var result = "(x,y)=>new {}".ToExpression();

            Assert.IsInstanceOf<FunctionExpression>(result);

            var fx = (FunctionExpression)result;
            Assert.AreEqual(2, fx.Parameters.Parameters.Length);
            Assert.IsInstanceOf<VariableExpression>(fx.Parameters.Parameters[0]);
            Assert.IsInstanceOf<VariableExpression>(fx.Parameters.Parameters[1]);

            var x = (VariableExpression)fx.Parameters.Parameters[0];
            Assert.AreEqual("x", x.Name);
            var y = (VariableExpression)fx.Parameters.Parameters[1];
            Assert.AreEqual("y", y.Name);

            Assert.IsInstanceOf<SimpleStatement>(fx.Body);

            var body = (SimpleStatement)fx.Body;
            Assert.IsInstanceOf<ObjectExpression>(body.Expression);
        }

        [Test]
        public void ParseSimpleFunctionWithThreeArguments()
        {
            var result = "(x,y, abc)=>new{}".ToExpression();

            Assert.IsInstanceOf<FunctionExpression>(result);

            var fx = (FunctionExpression)result;
            Assert.AreEqual(3, fx.Parameters.Parameters.Length);
            Assert.IsInstanceOf<VariableExpression>(fx.Parameters.Parameters[0]);
            Assert.IsInstanceOf<VariableExpression>(fx.Parameters.Parameters[1]);
            Assert.IsInstanceOf<VariableExpression>(fx.Parameters.Parameters[2]);

            var x = (VariableExpression)fx.Parameters.Parameters[0];
            Assert.AreEqual("x", x.Name);
            var y = (VariableExpression)fx.Parameters.Parameters[1];
            Assert.AreEqual("y", y.Name);
            var abc = (VariableExpression)fx.Parameters.Parameters[2];
            Assert.AreEqual("abc", abc.Name);

            Assert.IsInstanceOf<SimpleStatement>(fx.Body);

            var body = (SimpleStatement)fx.Body;
            Assert.IsInstanceOf<ObjectExpression>(body.Expression);
        }

        [Test]
        public void ParseSimpleFunctionWithSingleNakedArgument()
        {
            var result = "_=>new{}".ToExpression();

            Assert.IsInstanceOf<FunctionExpression>(result);

            var fx = (FunctionExpression)result;
            Assert.AreEqual(1, fx.Parameters.Parameters.Length);
            Assert.IsInstanceOf<VariableExpression>(fx.Parameters.Parameters[0]);

            var underscore = (VariableExpression)fx.Parameters.Parameters[0];
            Assert.AreEqual("_", underscore.Name);

            Assert.IsInstanceOf<SimpleStatement>(fx.Body);

            var body = (SimpleStatement)fx.Body;
            Assert.IsInstanceOf<ObjectExpression>(body.Expression);
        }
    }
}
