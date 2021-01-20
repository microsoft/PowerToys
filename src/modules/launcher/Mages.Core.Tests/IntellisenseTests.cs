namespace Mages.Core.Tests
{
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    [TestFixture]
    public class IntellisenseTests
    {
        [Test]
        public void EmptyScopeAndSourceYieldsKeywords()
        {
            var source = "";
            var engine = new Engine();
            engine.Globals.Clear();
            var autocomplete = engine.GetCompletionAt(source, 0).ToArray();
            var available = Keywords.GlobalStatementKeywords.Concat(Keywords.ExpressionKeywords);

            CollectionAssert.AreEquivalent(available, autocomplete);
        }

        [Test]
        public void GlobalScopeAndEmptySourceYieldsKeywordsAndVariables()
        {
            var source = "";
            var engine = new Engine();
            var autocomplete = engine.GetCompletionAt(source, 0).ToArray();
            var available = Keywords.GlobalStatementKeywords.Concat(Keywords.ExpressionKeywords).Concat(engine.Globals.Keys);

            CollectionAssert.AreEquivalent(available, autocomplete);
        }

        [Test]
        public void LocalScopeYieldsKeywordsAndLocalVariables()
        {
            var source = "(() => { var x = 5; })";
            var engine = new Engine();
            engine.Globals.Clear();
            var autocomplete = engine.GetCompletionAt(source, source.Length - 2).ToArray();
            var available = Keywords.GlobalStatementKeywords.Concat(Keywords.ExpressionKeywords).Concat(new[] { "x" });

            CollectionAssert.AreEquivalent(available, autocomplete);
        }

        [Test]
        public void OutsideLocalScopeYieldsKeywordsAndVariables()
        {
            var source = "(() => { var x = 5; });";
            var engine = new Engine();
            engine.Globals.Clear();
            var autocomplete = engine.GetCompletionAt(source, source.Length).ToArray();
            var available = Keywords.GlobalStatementKeywords.Concat(Keywords.ExpressionKeywords);

            CollectionAssert.AreEquivalent(available, autocomplete);
        }

        [Test]
        public void InGlobalScopeAfterAssignmentRightHandSideYieldsVariables()
        {
            var source = "x = 5; var y = 9; 7 +";
            var engine = new Engine();
            engine.Globals.Clear();
            var autocomplete = engine.GetCompletionAt(source, source.Length).ToArray();
            var available = Keywords.ExpressionKeywords.Concat(new []{ "x", "y" });

            CollectionAssert.AreEquivalent(available, autocomplete);
        }

        [Test]
        public void ParametersOfFunctionShouldBeIncludedInCompletionList()
        {
            var source = "((a, b, c) => { var x = 5; })";
            var engine = new Engine();
            engine.Globals.Clear();
            var autocomplete = engine.GetCompletionAt(source, source.Length - 2).ToArray();
            var available = Keywords.GlobalStatementKeywords.Concat(Keywords.ExpressionKeywords).Concat(new[] { "x", "a", "b", "c" });

            CollectionAssert.AreEquivalent(available, autocomplete);
        }

        [Test]
        public void ParameterOfFunctionShouldYieldNoCompletion()
        {
            var source = "((a, b, c) => { var x = 5; })";
            var engine = new Engine();
            engine.Globals.Clear();
            var autocomplete = engine.GetCompletionAt(source, 3).ToArray();
            var available = new String[0];

            CollectionAssert.AreEquivalent(available, autocomplete);
        }

        [Test]
        public void MemberOfObjectShouldYieldKeysDirectly()
        {
            var source = "o.";
            var engine = new Engine();
            engine.Globals.Clear();
            engine.Scope.Add("o", new Dictionary<String, Object>
            {
                { "abc", 0.0 },
                { "abd", 5.0 },
            });
            var autocomplete = engine.GetCompletionAt(source, 2).ToArray();
            var available = new [] { "abc", "abd" };

            CollectionAssert.AreEquivalent(available, autocomplete);
        }

        [Test]
        public void MemberOfObjectShouldYieldKeysWithPrefix()
        {
            var source = "o.a";
            var engine = new Engine();
            engine.Globals.Clear();
            engine.Scope.Add("o", new Dictionary<String, Object>
            {
                { "abc", 0.0 },
                { "abd", 5.0 },
            });
            var autocomplete = engine.GetCompletionAt(source, 3).ToArray();
            var available = new[] { "a|bc", "a|bd" };

            CollectionAssert.AreEquivalent(available, autocomplete);
        }

        [Test]
        public void MemberOfNonObjectShouldYieldNothing()
        {
            var source = "o.";
            var engine = new Engine();
            engine.Globals.Clear();
            engine.Scope.Add("o", 23.0);
            var autocomplete = engine.GetCompletionAt(source, 2).ToArray();
            var available = new String[0];

            CollectionAssert.AreEquivalent(available, autocomplete);
        }

        [Test]
        public void MemberOfNestedObjectShouldYieldInnerKeys()
        {
            var source = "o.cd.";
            var engine = new Engine();
            engine.Globals.Clear();
            engine.Scope.Add("o", new Dictionary<String, Object>
            {
                { "abc", 0.0 },
                { "abd", 5.0 },
                { "cd", new Dictionary<String, Object> { { "a", 5.0 }, { "b", 7.0 } } }
            });
            var autocomplete = engine.GetCompletionAt(source, 5).ToArray();
            var available = new[] { "a", "b" };

            CollectionAssert.AreEquivalent(available, autocomplete);
        }

        [Test]
        public void MemberOfNestedObjectWithCallShouldYieldNothing()
        {
            var source = "o.cd().";
            var engine = new Engine();
            engine.Globals.Clear();
            engine.Scope.Add("o", new Dictionary<String, Object>
            {
                { "abc", 0.0 },
                { "abd", 5.0 },
                { "cd", new Dictionary<String, Object> { { "a", 5.0 }, { "b", 7.0 } } }
            });
            var autocomplete = engine.GetCompletionAt(source, 7).ToArray();
            var available = new String[0];

            CollectionAssert.AreEquivalent(available, autocomplete);
        }

        [Test]
        public void ParameterOfFunctionShouldBeFoundWithPrefix()
        {
            var source = "abc => { ab";
            var engine = new Engine();
            engine.Globals.Clear();
            var autocomplete = engine.GetCompletionAt(source, 11).ToArray();
            var available = new[] { "ab|c" };

            CollectionAssert.AreEquivalent(available, autocomplete);
        }
    }
}
