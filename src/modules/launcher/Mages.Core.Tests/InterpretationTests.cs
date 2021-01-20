namespace Mages.Core.Tests
{
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;

    [TestFixture]
    public class InterpretationTests
    {
        [Test]
        public void FactorialOfNegativeNumberShouldBeNegative()
        {
            Test("-3!", -6.0);
        }

        [Test]
        public void AddAndMultiplyNumbersShouldYieldRightResult()
        {
            Test("2+3*4", 14.0);
        }

        [Test]
        public void CallPreviouslyCreatedFunction()
        {
            Test("((x, y) => x + y)(2, 3)", 5.0);
        }

        [Test]
        public void CallFunctionCreatedFromFunction()
        {
            Test("(x => y => x + y)(2)(3)", 5.0);
        }

        [Test]
        public void CallFunctionStoredInGlobalVariable()
        {
            var scope = Test("A = (x, y) => x + y; B = A(2, 3); A(4, 3)", 7.0);
            Assert.AreEqual(5.0, scope["B"]);
            Assert.IsInstanceOf<Function>(scope["A"]);
        }

        [Test]
        public void CallFunctionStoredInGlobalFunctionAndCreatedFromFunction()
        {
            var scope = Test("A = x => y => x + y; B = A(2); C = B(3); B(4)", 6.0);
            Assert.AreEqual(5.0, scope["C"]);
            Assert.IsInstanceOf<Function>(scope["A"]);
            Assert.IsInstanceOf<Function>(scope["B"]);
        }

        [Test]
        public void CompilationWorksAndCanBeExecutedRepeaditly()
        {
            var engine = new Engine();
            var func = engine.Compile("A = 6; B = 7; A * B");
            var result1 = func.Invoke();
            var result2 = func.Invoke();
            Assert.AreEqual(42.0, result1);
            Assert.AreEqual(42.0, result2);
        }

        [Test]
        public void CompilationUsingScopeWorksAndCanBeExecutedRepeaditlyWithDifferentInputs()
        {
            var engine = new Engine();
            var func = engine.Compile("A * B - C");
            engine.Scope["A"] = 1.0;
            engine.Scope["B"] = 2.0;
            engine.Scope["C"] = 0.0;
            var result1 = func.Invoke();
            engine.Scope["B"] = 3.0;
            engine.Scope["C"] = 4.0;
            var result2 = func.Invoke();
            Assert.AreEqual(2.0, result1);
            Assert.AreEqual(-1.0, result2);
        }

        [Test]
        public void InitializeObjectAndAccessEntry()
        {
            var scope = Test("A = new { a: 4, b: 6 }; A.a", 4.0);
            var obj = scope["A"] as IDictionary<String, Object>;

            Assert.IsNotNull(obj);
            Assert.AreEqual(4.0, obj["a"]);
            Assert.AreEqual(6.0, obj["b"]);
        }

        [Test]
        public void InitializeObjectAndChangeEntry()
        {
            var scope = Test("A = new { a: 4, b: 6 }; A.b = 7.0", 7.0);
            var obj = scope["A"] as IDictionary<String, Object>;

            Assert.IsNotNull(obj);
            Assert.AreEqual(4.0, obj["a"]);
            Assert.AreEqual(7.0, obj["b"]);
        }

        [Test]
        public void InitializeObjectAndAddEntry()
        {
            var scope = Test("A = new { a: 4, b: 6 }; A.c = 9.0", 9.0);
            var obj = scope["A"] as IDictionary<String, Object>;

            Assert.IsNotNull(obj);
            Assert.AreEqual(4.0, obj["a"]);
            Assert.AreEqual(6.0, obj["b"]);
            Assert.AreEqual(9.0, obj["c"]);
        }

        [Test]
        public void SetMatrixEntryWithValidValueAndSingleIndexAfterCreatingIt()
        {
            var scope = Test("A = [1,2;3,4]; A(1) = 0.0", 0.0);
            var mat = scope["A"] as Double[,];

            Assert.IsNotNull(mat);
            Assert.AreEqual(1.0, mat[0, 0]);
            Assert.AreEqual(0.0, mat[0, 1]);
            Assert.AreEqual(3.0, mat[1, 0]);
            Assert.AreEqual(4.0, mat[1, 1]);
        }

        [Test]
        public void SetMatrixEntryWithValidValueAfterCreatingIt()
        {
            var scope = Test("A = [1,2;3,4]; A(1,1) = 0.0", 0.0);
            var mat = scope["A"] as Double[,];

            Assert.IsNotNull(mat);
            Assert.AreEqual(1.0, mat[0, 0]);
            Assert.AreEqual(2.0, mat[0, 1]);
            Assert.AreEqual(3.0, mat[1, 0]);
            Assert.AreEqual(0.0, mat[1, 1]);
        }

        [Test]
        public void SetMatrixEntryWithInvalidValueAfterCreatingIt()
        {
            var scope = Test("A = [1,2;3,4]; A(1,1) = \"hi there\"; A(0,0)", 1.0);
            var mat = scope["A"] as Double[,];

            Assert.IsNotNull(mat);
            Assert.AreEqual(1.0, mat[0, 0]);
            Assert.AreEqual(2.0, mat[0, 1]);
            Assert.AreEqual(3.0, mat[1, 0]);
            Assert.AreEqual(Double.NaN, mat[1, 1]);
        }

        [Test]
        public void SetMatrixEntryWithConvertedStringValueAfterCreatingIt()
        {
            var scope = Test("A = [1,2;3,4]; A(1,1) = \"23\"; A(1, 0)", 3.0);
            var mat = scope["A"] as Double[,];

            Assert.IsNotNull(mat);
            Assert.AreEqual(1.0, mat[0, 0]);
            Assert.AreEqual(2.0, mat[0, 1]);
            Assert.AreEqual(3.0, mat[1, 0]);
            Assert.AreEqual(23.0, mat[1, 1]);
        }

        [Test]
        public void SetObjectWithValueAfterCreatingIt()
        {
            var scope = Test("A = new {}; A(\"a\") = 5", 5.0);
            var obj = scope["A"] as IDictionary<String, Object>;

            Assert.IsNotNull(obj);
            Assert.AreEqual(5.0, obj["a"]);
        }

        [Test]
        public void ModifyObjectWithValueAfterCreatingIt()
        {
            var scope = Test("A = new { a: 0 }; A(\"a\") = \"test\"; A(\"b\") = 17.3", 17.3);
            var obj = scope["A"] as IDictionary<String, Object>;

            Assert.IsNotNull(obj);
            Assert.AreEqual("test", obj["a"]);
            Assert.AreEqual(17.3, obj["b"]);
        }

        [Test]
        public void SetObjectWithNumericIndexAfterCreatingIt()
        {
            var scope = Test("A = new { }; A(2) = 17.3", 17.3);
            var obj = scope["A"] as IDictionary<String, Object>;

            Assert.IsNotNull(obj);
            Assert.AreEqual(17.3, obj["2"]);
        }

        [Test]
        public void SetObjectWithBooleanIndexAfterCreatingIt()
        {
            var scope = Test("A = new { }; A(true) = 17.3", 17.3);
            var obj = scope["A"] as IDictionary<String, Object>;

            Assert.IsNotNull(obj);
            Assert.AreEqual(17.3, obj["true"]);
        }

        [Test]
        public void FunctionLocalVariableRemainsLocal()
        {
            var scope = Test("(() => { var x = 5; return x + 9; })()", 14.0);

            Assert.AreEqual(0, scope.Count);
        }

        [Test]
        public void FunctionLocalVariableRemainsLocalAndDoesNotRequireTrailingSemicolon()
        {
            var scope = Test("((x, y) => { var z = 5; return x + y + z; })(2, 3)", 10.0);

            Assert.AreEqual(0, scope.Count);
        }

        [Test]
        public void FunctionGlobalAssignmentChangesScope()
        {
            var scope = Test("(x => { y = 5; return x + y; })(2)", 7.0);

            Assert.AreEqual(1, scope.Count);
            Assert.AreEqual(5.0, scope["y"]);
        }

        [Test]
        public void FunctionReturnStatementWithPayloadPreventsFurtherEvaluation()
        {
            Test("(x => { var y = 5; return x + y; 2 * y; })(2)", 7.0);
        }

        [Test]
        public void FunctionReturnStatementWithoutPayloadPreventsFurtherEvaluation()
        {
            var scope = Test("y = (x => { var y = 5; return; 2 * y; })(2); 0.0", 0.0);

            Assert.AreEqual(1, scope.Count);
            Assert.IsNull(scope["y"]);
        }

        [Test]
        public void GlobalReturnStatementStopsEvaluation()
        {
            Test("x = 5; y = 10; return x + y; return x * y", 15.0);
        }

        [Test]
        public void WhileStatementWorksFineWithBodyDedicatedPreIncrement()
        {
            Test("i = 0; n = 0; while (i < 5) { n = i + n; ++i; } n + i", 15.0);
        }

        [Test]
        public void WhileStatementWorksFineWithBodyMixedPostIncrement()
        {
            Test("i = 0; n = 0; while (i < 6) { n = i++ + n; } n + i", 21.0);
        }

        [Test]
        public void WhileStatementWorksFineWithConditionPostIncrement()
        {
            Test("i = 0; n = 0; while (i++ < 5) { n = i + n; } n + i", 21.0);
        }

        [Test]
        public void WhileStatementWorksFineWithContinue()
        {
            Test("n = 0; while (n < 1) { n = 1; continue; n = 0; }n", 1.0);
        }

        [Test]
        public void WhileStatementWorksFineWithBreak()
        {
            Test("n = 5; while (n > 0) { n = 0; break; n = 1; }n", 0.0);
        }

        [Test]
        public void WhileStatementWorksWithoutTrailingSemicolon()
        {
            Test("n = 0; while (n == 0) { n = 1; }n", 1.0);
        }

        [Test]
        public void WhileStatementBodySkippedWorks()
        {
            Test("n = 0; while (false) { n = 1; }n", 0.0);
        }

        [Test]
        public void IfStatementWithBlocksShouldBeInPrimary()
        {
            Test("n = 0; i = 3; if (i > 2) { n = 1; } else { n = -1; } n", 1.0);
        }

        [Test]
        public void IfStatementWithBlocksShouldBeInSecondary()
        {
            Test("n = 0; i = 3; if (i < 2) { n = 1; } else { n = -1; } n", -1.0);
        }

        [Test]
        public void IfStatementWithoutElseShouldNotDoAnything()
        {
            Test("n = 0; i = 3; if (i < 2) { n = 1; } n", 0.0);
        }

        [Test]
        public void IfStatementWithoutBlocksShouldBeInPrimary()
        {
            Test("n = 0; i = 3; if (i > 2) n = 1; else n = -1; n", 1.0);
        }

        [Test]
        public void IfStartementWithoutBlocksShouldBeInSecondary()
        {
            Test("n = 0; i = 3; if (i < 2) n = 1; else n = -1; n", -1.0);
        }

        [Test]
        public void ForLoopWorksWithStandardWay()
        {
            Test("sum = 0; for (var i = 0; i < 5; ++i) sum += i; sum", 10.0);
        }

        [Test]
        public void ForDeclaresVariableInLocalScope()
        {
            Test("i = -1; sum = 0; (() => { for (var i = 0; i < 5; ++i) sum += i; })(); i", -1.0);
        }

        [Test]
        public void ForUsesVariableInGlobalScope()
        {
            Test("i = -1; sum = 0; (() => { for (i = 0; i < 5; ++i) sum += i; })(); i", 5.0);
        }

        [Test]
        public void ForLoopChangesGlobalVariableIfNotScoped()
        {
            Test("i = -1; sum = 0; for (var i = 0; i < 5; ++i) sum += i; i", 5.0);
        }

        [Test]
        public void ForLoopChangesGlobalVariableIfNotDeclared()
        {
            Test("i = -1; sum = 0; for (i = 0; i < 5; ++i) sum += i; i", 5.0);
        }

        [Test]
        public void DeleteDoesNotRemoveLocalVariable()
        {
            var scope = Test("y = 1; x = 5; (() => { var x = 7; delete y; return x; })()", 7.0);
            Assert.IsTrue(scope.ContainsKey("y"));
        }

        [Test]
        public void DeleteRemovesLocalVariable()
        {
            Test("x = 5; (() => { var x = 7; delete x; return x; })()", 5.0);
        }

        [Test]
        public void DeleteRemovesGlobalVariable()
        {
            var scope = Test("x = 5; delete x; 1.0", 1.0);
            Assert.IsFalse(scope.ContainsKey("x"));
        }

        [Test]
        public void DeleteDoesNotRemoveGlobalVariable()
        {
            var scope = Test("x = 5; delete y; 1.0", 1.0);
            Assert.IsTrue(scope.ContainsKey("x"));
        }

        [Test]
        public void DeleteRemovesKeyOfObject()
        {
            var scope = Test("x = 5; o = new { z: 5 }; delete o.z; x", 5.0);
            Assert.IsTrue(scope.ContainsKey("x"));
            Assert.IsTrue(scope.ContainsKey("o"));
            Assert.AreEqual(0, ((IDictionary<String, Object>)scope["o"]).Count);
        }

        [Test]
        public void DeleteDoesNotRemoveInvalidKeyOfObject()
        {
            var scope = Test("x = 5; o = new { z: 5 }; delete o.y; x", 5.0);
            Assert.IsTrue(scope.ContainsKey("x"));
            Assert.IsTrue(scope.ContainsKey("o"));
            Assert.AreEqual(1, ((IDictionary<String, Object>)scope["o"]).Count);
        }

        [Test]
        public void DeleteRemovesKeyOfObjectInObject()
        {
            var scope = Test("x = 5; o = new { o: new { x: 5, y: 3 } }; delete o.o.y; x", 5.0);
            Assert.IsTrue(scope.ContainsKey("x"));
            Assert.IsTrue(scope.ContainsKey("o"));
            Assert.AreEqual(1, ((IDictionary<String, Object>)scope["o"]).Count);
            Assert.AreEqual(1, ((IDictionary<String, Object>)((IDictionary<String, Object>)scope["o"])["o"]).Count);
        }

        [Test]
        public void DeleteIsAnExpressionWithBooleanResult()
        {
            Test("x = 3; y = delete x; y ? 1 : 0", 1.0);
        }

        [Test]
        public void DeleteFailsReturnsFalse()
        {
            Test("x = 3; y = delete z; y ? 1 : 0", 0.0);
        }

        [Test]
        public void DeleteCanBeUsedAsCondition()
        {
            Test("x = 0; y = 1; if (delete y) x = 5; x - 4", 1.0);
        }

        [Test]
        public void OptionalArgumentRemainsUnused()
        {
            Test("f = (x, y = 3) => x * y; f(2, 5)", 10.0);
        }

        [Test]
        public void OptionalArgumentIsUsedRespectively()
        {
            Test("f = (x, y = 3) => x * y; f(2)", 6.0);
        }

        [Test]
        public void OptionalArgumentIsUsedFromCurriedVersion()
        {
            Test("f = (x, y = 3) => x * y; f()(2)", 6.0);
        }

        [Test]
        public void OptionalArgumentIsNotUsedFromCurriedVersionIfAllSupplied()
        {
            Test("f = (x, y = 3) => x * y; f()(2, 4)", 8.0);
        }

        [Test]
        public void OptionalArgumentsCanAccessArgumentsFromTheLeft()
        {
            Test("x = 10; f = (x, y = x) => x * y; f(3)", 9.0);
        }

        [Test]
        public void OptionalArgumentsCannotAccessArgumentsFromTheRight()
        {
            Test("y = 5; f = (x = y, y = 4) => x * y; f()", 20.0);
        }

        [Test]
        public void MultipleOptionalArgumentsAreUsedAccordingly()
        {
            Test("f = (x = 1, y = 3, z = 5) => x + z * y; f()", 16.0);
        }

        [Test]
        public void DeleteDoesNotRemoveInvalidKeyOfObjectInObject()
        {
            var scope = Test("x = 5; o = new { o: new { x: 5, y: 3 } }; delete o.o.z; x", 5.0);
            Assert.IsTrue(scope.ContainsKey("x"));
            Assert.IsTrue(scope.ContainsKey("o"));
            Assert.AreEqual(1, ((IDictionary<String, Object>)scope["o"]).Count);
            Assert.AreEqual(2, ((IDictionary<String, Object>)((IDictionary<String, Object>)scope["o"])["o"]).Count);
        }

        private static IDictionary<String, Object> Test(String sourceCode, Double expected, Double tolerance = 0.0)
        {
            var engine = new Engine();
            var result = engine.Interpret(sourceCode) as Double?;

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(expected, result.Value, tolerance);
            return engine.Scope;
        }
    }
}
