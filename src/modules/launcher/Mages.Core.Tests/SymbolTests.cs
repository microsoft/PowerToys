namespace Mages.Core.Tests
{
    using Mages.Core.Ast;
    using NUnit.Framework;
    using System;
    using System.Linq;

    [TestFixture]
    public class SymbolTests
    {
        [Test]
        public void ReadUnknownVariable()
        {
            var source = "a";
            TestBare(source, new[] { "a" });
        }

        [Test]
        public void ReadKnownVariable()
        {
            var source = "a = 1";
            TestBare(source, new String[] { });
        }

        [Test]
        public void ReadKnownAndUnknownVariablesWithoutGlobalScope()
        {
            var source = "d = 5; a = b + c * d";
            TestBare(source, new[] { "b", "c" });
        }

        [Test]
        public void ReadKnownAndUnknownVariablesWithGlobalScope()
        {
            var source = "d = 5; a = b + c * d";
            TestEngine(source, new [] { "b" }, new[] { "c" });
        }

        [Test]
        public void ReadUnknownVariableInLocalScopeWithoutGlobalScope()
        {
            var source = "f = (x) => x * y; g = f(z)";
            TestBare(source, new[] { "y", "z" });
        }

        [Test]
        public void ReaKnownVariableInLocalScopeWithGlobalScope()
        {
            var source = "f = (x) => x * y; g = f(z)";
            TestEngine(source, new[] { "y" }, new[] { "z" });
        }

        [Test]
        public void ReadUnknownVariableInLocalScopeWithGlobalScope()
        {
            var source = "f = (x) => x * y; g = f(z)";
            TestEngine(source, new[] { "z" }, new[] { "y" });
        }

        [Test]
        public void ReadUnknownVariableInLocalScopeWithGlobalScopeAndFunctions()
        {
            var source = "f = (x) => sin(x) * cos(y); g = f(y)";
            TestEngine(source, new[] { "y" }, new String[0]);
        }

        private static void TestBare(String source, String[] unknown)
        {
            var parser = new ExpressionParser();
            var statements = parser.ParseStatements(source);
            var actual = statements.FindMissingSymbols().Select(m => m.Name).ToArray();

            CollectionAssert.AreEquivalent(unknown, actual);
        }

        private static void TestEngine(String source, String[] available, String[] unknown)
        {
            var engine = new Engine();

            foreach (var variable in available)
            {
                engine.Scope[variable] = null;
            }

            var actual = engine.FindMissingSymbols(source).Select(m => m.Name).ToArray();

            CollectionAssert.AreEquivalent(unknown, actual);
        }
    }
}
