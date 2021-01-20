namespace Mages.Core.Tests
{
    using Mages.Core.Ast;
    using Mages.Core.Ast.Expressions;
    using Mages.Core.Ast.Statements;
    using Mages.Core.Ast.Walkers;
    using Mages.Core.Runtime;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;

    [TestFixture]
    public class StatementTests
    {
        [Test]
        public void ParseTwoAssignmentStatements()
        {
            var source = "d = 5; a = b + c * d";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(2, statements.Count);
            Assert.IsInstanceOf<SimpleStatement>(statements[0]);
            Assert.IsInstanceOf<SimpleStatement>(statements[1]);

            var assignment1 = (statements[0] as SimpleStatement).Expression as AssignmentExpression;
            var assignment2 = (statements[1] as SimpleStatement).Expression as AssignmentExpression;

            Assert.IsNotNull(assignment1);
            Assert.IsNotNull(assignment2);

            Assert.AreEqual("d", assignment1.VariableName);
            Assert.AreEqual("a", assignment2.VariableName);
            Assert.AreEqual(5.0, (Double)((ConstantExpression)assignment1.Value).Value);
            Assert.IsInstanceOf<BinaryExpression.Add>(assignment2.Value);
        }

        [Test]
        public void ParseOneReturnStatementWithEmptyPayload()
        {
            var source = "return";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<ReturnStatement>(statements[0]);

            var return1 = (statements[0] as ReturnStatement).Expression as EmptyExpression;

            Assert.IsNotNull(return1);
        }

        [Test]
        public void ParseOneReturnStatementWithConstantPayload()
        {
            var source = "return 5";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<ReturnStatement>(statements[0]);

            var return1 = (statements[0] as ReturnStatement).Expression as ConstantExpression;

            Assert.IsNotNull(return1);
        }

        [Test]
        public void ParseOneBreakStatementWithoutPayload()
        {
            var source = "break";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<BreakStatement>(statements[0]);

            var errors = Validate(statements);

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(ErrorCode.LoopMissing, errors[0].Code);
        }

        [Test]
        public void ParseOneBreakStatementWithPayloadShouldContainErrors()
        {
            var source = "break true";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<BreakStatement>(statements[0]);

            var errors = Validate(statements);

            Assert.AreEqual(2, errors.Count);
            Assert.AreEqual(ErrorCode.LoopMissing, errors[0].Code);
            Assert.AreEqual(ErrorCode.TerminatorExpected, errors[1].Code);
        }

        [Test]
        public void ParseOneContinueStatementWithoutPayload()
        {
            var source = "continue";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<ContinueStatement>(statements[0]);

            var errors = Validate(statements);

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(ErrorCode.LoopMissing, errors[0].Code);
        }

        [Test]
        public void ParseOneContinueStatementWithPayloadShouldContainErrors()
        {
            var source = "continue 2+3";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<ContinueStatement>(statements[0]);

            var errors = Validate(statements);

            Assert.AreEqual(2, errors.Count);
            Assert.AreEqual(ErrorCode.LoopMissing, errors[0].Code);
            Assert.AreEqual(ErrorCode.TerminatorExpected, errors[1].Code);
        }

        [Test]
        public void ParseEmptyIfStatementShouldBeFine()
        {
            var source = "if () {}";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<IfStatement>(statements[0]);

            var errors = Validate(statements);

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void ParseIfStatementWithNoStatementsShouldBeFine()
        {
            var source = "if (true) {}";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<IfStatement>(statements[0]);
            Assert.IsInstanceOf<BlockStatement>(((IfStatement)statements[0]).Primary);

            var errors = Validate(statements);

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void ParseIfStatementWithSingleStatementsShouldBeFine()
        {
            var source = "if (true) n = 2 + 3";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<IfStatement>(statements[0]);
            Assert.IsInstanceOf<SimpleStatement>(((IfStatement)statements[0]).Primary);

            var errors = Validate(statements);

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void ParseIfStatementWithMissingTailShouldYieldError()
        {
            var source = "if (true) { n = 2 + 3";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<IfStatement>(statements[0]);
            Assert.IsInstanceOf<BlockStatement>(((IfStatement)statements[0]).Primary);

            var errors = Validate(statements);

            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void ParseIfStatementWithMissingClosingScopeShouldYieldError()
        {
            var source = "if (true) { n = 2 + 3;";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<IfStatement>(statements[0]);
            Assert.IsInstanceOf<BlockStatement>(((IfStatement)statements[0]).Primary);

            var errors = Validate(statements);

            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void ParseIfStatementWithComposedConditionAndSingleStatementInBlockShouldBeFine()
        {
            var source = "if (a + b + c == d / 2) { n = k; }";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<IfStatement>(statements[0]);
            Assert.IsInstanceOf<BlockStatement>(((IfStatement)statements[0]).Primary);

            var errors = Validate(statements);

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void ParseWhileStatementWithMissingCloseCurlyBracketShouldYieldError()
        {
            var source = "while (true) {";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<WhileStatement>(statements[0]);
            Assert.IsInstanceOf<BlockStatement>(((WhileStatement)statements[0]).Body);

            var errors = Validate(statements);

            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void ParseWhileStatementWithEmptyStatementShouldBeFine()
        {
            var source = "while (true);";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<WhileStatement>(statements[0]);
            Assert.IsInstanceOf<SimpleStatement>(((WhileStatement)statements[0]).Body);

            var errors = Validate(statements);

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void ParseTwoStatementWhereReturnHasBinaryPayload()
        {
            var source = "var x = 9; return x + pi";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(2, statements.Count);
            Assert.IsInstanceOf<VarStatement>(statements[0]);
            Assert.IsInstanceOf<ReturnStatement>(statements[1]);

            var assignment1 = (statements[0] as VarStatement).Assignment as AssignmentExpression;
            var return1 = (statements[1] as ReturnStatement).Expression as BinaryExpression.Add;

            Assert.IsNotNull(assignment1);
            Assert.IsNotNull(return1);

            Assert.AreEqual("x", assignment1.VariableName);

            var errors = Validate(statements);

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void ParseNestedWhileLoopsShouldSeeNestedBlocks()
        {
            var source = "while (i < 5) { n++; while (i < 4) { i++; } i++; }";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<WhileStatement>(statements[0]);
            Assert.IsInstanceOf<BlockStatement>(((WhileStatement)statements[0]).Body);

            var errors = Validate(statements);

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void IfBlockWithEndedElseShouldBeAnError()
        {
            var source = "if () { } else";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<IfStatement>(statements[0]);
            Assert.IsInstanceOf<BlockStatement>(((IfStatement)statements[0]).Primary);

            var errors = Validate(statements);

            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void ForStatementWithEmptyBodyShouldBeOkay()
        {
            var source = "for(var i = 0; i < 5; ++i);";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<ForStatement>(statements[0]);
            Assert.IsInstanceOf<SimpleStatement>(((ForStatement)statements[0]).Body);

            var errors = Validate(statements);

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void ForStatementWithEmptyHeadShouldBeOkay()
        {
            var source = "for(; ;) {}";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<ForStatement>(statements[0]);
            Assert.IsInstanceOf<BlockStatement>(((ForStatement)statements[0]).Body);

            var errors = Validate(statements);

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void ForStatementWithRelaxedEmptyHeadShouldBeOkay()
        {
            var source = "for( ;  ; ) {  }";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<ForStatement>(statements[0]);
            Assert.IsInstanceOf<BlockStatement>(((ForStatement)statements[0]).Body);

            var errors = Validate(statements);

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void ForStatementWithTightEmptyHeadShouldBeOkay()
        {
            var source = "for(;;){}";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<ForStatement>(statements[0]);
            Assert.IsInstanceOf<BlockStatement>(((ForStatement)statements[0]).Body);

            var errors = Validate(statements);

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void ForStatementWithStatementBodyShouldBeOkay()
        {
            var source = "for(k = 0; k ~= 2; k++) { }";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<ForStatement>(statements[0]);
            Assert.IsInstanceOf<BlockStatement>(((ForStatement)statements[0]).Body);

            var errors = Validate(statements);

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void JumpsInFunctionStatementsAreTreatedCorrectly()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(System.Net.Mail.MailAddress)).WithDefaultName();
            engine.SetStatic(typeof(System.Net.Mail.MailMessage)).WithDefaultName();
            engine.SetStatic(typeof(System.Net.NetworkCredential)).WithDefaultName();
            engine.SetStatic(typeof(System.Net.Mail.SmtpClient)).WithDefaultName();
            var source = @"var isBasic = is(""String"");
var isExtended = x => is(""Object"", x) && x(""name"") && x(""mail"");
var getMail = address => {
	if (isBasic(address)) {
		return MailAddress.create(address);
	}

    if (isExtended(address)) {
		return MailAddress.create(address.mail, address.name);
	}
};
var f = (host, port, user, pass, sender, recipient, subject, body) => {
	var smtp = SmtpClient.create(host, port);
	var message = MailMessage.create();

	if (user || pass) {
		smtp.credentials = NetworkCredential.create(user, pass);
	}

	message.from = sender | getMail;
	message.to.add(recipient | getMail);
	message.subject = subject;
	message.body = body;
	return message;
};

f(""foo.com"", 25, ""x@foo"", ""baz"", new { name: ""F"", mail: ""origin@foo.com"" }, new { name: ""R"", mail: ""dest@foo.com"" }, ""Subj"", ""Body"")";
            var result = engine.Interpret(source) as WrapperObject;
            Assert.IsNotNull(result);
            var mailMessage = (System.Net.Mail.MailMessage)result.Content;
            Assert.IsNotNull(mailMessage.From);
            Assert.AreEqual("Body", mailMessage.Body);
            Assert.AreEqual("Subj", mailMessage.Subject);
            Assert.AreEqual("origin@foo.com", mailMessage.From.Address);
            Assert.AreEqual("F", mailMessage.From.DisplayName);
            Assert.AreEqual(1, mailMessage.To.Count);
            Assert.AreEqual("dest@foo.com", mailMessage.To[0].Address);
            Assert.AreEqual("R", mailMessage.To[0].DisplayName);
        }

        [Test]
        public void SimplePatternMatchingIsAlright()
        {
            var source = "match(2 + 3) { eq(5) { break; } any { } }";
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);

            Assert.AreEqual(1, statements.Count);
            Assert.IsInstanceOf<MatchStatement>(statements[0]);
            Assert.IsInstanceOf<BlockStatement>(((MatchStatement)statements[0]).Cases);

            var errors = Validate(statements);
            
            Assert.AreEqual(0, errors.Count);
        }

        private static List<ParseError> Validate(List<IStatement> statements)
        {
            var errors = new List<ParseError>();
            var validator = new ValidationTreeWalker(errors);

            statements.ToBlock().Accept(validator);

            return errors;
        }
    }
}
