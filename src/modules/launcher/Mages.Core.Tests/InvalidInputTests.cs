namespace Mages.Core.Tests
{
    using Mages.Core.Ast;
    using Mages.Core.Ast.Expressions;
    using Mages.Core.Ast.Statements;
    using Mages.Core.Ast.Walkers;
    using NUnit.Framework;
    using System.Collections.Generic;
    using System;

    [TestFixture]
    public class InvalidInputTests
    {
        [Test]
        public void EscapeSequenceClosedDirectlyShouldNotThrowException()
        {
            var expr = "\"\\\"".ToExpression();
            Assert.IsInstanceOf<ConstantExpression>(expr);
            IsInvalid(expr);
        }

        [Test]
        public void BareWhileStatementShouldNotThrowException()
        {
            var stmt = "while true {}".ToStatement();
            Assert.IsInstanceOf<WhileStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void NonBodiedWhileStatementShouldNotThrowException()
        {
            var stmt = "while (0)".ToStatement();
            Assert.IsInstanceOf<WhileStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void ScopeInConditionOfWhileStatementShouldNotThrowException()
        {
            var stmt = "while (0 {}) {}".ToStatement();
            Assert.IsInstanceOf<WhileStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void ForStatementWithMissingOperandsShouldFail()
        {
            var stmt = "for() { }".ToStatement();
            Assert.IsInstanceOf<ForStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void ForStatementWithIncompleteBlockShouldFail()
        {
            var stmt = "for() { ".ToStatement();
            Assert.IsInstanceOf<ForStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void ForStatementWithMissingRestShouldFail()
        {
            var stmt = "for(k=0 { }".ToStatement();
            Assert.IsInstanceOf<ForStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void ForStatementWithCommasInHeadShouldFail()
        {
            var stmt = "for(k=0, k ~= 2, k++) { }".ToStatement();
            Assert.IsInstanceOf<ForStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void ForStatementWithIncompleteInitializationShouldFail()
        {
            var stmt = "for(k=; k ~= 2 ; k++) { }".ToStatement();
            Assert.IsInstanceOf<ForStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void ForStatementWithStatementInInitializationShouldFail()
        {
            var stmt = "for(break; k ~= 2 ; k++) { }".ToStatement();
            Assert.IsInstanceOf<ForStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void ForStatementWithStatementInAfterThoughtShouldFail()
        {
            var stmt = "for(; ; var k = 0) { }".ToStatement();
            Assert.IsInstanceOf<ForStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void ForStatementWithoutBodyShouldFail()
        {
            var stmt = "for(; ; )".ToStatement();
            Assert.IsInstanceOf<ForStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void DeleteWithoutPayloadIsInvalid()
        {
            var stmt = "delete".ToStatement();
            Assert.IsInstanceOf<SimpleStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void DeleteWithNumberPayloadIsInvalid()
        {
            var stmt = "delete 2".ToStatement();
            Assert.IsInstanceOf<SimpleStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void DeleteWithBooleanPayloadIsInvalid()
        {
            var stmt = "delete true".ToStatement();
            Assert.IsInstanceOf<SimpleStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void DeleteWithMemberCallPayloadIsInvalid()
        {
            var stmt = "delete a.b()".ToStatement();
            Assert.IsInstanceOf<SimpleStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void DeleteWithCallExpressionPayloadIsInvalid()
        {
            var stmt = "delete foo()".ToStatement();
            Assert.IsInstanceOf<SimpleStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void DeleteWithBinaryExpressionPayloadIsInvalid()
        {
            var stmt = "delete (foo+bar)".ToStatement();
            Assert.IsInstanceOf<SimpleStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void OptionalArgumentsNeedToBeEnclosedInBrackets()
        {
            var expr = "x = 3 => x".ToExpression();
            Assert.IsInstanceOf<AssignmentExpression>(expr);
            IsInvalid(expr);
        }

        [Test]
        public void OptionalArgumentsNeedValidAssignment()
        {
            var expr = "(x = , y = 3) => x".ToExpression();
            Assert.IsInstanceOf<FunctionExpression>(expr);
            IsInvalid(expr);
        }

        [Test]
        public void ListOfParameterNamesCanBeRetrievedIfAssignmentIsBroken()
        {
            var expr = "(x = , y = 3) => x".ToExpression();
            Assert.IsInstanceOf<FunctionExpression>(expr);
            var parameters = ((FunctionExpression)expr).Parameters;
            CollectionAssert.AreEquivalent(new[] { Po("x"), Po("y") }, parameters.Names);
            IsInvalid(expr);
        }

        [Test]
        public void ListOfParameterNamesCanBeRetrievedIfBodyIsMissing()
        {
            var expr = "(x,y,z) => {".ToExpression();
            Assert.IsInstanceOf<FunctionExpression>(expr);
            var parameters = ((FunctionExpression)expr).Parameters;
            CollectionAssert.AreEquivalent(new[] { Pr("x"), Pr("y"), Pr("z") }, parameters.Names);
            IsInvalid(expr);
        }

        [Test]
        public void ListOfParameterNamesCanBeRetrievedIfInvalidArgumentSpecified()
        {
            var expr = "(x,0,z) => {}".ToExpression();
            Assert.IsInstanceOf<FunctionExpression>(expr);
            var parameters = ((FunctionExpression)expr).Parameters;
            CollectionAssert.AreEquivalent(new[] { Pr("x"), Pr(null), Pr("z") }, parameters.Names);
            IsInvalid(expr);
        }

        [Test]
        public void ListOfParameterNamesCanBeRetrievedIfArgumentIsMissing()
        {
            var expr = "(x,,z) => {}".ToExpression();
            Assert.IsInstanceOf<FunctionExpression>(expr);
            var parameters = ((FunctionExpression)expr).Parameters;
            CollectionAssert.AreEquivalent(new[] { Pr("x"), Pr(null), Pr("z") }, parameters.Names);
            IsInvalid(expr);
        }

        [Test]
        public void PatternMatchingWithMissingReferenceShouldFail()
        {
            var stmt = "match() { }".ToStatement();
            Assert.IsInstanceOf<MatchStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void PatternMatchingWithInvalidCaseShouldFail()
        {
            var stmt = "match(x) { y }".ToStatement();
            Assert.IsInstanceOf<MatchStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void PatternMatchingWithMisplacedBreakShouldFail()
        {
            var stmt = "match(x) { y { } break; }".ToStatement();
            Assert.IsInstanceOf<MatchStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void PatternMatchingWithMissingCaseBodyShouldFail()
        {
            var stmt = "match(x) { y z }".ToStatement();
            Assert.IsInstanceOf<MatchStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void PatternMatchingWithMisplacedBracketsShouldFail()
        {
            var stmt = "match(x) { y [foo] }".ToStatement();
            Assert.IsInstanceOf<MatchStatement>(stmt);
            IsInvalid(stmt);
        }

        [Test]
        public void PatternMatchingWithArithmeticExpressionShouldFail()
        {
            var stmt = "match(x) { 2+3 { } }".ToStatement();
            Assert.IsInstanceOf<MatchStatement>(stmt);
            IsInvalid(stmt);
        }

        private static void IsInvalid(IWalkable element)
        {
            var errors = new List<ParseError>();
            var validator = new ValidationTreeWalker(errors);
            element.Accept(validator);
            Assert.IsTrue(errors.Count > 0);
        }

        private static ParameterDefinition Pr(String v)
        {
            return P(v, true);
        }

        private static ParameterDefinition Po(String v)
        {
            return P(v, false);
        }

        private static ParameterDefinition P(String v, Boolean r)
        {
            if (v != null)
            {
                return new ParameterDefinition(v, r);
            }

            return default(ParameterDefinition);
        }
    }
}
