namespace Mages.Core.Tests
{
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;

    [TestFixture]
    public class AuxiliariesTests
    {
        [Test]
        public void ReduceWithNumberShouldYieldSingleCall()
        {
            var result = "reduce(add, 2, 3)".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void ReduceWithBooleanShouldYieldSingleCall()
        {
            var result = "reduce(and, true, false)".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void ReduceWithMatrixShouldYieldOneValue()
        {
            var result = "reduce(add, 0, [1, 2, 3, 4, 5, 6])".Eval();
            Assert.AreEqual(21.0, result);
        }

        [Test]
        public void ReduceWithObjectShouldYieldOneValue()
        {
            var result = "reduce(mul, 1, new { a: 5, b: 4, c: 3 })".Eval();
            Assert.AreEqual(60.0, result);
        }

        [Test]
        public void WhereWithNumberSatisfiedShouldYieldValue()
        {
            var result = "where(gt(3), 5)".Eval();
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void WhereWithNumberNotSatisfiedShouldYieldNull()
        {
            var result = "where(lt(3), 5)".Eval();
            Assert.AreEqual(null, result);
        }

        [Test]
        public void WhereWithMatrixPartiallySatisfiedShouldYieldVector()
        {
            var result = "where(gt(4), [1, 2, 3, 4, 5, 6])".Eval();
            var vector = result as Double[,];
            Assert.IsNotNull(vector);
            Assert.AreEqual(2, vector.Length);
            Assert.AreEqual(5.0, vector[0, 0]);
            Assert.AreEqual(6.0, vector[0, 1]);
        }

        [Test]
        public void WhereWithMatrixFullySatisfiedShouldYieldVector()
        {
            var result = "where(gt(0), [1, 2; 3, 4; 5, 6])".Eval();
            var vector = result as Double[,];
            Assert.IsNotNull(vector);
            Assert.AreEqual(6, vector.Length);
            Assert.AreEqual(1.0, vector[0, 0]);
            Assert.AreEqual(2.0, vector[0, 1]);
            Assert.AreEqual(3.0, vector[0, 2]);
            Assert.AreEqual(4.0, vector[0, 3]);
            Assert.AreEqual(5.0, vector[0, 4]);
            Assert.AreEqual(6.0, vector[0, 5]);
        }

        [Test]
        public void WhereWithObjectNotSatisfiedShouldYieldEmptyObject()
        {
            var result = "where(lt(0), new { a: 5, b: 4, c: 3 })".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(0, array.Count);
        }

        [Test]
        public void WhereWithObjectPartiallySatisfiedShouldYieldObject()
        {
            var result = "where(lt(5), new { a: 5, b: 4, c: 3 })".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(2, array.Count);
            Assert.AreEqual(4.0, array["b"]);
            Assert.AreEqual(3.0, array["c"]);
        }

        [Test]
        public void WhereWithObjectFullySatisfiedShouldYieldObject()
        {
            var result = "where(gt(0), new { a: 5, b: 4, c: 3 })".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(3, array.Count);
            Assert.AreEqual(5.0, array["a"]);
            Assert.AreEqual(4.0, array["b"]);
            Assert.AreEqual(3.0, array["c"]);
        }

        [Test]
        public void ZipWithObjectsResultInArrayObject()
        {
            var result = "zip(new { c: 4 }, new { a: 5, b: 4, c: 3 })".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(1, array.Count);
            var subarray = array["0"] as IDictionary<String, Object>;
            Assert.IsNotNull(subarray);
            Assert.AreEqual(4.0, subarray["0"]);
            Assert.AreEqual(5.0, subarray["1"]);
        }

        [Test]
        public void ZipWithSwitchedObjectsResultInArrayObjectWithReverseEntry()
        {
            var result = "zip(new { a: 5, b: 4, c: 3 }, new { c: 4 })".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(1, array.Count);
            var subarray = array["0"] as IDictionary<String, Object>;
            Assert.IsNotNull(subarray);
            Assert.AreEqual(5.0, subarray["0"]);
            Assert.AreEqual(4.0, subarray["1"]);
        }

        [Test]
        public void ZipWithMatrixAndObjectResultInArrayObject()
        {
            var result = "zip([1,2,3], new { a: 5, b: 4, c: 3 })".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(3, array.Count);
            var subarray1 = array["0"] as IDictionary<String, Object>;
            var subarray2 = array["1"] as IDictionary<String, Object>;
            var subarray3 = array["2"] as IDictionary<String, Object>;
            Assert.AreEqual(1.0, subarray1["0"]);
            Assert.AreEqual(5.0, subarray1["1"]);
            Assert.AreEqual(2.0, subarray2["0"]);
            Assert.AreEqual(4.0, subarray2["1"]);
            Assert.AreEqual(3.0, subarray3["0"]);
            Assert.AreEqual(3.0, subarray3["1"]);
        }

        [Test]
        public void ZipWithMatrixAndNumberResultInArrayObject()
        {
            var result = "zip([1,2,3], -5)".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(1, array.Count);
            var subarray = array["0"] as IDictionary<String, Object>;
            Assert.AreEqual(1.0, subarray["0"]);
            Assert.AreEqual(-5.0, subarray["1"]);
        }

        [Test]
        public void ZipWithBooleanAndStringResultInArrayObject()
        {
            var result = "zip(false, \"foo\")".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(1, array.Count);
            var subarray = array["0"] as IDictionary<String, Object>;
            Assert.AreEqual(false, subarray["0"]);
            Assert.AreEqual("foo", subarray["1"]);
        }

        [Test]
        public void ZipWitMatricesResultInArrayObject()
        {
            var result = "zip([1,2,3], [4,5])".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(2, array.Count);
            var subarray1 = array["0"] as IDictionary<String, Object>;
            var subarray2 = array["1"] as IDictionary<String, Object>;
            Assert.AreEqual(1.0, subarray1["0"]);
            Assert.AreEqual(4.0, subarray1["1"]);
            Assert.AreEqual(2.0, subarray2["0"]);
            Assert.AreEqual(5.0, subarray2["1"]);
        }

        [Test]
        public void ConcatWithObjectAndBooleanResultInArrayObject()
        {
            var result = "concat(new { c: 4, foo: \"bar\" }, true)".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(3, array.Count);
            Assert.AreEqual(4.0, array["0"]);
            Assert.AreEqual("bar", array["1"]);
            Assert.AreEqual(true, array["2"]);
        }

        [Test]
        public void ConcatWithObjectsResultInArrayObject()
        {
            var result = "concat(new { a: 5, b: 4, c: 3 }, new { c: 4 })".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(4, array.Count);
            Assert.AreEqual(5.0, array["0"]);
            Assert.AreEqual(4.0, array["1"]);
            Assert.AreEqual(3.0, array["2"]);
            Assert.AreEqual(4.0, array["3"]);
        }

        [Test]
        public void ConcatWithMatrixAndObjectResultInArrayObject()
        {
            var result = "concat([1,2,3], new { a: 5, b: 4, c: 3 })".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(6, array.Count);
            Assert.AreEqual(1.0, array["0"]);
            Assert.AreEqual(2.0, array["1"]);
            Assert.AreEqual(3.0, array["2"]);
            Assert.AreEqual(5.0, array["3"]);
            Assert.AreEqual(4.0, array["4"]);
            Assert.AreEqual(3.0, array["5"]);
        }

        [Test]
        public void ConcatWithNumberAndNumberResultInArrayObject()
        {
            var result = "concat(5, -5)".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(2, array.Count);
            Assert.AreEqual(5.0, array["0"]);
            Assert.AreEqual(-5.0, array["1"]);
        }

        [Test]
        public void ConcatWithMatricesResultInArrayObject()
        {
            var result = "concat([1,2,3,4], [1,2])".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(6, array.Count);
            Assert.AreEqual(1.0, array["0"]);
            Assert.AreEqual(2.0, array["1"]);
            Assert.AreEqual(3.0, array["2"]);
            Assert.AreEqual(4.0, array["3"]);
            Assert.AreEqual(1.0, array["4"]);
            Assert.AreEqual(2.0, array["5"]);
        }

        [Test]
        public void IntersectionBetweenTwoObjectsWithMatchingKeyButValueMismatch()
        {
            var result = "intersect(new { a: 2, c: 4 }, new { a: 9 })".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(0, array.Count);
        }

        [Test]
        public void IntersectionBetweenTwoObjectsWithMatchingKeyAndValue()
        {
            var result = "intersect(new { a: 2, c: 4 }, new { a: 2 })".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(1, array.Count);
            Assert.AreEqual(2.0, array["a"]);
        }

        [Test]
        public void UnionBetweenTwoObjectsWithTwoKeysDifferentInValue()
        {
            var result = "union(new { a: 2, c: 4 }, new { a: 9 })".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(2, array.Count);
            var subarray = array["a"] as IDictionary<String, Object>;
            Assert.AreEqual(2.0, subarray["0"]);
            Assert.AreEqual(9.0, subarray["1"]);
            Assert.AreEqual(4.0, array["c"]);
        }

        [Test]
        public void UnionBetweenTwoObjectWithTwoEqualInValue()
        {
            var result = "union(new { a: 2, c: 4 }, new { a: 2 })".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(2, array.Count);
            Assert.AreEqual(2.0, array["a"]);
            Assert.AreEqual(4.0, array["c"]);
        }

        [Test]
        public void ExceptBetweenTwoObjectsWithIdenticalValue()
        {
            var result = "except(new { a: 2 }, new { a: 2, c: 4 })".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(1, array.Count);
            Assert.AreEqual(4.0, array["c"]);
        }

        [Test]
        public void ExceptBetweenTwoObjectsWithDifferentValue()
        {
            var result = "except(new { a: 9 }, new { a: 2, c: 4 })".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(2, array.Count);
            Assert.AreEqual(2.0, array["a"]);
            Assert.AreEqual(4.0, array["c"]);
        }

        [Test]
        public void KeysOfMatrixYieldNumbers()
        {
            var result = "keys(1:5)".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(5, array.Count);
            Assert.AreEqual(0.0, array["0"]);
            Assert.AreEqual(1.0, array["1"]);
            Assert.AreEqual(2.0, array["2"]);
            Assert.AreEqual(3.0, array["3"]);
            Assert.AreEqual(4.0, array["4"]);
        }

        [Test]
        public void KeysOfObjectYieldsStrings()
        {
            var result = "keys(new { a: 5, c: \"foo\", z: true })".Eval();
            var array = result as IDictionary<String, Object>;
            Assert.IsNotNull(array);
            Assert.AreEqual(3, array.Count);
            Assert.AreEqual("a", array["0"]);
            Assert.AreEqual("c", array["1"]);
            Assert.AreEqual("z", array["2"]);
        }

        [Test]
        public void AnyOfEmptyObjectIsFalse()
        {
            var result = "any(new { })".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void AnyOfObjectWithAnyTrueIsTrue()
        {
            var result = "any(new { b: 0, c: false, h: true })".Eval();
            Assert.AreEqual(true, result);
        }

        [Test]
        public void AllOfObjectWithSomeFalseIsFalse()
        {
            var result = "all(new { b: 0, c: false, h: true })".Eval();
            Assert.AreEqual(false, result);
        }

        [Test]
        public void AllOfMatrixWithNoZerosIsTrue()
        {
            var result = "all(1:10)".Eval();
            Assert.AreEqual(true, result);
        }
    }
}
