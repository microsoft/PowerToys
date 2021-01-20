namespace Mages.Core.Tests
{
    using Mages.Core.Runtime;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;

    [TestFixture]
    public class StringifyTests
    {
        [Test]
        public void StringifyNullIsUndefined()
        {
            var value = default(Object);
            var result = Stringify.This(value);
            var undefined = Stringify.Undefined();
            Assert.AreEqual(undefined, result);
        }

        [Test]
        public void StringifyDictionaryIsObject()
        {
            var dict = new Dictionary<String, Object>
            {
                { "Foo", "Bar" }
            };
            var value = default(Object);
            value = dict;
            var result = Stringify.This(value);
            var obj = Stringify.This(dict);
            Assert.AreEqual(obj, result);
        }

        [Test]
        public void StringifyDoubleIsNumber()
        {
            var dbl = 2.0;
            var value = default(Object);
            value = dbl;
            var result = Stringify.This(value);
            var number = Stringify.This(dbl);
            Assert.AreEqual(number, result);
        }

        [Test]
        public void StringifyBooleanIsBoolean()
        {
            var bln = true;
            var value = default(Object);
            value = bln;
            var result = Stringify.This(value);
            var boolean = Stringify.This(bln);
            Assert.AreEqual(boolean, result);
        }

        [Test]
        public void StringifyStringIsIdentity()
        {
            var str = "Hallo";
            var value = default(Object);
            value = str;
            var result = Stringify.This(value);
            var self = Stringify.This(str);
            Assert.AreEqual(self, result);
        }

        [Test]
        public void StringifyDoubleArrayIsMatrix()
        {
            var array = new Double[,]
            {
                { 1, 2 }, 
                { 3, 4 }
            };
            var value = default(Object);
            value = array;
            var result = Stringify.This(value);
            var mat = Stringify.This(array);
            Assert.AreEqual(mat, result);
        }

        [Test]
        public void StringifyDelegateIsFunction()
        {
            var del = new Function(args => null);
            var value = default(Object);
            value = del;
            var result = Stringify.This(value);
            var func = Stringify.This(del);
            Assert.AreEqual(func, result);
        }

        [Test]
        public void StringifyDoubleUsesInvariantCulture()
        {
            var dbl = 2.8;
            var thread = Thread.CurrentThread;
            var culture = thread.CurrentUICulture;
            thread.CurrentCulture = new CultureInfo("de-de");
            Assert.AreEqual("2,8", dbl.ToString());
            Assert.AreEqual("2.8", Stringify.This(dbl));
            thread.CurrentCulture = culture;
        }
    }
}
