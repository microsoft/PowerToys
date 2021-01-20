namespace Mages.Core.Tests
{
    using Mages.Core.Ast.Expressions;
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class ComposedExpressionTests
    {
        [Test]
        public void TightBracketStatement()
        {
            var result = "(2+3)*2".ToExpression();

            Assert.IsInstanceOf<BinaryExpression.Multiply>(result);

            var multiply = (BinaryExpression)result;

            Assert.IsInstanceOf<ArgumentsExpression>(multiply.LValue);
            Assert.IsInstanceOf<ConstantExpression>(multiply.RValue);

            var brackets = (ArgumentsExpression)multiply.LValue;

            Assert.AreEqual(1, brackets.Arguments.Length);
            Assert.IsInstanceOf<BinaryExpression.Add>(brackets.Arguments[0]);

            var add = (BinaryExpression.Add)brackets.Arguments[0];

            Assert.IsInstanceOf<ConstantExpression>(add.LValue);
            Assert.IsInstanceOf<ConstantExpression>(add.RValue);
        }

        [Test]
        public void RelaxedBracketStatement()
        {
            var result = " ( 2 + 3 ) * 2 ".ToExpression();

            Assert.IsInstanceOf<BinaryExpression.Multiply>(result);

            var multiply = (BinaryExpression)result;

            Assert.IsInstanceOf<ArgumentsExpression>(multiply.LValue);
            Assert.IsInstanceOf<ConstantExpression>(multiply.RValue);

            var brackets = (ArgumentsExpression)multiply.LValue;

            Assert.AreEqual(1, brackets.Arguments.Length);
            Assert.IsInstanceOf<BinaryExpression.Add>(brackets.Arguments[0]);

            var add = (BinaryExpression.Add)brackets.Arguments[0];

            Assert.IsInstanceOf<ConstantExpression>(add.LValue);
            Assert.IsInstanceOf<ConstantExpression>(add.RValue);
        }

        [Test]
        public void MemberOperatorFromLeftSide()
        {
            var result = "a.b.c".ToExpression();

            Assert.IsInstanceOf<MemberExpression>(result);

            var member1 = (MemberExpression)result;

            Assert.IsInstanceOf<MemberExpression>(member1.Object);
            Assert.IsInstanceOf<IdentifierExpression>(member1.Member);

            var member2 = (MemberExpression)member1.Object;

            Assert.IsInstanceOf<VariableExpression>(member2.Object);
            Assert.IsInstanceOf<IdentifierExpression>(member2.Member);

            Assert.AreEqual("a", ((VariableExpression)member2.Object).Name);
            Assert.AreEqual("b", ((IdentifierExpression)member2.Member).Name);
            Assert.AreEqual("c", ((IdentifierExpression)member1.Member).Name);
        }

        [Test]
        public void PowerOperatorsFromRightSide()
        {
            var result = "1^2^3^4".ToExpression();

            Assert.IsInstanceOf<BinaryExpression.Power>(result);

            var power1 = (BinaryExpression)result;

            Assert.IsInstanceOf<BinaryExpression.Power>(power1.RValue);

            var power2 = (BinaryExpression)power1.RValue;

            Assert.IsInstanceOf<BinaryExpression.Power>(power2.RValue);

            var power3 = (BinaryExpression)power2.RValue;

            Assert.AreEqual(1.0, (Double)((ConstantExpression)power1.LValue).Value);
            Assert.AreEqual(2.0, (Double)((ConstantExpression)power2.LValue).Value);
            Assert.AreEqual(3.0, (Double)((ConstantExpression)power3.LValue).Value);
            Assert.AreEqual(4.0, (Double)((ConstantExpression)power3.RValue).Value);
        }

        [Test]
        public void FunctionCallWithoutArguments()
        {
            var result = "f()".ToExpression();

            Assert.IsInstanceOf<CallExpression>(result);

            var call = (CallExpression)result;

            var function = call.Function as VariableExpression;
            var arguments = call.Arguments;

            Assert.AreEqual("f", function.Name);
            Assert.AreEqual(0, arguments.Arguments.Length);

            Assert.AreEqual(1, call.Start.Column);
            Assert.AreEqual(3, call.End.Column);
        }

        [Test]
        public void FunctionCallWithTwoArguments()
        {
            var result = "f(1, a)".ToExpression();

            Assert.IsInstanceOf<CallExpression>(result);

            var call = (CallExpression)result;

            var function = call.Function as VariableExpression;
            var arguments = call.Arguments;

            Assert.AreEqual("f", function.Name);
            Assert.AreEqual(2, arguments.Arguments.Length);
            Assert.IsInstanceOf<ConstantExpression>(arguments.Arguments[0]);
            Assert.IsInstanceOf<VariableExpression>(arguments.Arguments[1]);

            Assert.AreEqual(1, call.Start.Column);
            Assert.AreEqual(7, call.End.Column);
        }

        [Test]
        public void FunctionCallWithThreeArguments()
        {
            var result = "f(1,a,\"hi\")".ToExpression();

            Assert.IsInstanceOf<CallExpression>(result);

            var call = (CallExpression)result;

            var function = call.Function as VariableExpression;
            var arguments = call.Arguments;

            Assert.AreEqual("f", function.Name);
            Assert.AreEqual(3, arguments.Arguments.Length);
            Assert.IsInstanceOf<ConstantExpression>(arguments.Arguments[0]);
            Assert.IsInstanceOf<VariableExpression>(arguments.Arguments[1]);
            Assert.IsInstanceOf<ConstantExpression>(arguments.Arguments[2]);

            Assert.AreEqual(1, call.Start.Column);
            Assert.AreEqual(11, call.End.Column);
        }

        [Test]
        public void ConditionWithRangeShouldYieldRange()
        {
            var result = "c ? a : 2:3:1".ToExpression();

            Assert.IsInstanceOf<RangeExpression>(result);

            var range = (RangeExpression)result;
            var condition = (ConditionalExpression)range.From;

            Assert.IsInstanceOf<VariableExpression>(condition.Condition);
            Assert.IsInstanceOf<VariableExpression>(condition.Primary);
            Assert.IsInstanceOf<ConstantExpression>(condition.Secondary);
            Assert.IsInstanceOf<ConstantExpression>(range.Step);
            Assert.IsInstanceOf<ConstantExpression>(range.To);
        }

        [Test]
        public void ConditionWithConditionShouldYieldRightResult()
        {
            var result = "c ? a ? 1 : 2 : 3".ToExpression();

            Assert.IsInstanceOf<ConditionalExpression>(result);

            var condition = (ConditionalExpression)result;
            Assert.IsInstanceOf<VariableExpression>(condition.Condition);
            Assert.IsInstanceOf<ConditionalExpression>(condition.Primary);
            Assert.IsInstanceOf<ConstantExpression>(condition.Secondary);
        }

        [Test]
        public void UnaryExpressionOnPowerShouldActOnUnaryExpression()
        {
            var result = "-2^2".ToExpression();

            Assert.IsInstanceOf<BinaryExpression.Power>(result);

            var power = (BinaryExpression.Power)result;

            Assert.IsInstanceOf<PreUnaryExpression.Minus>(power.LValue);
            Assert.IsInstanceOf<ConstantExpression>(power.RValue);

            var unary = (PreUnaryExpression.Minus)power.LValue;

            Assert.IsInstanceOf<ConstantExpression>(unary.Value);
        }
    }
}
