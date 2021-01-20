namespace Mages.Core.Tests
{
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    [TestFixture]
    public class PluginTests
    {
        [Test]
        public void AddingNewPluginShouldNotOverrideExistingObjects()
        {
            var metaData = new Dictionary<String, String>();
            var content = new Dictionary<String, Object>();
            content["sin"] = 3.0;
            var plugin = new Plugin(metaData, content);
            var engine = new Engine();
            engine.AddPlugin(plugin);

            var sin = engine.Interpret("sin");

            Assert.AreNotEqual(content["sin"], sin);
        }

        [Test]
        public void AddingNewPluginShouldAddNewObjects()
        {
            var metaData = new Dictionary<String, String>();
            var content = new Dictionary<String, Object>();
            content["foo"] = 3.0;
            var plugin = new Plugin(metaData, content);
            var engine = new Engine();
            engine.AddPlugin(plugin);

            var foo = engine.Interpret("foo");

            Assert.AreEqual(content["foo"], foo);
        }

        [Test]
        public void AddingNewPluginShouldAddAllNew()
        {
            var metaData = new Dictionary<String, String>();
            var content = new Dictionary<String, Object>();
            content["a"] = new Function(args => (Double)args.Length);
            content["b"] = 2.0;
            var plugin = new Plugin(metaData, content);
            var engine = new Engine();
            engine.AddPlugin(plugin);

            var five = engine.Interpret("a(1, 2, 3) + b");

            Assert.AreEqual(5.0, five);
        }

        [Test]
        public void RemovingInexistingPluginHasNoEffect()
        {
            var metaData = new Dictionary<String, String>();
            var content = new Dictionary<String, Object>();
            content["a"] = new Function(args => (Double)args.Length);
            var plugin = new Plugin(metaData, content);
            var engine = new Engine();
            engine.Globals["a"] = content["a"];
            engine.RemovePlugin(plugin);

            var three = engine.Interpret("a(1, 2, 3)");

            Assert.AreEqual(3.0, three);
        }

        [Test]
        public void RemovingExistingPluginRemovesNewFunction()
        {
            var metaData = new Dictionary<String, String>();
            var content = new Dictionary<String, Object>();
            content["a"] = new Function(args => (Double)args.Length);
            var plugin = new Plugin(metaData, content);
            var engine = new Engine();
            engine.AddPlugin(plugin);
            engine.RemovePlugin(plugin);

            var undefined = engine.Interpret("a(1, 2, 3)");

            Assert.AreEqual(null, undefined);
        }

        [Test]
        public void AddPluginFromStaticClassByConvention()
        {
            var engine = new Engine();
            var plugin = engine.AddPlugin(typeof(MyPlugin));

            var matrix = engine.Interpret("bar * numberOfArguments(1, 2, 3) * identity");

            CollectionAssert.AreEquivalent(new Double[,] { { 6.0, 0.0 }, { 0.0, 6.0 } }, (Double[,])matrix);
            Assert.AreEqual("Foo", plugin.Name);
            Assert.AreEqual(3, plugin.MetaData.Count());
            Assert.AreEqual(4, plugin.Content.Count());
        }

        [Test]
        public void AddPluginFailsFromNormalClass()
        {
            var engine = new Engine();
            var plugin = engine.AddPlugin(typeof(NotAPlugin));

            Assert.IsNull(plugin);
        }

        [Test]
        public void ExposeFunctionFromPluginViaFuncDelegate()
        {
            var engine = new Engine();
            var plugin = engine.AddPlugin(typeof(FunctionPlugin));

            var result = engine.Interpret("capture(5)()");

            Assert.IsNotNull(plugin);
            Assert.AreEqual(5.0, result);
        }

        static class MyPlugin
        {
            public static readonly String Name = "Foo";
            public static readonly String Version = "1.0.0";
            public static readonly String Author = "Author";

            public static Double Bar { get { return 2.0; } }
            public static Boolean Foo { get { return true; } }
            public static Double[,] Identity { get { return new Double[2, 2] { { 1.0, 0.0 }, { 0.0, 1.0 } }; } }
            public static Object NumberOfArguments(Object[] args)
            {
                return (Double)args.Length;
            }
        }

        static class FunctionPlugin
        {
            public static Func<Object[], Object> Capture(Double five)
            {
                return args => five;
            }
        }

        class NotAPlugin
        {
        }
    }
}
