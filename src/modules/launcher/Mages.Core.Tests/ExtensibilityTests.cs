namespace Mages.Core.Tests
{
    using Mages.Core.Runtime;
    using Mages.Core.Tests.Mocks;
    using NUnit.Framework;
    using Runtime.Functions;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    [TestFixture]
    public class ExtensibilityTests
    {
        [Test]
        public void AddingANewFunctionCanBeUsed()
        {
            var engine = new Engine();
            var function = new Function(args => (Double)args.Length);
            engine.SetFunction("foo", function);

            var result = engine.Interpret("foo(1,2,3)");
            Assert.AreEqual(3.0, result);
        }

        [Test]
        public void AddingANewDelegateCanBeUsed()
        {
            var engine = new Engine();
            var func = new Func<Double, String, Boolean>((n, str) => n == str.Length);
            engine.SetFunction("foo", func);

            var result1 = engine.Interpret("foo(2,\"hi\")");
            var result2 = engine.Interpret("foo(2,\"hallo\")");

            Assert.AreEqual(true, result1);
            Assert.AreEqual(false, result2);
        }

        [Test]
        public void AddingANewMethodInfoCanBeUsed()
        {
            var engine = new Engine();
            var func = new Func<Double, String, Boolean>((n, str) => n == str.Length);
            engine.SetFunction("foo", func.Method, func.Target);

            var result1 = engine.Interpret("foo(2,\"hi\")");
            var result2 = engine.Interpret("foo(2,\"hallo\")");

            Assert.AreEqual(true, result1);
            Assert.AreEqual(false, result2);
        }

        [Test]
        public void AddingANewMethodReturningVoidShouldYieldNull()
        {
            var engine = new Engine();
            var func = new Action<String>(str => Console.WriteLine(str));
            engine.SetFunction("hello", func.Method, func.Target);

            var result = engine.Interpret("hello(\"World\")");

            Assert.AreEqual(null, result);
        }

        [Test]
        public void AddingANewMethodInfoWithArbitraryReturnTypeAndIntParameterCanBeUsed()
        {
            var engine = new Engine();
            var func = new Func<Int32, String, Char>((n, str) => str[n]);
            engine.SetFunction("getCharAt", func.Method, func.Target);

            var result1 = engine.Interpret("getCharAt(1,\"hi\")");
            var result2 = engine.Interpret("getCharAt(2,\"hallo\")");

            Assert.AreEqual("i", result1);
            Assert.AreEqual("l", result2);
        }

        [Test]
        public void AddingANewMethodTakingAVectorAndReturningAList()
        {
            var engine = new Engine();
            var func = new Func<Double[], List<Double>>(vec => vec.Skip(1).Reverse().Take(2).ToList());
            engine.SetFunction("bottom", func.Method, func.Target);

            var result = engine.Interpret("bottom([1,2,3,4,5])");

            CollectionAssert.AreEquivalent(new Double[,] { { 5, 4 } }, (Double[,])result);
        }

        [Test]
        public void AddingANewMethodHasAutoCurryActivatedYieldsRightResult()
        {
            var engine = new Engine();
            var func = new Func<Double, Double, Double, Double>((x, y, z) => x + 2 * y + 3 * z);
            engine.SetFunction("foo", func.Method, func.Target);

            var result = engine.Interpret("foo()(1)(2)(3)");
            Assert.AreEqual(14.0, result);
        }

        [Test]
        public void AddingANewMethodHasAutoCurryActivatedIsEqual()
        {
            var engine = new Engine();
            var func = new Func<Double, Double, Double, Double>((x, y, z) => x + 2 * y + 3 * z);
            engine.SetFunction("foo", func.Method, func.Target);

            var result = engine.Interpret("foo() == foo");
            Assert.AreEqual(true, result);
        }

        [Test]
        public void AddingNewMethodIsAlreadyFunctionIsNotWrapped()
        {
            var engine = new Engine();
            var func = new Func<Object[], Object>(args => (Double)args.Length);
            engine.SetFunction("foo", func.Method, func.Target);

            var result = engine.Interpret("foo(1, 2, 3, 4, 5)");
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void AddingNewMethodIsAlreadyFunctionWillNotAutoCurry()
        {
            var engine = new Engine();
            var func = new Func<Object[], Object>(args => (Double)args.Length);
            engine.SetFunction("foo", func.Method, func.Target);

            var result = engine.Interpret("foo()");
            Assert.AreEqual(0.0, result);
        }

        [Test]
        public void ReplacingAnExistingFunctionOverwrites()
        {
            var engine = new Engine();
            var function = new Function(args => (Double)args.Length);
            engine.SetFunction("sin", function);

            var result = engine.Interpret("sin(1,2)");
            Assert.AreEqual(2.0, result);
        }

        [Test]
        public void AddingAGlobalConstant()
        {
            var engine = new Engine();
            engine.Globals["Answer"] = 42.0;

            var result = engine.Interpret("Answer / 2");
            Assert.AreEqual(21.0, result);
        }

        [Test]
        public void AddingABooleanConstantShouldNotBeConverted()
        {
            var engine = new Engine();
            engine.SetConstant("foo", true);

            var result = engine.Interpret("foo");
            Assert.AreEqual(true, result);
        }

        [Test]
        public void AddingANumberConstantShouldNotBeConverted()
        {
            var engine = new Engine();
            engine.SetConstant("foo", 2.3);

            var result = engine.Interpret("foo");
            Assert.AreEqual(2.3, result);
        }

        [Test]
        public void AddingAnIntegerConstantShouldBeConvertedToANumber()
        {
            var engine = new Engine();
            engine.SetConstant("foo", 2);

            var result = engine.Interpret("foo");
            Assert.AreEqual(2.0, result);
        }

        [Test]
        public void AddingASingleConstantShouldBeConvertedToANumber()
        {
            var engine = new Engine();
            engine.SetConstant("foo", 2f);

            var result = engine.Interpret("foo");
            Assert.AreEqual(2.0, result);
        }

        [Test]
        public void AddingAStringConstantShouldNotBeConverted()
        {
            var engine = new Engine();
            var str = "hallo";
            engine.SetConstant("foo", str);

            var result = engine.Interpret("foo");
            Assert.AreEqual(str, result);
        }

        [Test]
        public void AddingACharacterConstantShouldBeConvertedToAString()
        {
            var engine = new Engine();
            engine.SetConstant("foo", 'c');

            var result = engine.Interpret("foo");
            Assert.AreEqual("c", result);
        }

        [Test]
        public void AddingAMatrixConstantShouldNotBeConverted()
        {
            var engine = new Engine();
            var matrix = new Double[,] { { 1, 2 }, { 3, 4 } };
            engine.SetConstant("foo", matrix);

            var result = engine.Interpret("foo");
            Assert.AreEqual(matrix, result);
        }

        [Test]
        public void AddingADictionaryConstantShouldNotBeConverted()
        {
            var engine = new Engine();
            var obj = new Dictionary<String, Object>
            {
                { "a", true },
                { "b", "hallo" }
            };
            engine.SetConstant("foo", obj);

            var result = engine.Interpret("foo");
            Assert.AreEqual(obj, result);
        }

        [Test]
        public void AddingAPointConstantShouldBeConvertedToAWrapper()
        {
            var engine = new Engine();
            var pt = new Point();
            engine.SetConstant("foo", pt);

            var result = engine.Interpret("foo") as WrapperObject;

            Assert.IsNotNull(result);
            Assert.AreEqual(pt, result.Content);
        }

        [Test]
        public void GlobalPointConstantShouldBeModifyable()
        {
            var engine = new Engine();
            var pt = new Point { y = 7.0 };
            engine.SetConstant("foo", pt);

            var result = engine.Interpret("foo.x = 3; foo.y");

            Assert.AreEqual(7.0, result);
            Assert.AreEqual(3.0, pt.x);
        }

        [Test]
        public void GlobalIndexConstantShouldBeModifyableDespiteIntegerTypes()
        {
            var engine = new Engine();
            var index = new Index();
            engine.SetConstant("foo", index);

            engine.Interpret("foo.col = 2.8; foo.row = -9;");

            Assert.AreEqual(2, index.col);
            Assert.AreEqual(0, index.row);
        }

        [Test]
        public void GlobalIndexConstantPropertiesAndMethodsShouldWork()
        {
            var engine = new Engine();
            var list = new List<String>();
            engine.SetConstant("list", list);

            var result = engine.Interpret("list.add(\"one\");list.add(\"two\");list.insert(1, \"half\");list.count");

            Assert.AreEqual(3.0, result);
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual("one", list[0]);
            Assert.AreEqual("half", list[1]);
            Assert.AreEqual("two", list[2]);
        }

        [Test]
        public void GlobalPropertyTestSetPropertyShouldWork()
        {
            var engine = new Engine();
            var pt = new PropertyTest();
            engine.SetConstant("prop", pt);

            engine.Interpret("prop.test = \"Hi!\"");

            Assert.AreEqual("Hi!", pt.test);
        }

        [Test]
        public void GlobalPropertyTestGetPropertyShouldWork()
        {
            var engine = new Engine();
            var pt = new PropertyTest();
            pt.test = "Ho!";
            engine.SetConstant("prop", pt);

            var result = engine.Interpret("prop.test");

            Assert.AreEqual("Ho!", result);
        }

        [Test]
        public void ProvideSimpleArrayClassShouldWork()
        {
            var engine = new Engine();
            engine.SetStatic<List<Object>>().WithName("Array");

            var result = engine.Interpret("x = Array.create(); x.add(\"one\");x.add(\"two\");x.insert(1, \"half\");x.add(x.count);x.add(x.at(0)); x") as WrapperObject;

            Assert.IsNotNull(result);

            var list = result.Content as List<Object>;

            Assert.IsNotNull(list);
            Assert.AreEqual(5, list.Count);
            Assert.AreEqual("one", list[0]);
            Assert.AreEqual("half", list[1]);
            Assert.AreEqual("two", list[2]);
            Assert.AreEqual(3.0, list[3]);
            Assert.AreEqual("one", list[4]);
        }

        [Test]
        public void ProvideStringBuilderWithoutNameShouldYieldNormalName()
        {
            var engine = new Engine();
            engine.SetStatic<StringBuilder>().WithDefaultName();

            var result = engine.Interpret("sb = StringBuilder.create(); sb.append(\"foo\").append(\"bar\"); sb.toString()");

            Assert.AreEqual("foobar", result);
        }

        [Test]
        public void ContainerLifeTimeIsControlledCorrectlyExplicitly()
        {
            var foo = new PropertyTest();

            var lifetime = Container.Register(foo);
            Assert.AreEqual(foo, Container.GetService<PropertyTest>());
            lifetime.Dispose();
            Assert.IsNull(Container.GetService<PropertyTest>());
        }

        [Test]
        public void ContainerLifeTimeIsControlledCorrectlyImplicitly()
        {
            var foo = new PropertyTest();

            using (Container.Register(foo))
            {
                Assert.AreEqual(foo, Container.GetService<PropertyTest>());
            }

            Assert.IsNull(Container.GetService<PropertyTest>());
        }

        [Test]
        public void ContainerLifeTimeIsControlledCorrectlyExternally()
        {
            var foo = new PropertyTest();

            Container.Register(foo);
            Assert.AreEqual(foo, Container.GetService<PropertyTest>());
            Container.Unregister(foo);
            Assert.IsNull(Container.GetService<PropertyTest>());
        }

        [Test]
        public void NamesAreTakenFromTheCustomNameSelector()
        {
            var engine = new Engine();
            var service = new NameSelectorMock(member =>
            {
                if (member is Type) return "foo";
                if (member is MethodInfo) return member.Name.ToUpper();
                if (member is ConstructorInfo) return "New";
                return member.Name;
            });

            using (Container.Register(service))
            {
                engine.SetStatic<StringBuilder>().WithDefaultName();
                var result = engine.Interpret("sb = foo.New(); sb.APPEND(\"foo\").APPEND(\"bar\"); sb.TOSTRING()");

                Assert.AreEqual("foobar", result);
            }
        }

        [Test]
        public void ProvideMethodsFromStaticClassByScattering()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(Functions)).Scattered();

            var result = engine.Interpret("foo() + bar()");

            Assert.AreEqual("foo0bar1", result);
        }

        [Test]
        public void ProvideMethodsFromStaticClassByNamespaceObject()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(Functions)).WithName("baz");

            var result = engine.Interpret("baz.foo() + baz.bar()");

            Assert.AreEqual("foo0bar1", result);
        }

        [Test]
        public void ProvideMethodsFromStaticClassProvideCurryingCapabilities()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(Functions)).Scattered();

            var result = engine.Interpret("xyz()(1)(2)(3)");

            Assert.AreEqual(14.0, result);
        }

        [Test]
        public void ProvideConstructorFromClassHasCurryCapabilityFourSteps()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(CtorSample)).WithName("Type");

            var result = engine.Interpret("Type.create()(1)(2)(3).value");

            Assert.AreEqual(32.0, result);
        }

        [Test]
        public void ProviderConstructorFromClassHasCurryCapabilityTwoSteps()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(CtorSample)).WithName("Type");

            var result = engine.Interpret("x = Type.create(1, 2)(3); x.value");

            Assert.AreEqual(32.0, result);
        }

        [Test]
        public void ProvideIndexerFromClassHasCurryCapability()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(CtorSample)).WithName("Type");

            var result = engine.Interpret("x = Type.create(1, 2)(3); x.at()(3)(\"hi\")");

            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void RegisteredBooleanTypeFunctionDoesExist()
        {
            var engine = new Engine();
            TypeFunctions.Register<Boolean>(_ => args => args[0]);
            var result = engine.Interpret("true(3)");
            TypeFunctions.Unregister<Boolean>();
            Assert.AreEqual(3.0, result);
        }

        [Test]
        public void BooleanTypeFunctionDoesNotExistByDefault()
        {
            var engine = new Engine();
            var result = engine.Interpret("true(3)");
            Assert.AreEqual(null, result);
        }

        [Test]
        public void RegisteredBooleanTypeProcedureDoesExist()
        {
            var engine = new Engine();
            var captured = default(Object);
            TypeProcedures.Register<Boolean>(_ => (args, value) => { captured = value; });
            engine.Interpret("true(3) = 5");
            TypeFunctions.Unregister<Boolean>();
            Assert.AreEqual(5.0, captured);
        }

        [Test]
        public void BooleanTypeProcedureDoesNotExistByDefault()
        {
            var engine = new Engine();
            var result = engine.Interpret("true(3) = 5");
            Assert.AreEqual(5.0, result);
        }

        [Test]
        public void MatrixExtendedWithXPropertyYieldsX()
        {
            var engine = new Engine();
            AttachedProperties.Register<Double[,]>("x", matrix => matrix[0, 0]);
            var result = engine.Interpret("M = [1,2,3]; M.x");
            AttachedProperties.Unregister<Double[,]>("x");
            Assert.AreEqual(1.0, result);
        }

        [Test]
        public void MatrixExtendedWithXPropertiesYieldsRightValue()
        {
            var engine = new Engine();
            AttachedProperties.Register<Double[,]>("x", matrix => matrix[0, 0]);
            AttachedProperties.Register<Double[,]>("y", matrix => matrix[0, 1]);
            AttachedProperties.Register<Double[,]>("z", matrix => matrix[0, 2]);
            var result = engine.Interpret("M = [1,2,3]; M.x + M.y + M.z");
            AttachedProperties.Unregister<Double[,]>("x");
            AttachedProperties.Unregister<Double[,]>("y");
            AttachedProperties.Unregister<Double[,]>("z");
            Assert.AreEqual(6.0, result);
        }

        [Test]
        public void MatrixHasNoXPropertyByDefault()
        {
            var engine = new Engine();
            var result = engine.Interpret("M = [1,2,3]; M.x");
            Assert.AreEqual(null, result);
        }

        [Test]
        public void WrapperObjectReferencesShouldBeUnwrappedIfSpecified()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(WrapUnwrapTest)).WithName("Test");

            var result = engine.Interpret("x = Test.create(); x.foo(x)");

            Assert.AreEqual(true, result);
        }

        [Test]
        public void FunctionTakingComplexObjectShouldBeUnwrappedIfSpecified()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(WrapUnwrapTest)).WithName("Test");
            var func = new Func<WrapUnwrapTest, Boolean>((obj) => obj != null);
            engine.SetFunction("verify", func.Method, func.Target);

            var result = engine.Interpret("x = Test.create(); verify(x)");

            Assert.AreEqual(true, result);
        }

        [Test]
        public void PropertyShouldConvertTypeByUnpackingIfInterfaceMatches()
        {
            var engine = new Engine();
            engine.SetStatic<Bar>().WithDefaultName();
            engine.SetStatic<SampleFoo>().WithDefaultName();
            var result = engine.Interpret("var bar = Bar.create(); bar.sample = SampleFoo.create(); bar") as WrapperObject;

            Assert.IsNotNull(result);
            Assert.IsInstanceOf<Bar>(result.Content);
            Assert.IsNotNull(((Bar)result.Content).Sample);
        }

        [Test]
        public void ReturnArrayOfElementsAsObjectList()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(Functions)).Scattered();

            var result = engine.Interpret("many()") as IDictionary<String, Object>;
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("Foo", result["0"]);
            Assert.AreEqual("Bar", result["1"]);
            Assert.AreEqual("Baz", result["2"]);
        }

        [Test]
        public void UseStringForOverloadedFunctionInvocation()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(System.Net.Mail.MailAddress)).WithDefaultName();
            engine.SetStatic(typeof(System.Net.Mail.MailMessage)).WithDefaultName();

            var result = engine.Interpret("var message = MailMessage.create(); message.to.add(\"my@bar\"); message") as WrapperObject;
            Assert.IsNotNull(result);
            var message = (System.Net.Mail.MailMessage)result.Content;
            Assert.AreEqual(1, message.To.Count);
            Assert.AreEqual("my@bar", message.To[0].Address);
            Assert.AreEqual("", message.To[0].DisplayName);
        }

        [Test]
        public void UseComplexObjectForOverloadedFunctionInvocation()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(System.Net.Mail.MailAddress)).WithDefaultName();
            engine.SetStatic(typeof(System.Net.Mail.MailMessage)).WithDefaultName();

            var result = engine.Interpret("var message = MailMessage.create(); message.to.add(MailAddress.create(\"my@bar\", \"My Bar\")); message") as WrapperObject;
            Assert.IsNotNull(result);
            var message = (System.Net.Mail.MailMessage)result.Content;
            Assert.AreEqual(1, message.To.Count);
            Assert.AreEqual("my@bar", message.To[0].Address);
            Assert.AreEqual("My Bar", message.To[0].DisplayName);
        }

        [Test]
        public void UseParamsFunctionShouldAcceptZeroArguments()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(Functions)).Scattered();

            var result = engine.Interpret("params()");
            Assert.AreEqual(0.0, result);
        }

        [Test]
        public void UseParamsFunctionShouldAcceptSingleArguments()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(Functions)).Scattered();

            var result = engine.Interpret("params(\"hi\")");
            Assert.AreEqual(1.0, result);
        }

        [Test]
        public void UseParamsFunctionShouldAcceptManyArguments()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(Functions)).Scattered();

            var result = engine.Interpret("params(\"hi\", \"ho\", \"ha\")");
            Assert.AreEqual(3.0, result);
        }

        [Test]
        public void UseParamsFunctionShouldAcceptAndConvertManyArguments()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(Functions)).Scattered();

            var result = engine.Interpret("params(\"hi\", null, \"ha\", \"\")");
            Assert.AreEqual(2.0, result);
        }

        [Test]
        public void ByteArrayTransformationIsAccepted()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(Functions)).Scattered();

            var result = engine.Interpret("4 | loadBin | takeBin");
            Assert.AreEqual(6.0, result);
        }

        [Test]
        public void SingleActionCallbacksMayBeUsedInApis()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(SampleApi)).Scattered();

            var result = engine.Interpret("var y = 0; perform(x => y = x); y");
            Assert.AreEqual(3.0, result);
        }

        [Test]
        public void DoubleFuncCallbacksMayBeUsedInApis()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(SampleApi)).Scattered();

            var result = engine.Interpret("compute((x, y) => x + y)");
            Assert.AreEqual(7.0, result);
        }

        [Test]
        public void QuadFuncCallbacksMayBeUsedInApis()
        {
            var engine = new Engine();
            engine.SetStatic(typeof(SampleApi)).Scattered();

            var result = engine.Interpret("substr((str, start, length) => { var t = \"\"; var end = start + length; while (start < end) { t += str(start++); } return t; })");
            Assert.AreEqual("ha", result);
        }

        sealed class Point
        {
            public Double x = 0;
            public Double y = 0;
        }

        static class SampleApi
        {
            public static void Perform(Action<Double> handler)
            {
                handler.Invoke(3.0);
            }

            public static Double Compute(Func<Double, Double, Double> handler)
            {
                return handler.Invoke(3.0, 4.0);
            }

            public static String Substr(Func<String, Int32, Int32, String> handler)
            {
                return handler.Invoke("hallo", 0, 2);
            }
        }

        sealed class Index
        {
            public Int32 col = 0;
            public UInt16 row = 0;
        }

        sealed class PropertyTest
        {
            public String test { get; set; }
        }

        sealed class WrapUnwrapTest
        {
            public Boolean Foo(WrapUnwrapTest self)
            {
                return Object.ReferenceEquals(this, self);
            }
        }

        static class Functions
        {
            public static String Foo()
            {
                return "foo0";
            }

            public static String Bar()
            {
                return "bar1";
            }

            public static String[] Many()
            {
                return new[] { "Foo", "Bar", "Baz" };
            }

            public static Double Xyz(Double x, Double y, Double z)
            {
                return x + 2 * y + 3 * z;
            }

            public static Double Params(params String[] str)
            {
                return str.Where(m => !String.IsNullOrEmpty(m)).Count();
            }

            public static Byte[] LoadBin(Int32 count)
            {
                var content = new Byte[count];

                for (var i = 0; i < count; i++)
                {
                    content[i] = (Byte)i;
                }

                return content;
            }

            public static Double TakeBin(Byte[] content)
            {
                var sum = 0.0;

                for (var i = 0; i < content.Length; i++)
                {
                    sum += content[i];
                }

                return sum;
            }
        }

        public class CtorSample
        {
            public CtorSample(Double x, Double y, Double z)
            {
                Value = x + y * y + z * z * z;
            }

            public Double this[Int32 x, String y]
            {
                get { return x + y.Length; }
            }

            public Double Value
            {
                get;
                private set;
            }
        }

        public interface IFoo
        {

        }

        class SampleFoo : IFoo
        {

        }

        public class Bar
        {
            public IFoo Sample { get; set; }
        }
    }
}
