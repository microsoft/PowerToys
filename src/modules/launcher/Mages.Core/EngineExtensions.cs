namespace Mages.Core
{
    using Mages.Core.Ast;
    using Mages.Core.Ast.Expressions;
    using Mages.Core.Runtime;
    using Mages.Core.Source;
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// A collection of useful extensions for the engine.
    /// </summary>
    public static class EngineExtensions
    {
        /// <summary>
        /// Adds or replaces a function with the given name to the function layer.
        /// </summary>
        /// <param name="engine">The engine.</param>
        /// <param name="name">The name of the function to add or replace.</param>
        /// <param name="function">The function to be integrated.</param>
        public static void SetFunction(this Engine engine, String name, Function function)
        {
            engine.Globals[name] = function;
        }

        /// <summary>
        /// Adds or replaces a function represented as a general delegate by wrapping
        /// it as a function with the given name.
        /// </summary>
        /// <param name="engine">The engine.</param>
        /// <param name="name">The name of the function to add or replace.</param>
        /// <param name="function">The function to be wrapped.</param>
        public static void SetFunction(this Engine engine, String name, Delegate function)
        {
            engine.Globals[name] = function.WrapFunction();
        }

        /// <summary>
        /// Adds or replaces a function represented as a reflected method info by
        /// wrapping it as a function with the given name.
        /// </summary>
        /// <param name="engine">The engine.</param>
        /// <param name="name">The name of the function to add or replace.</param>
        /// <param name="method">The function to be wrapped.</param>
        /// <param name="target">The optional target object of the method.</param>
        public static void SetFunction(this Engine engine, String name, MethodInfo method, Object target = null)
        {
            engine.Globals[name] = method.WrapFunction(target);
        }

        /// <summary>
        /// Adds or replaces an object represented as the MAGES primitive. This is
        /// either directly the given value or a wrapper around it.
        /// </summary>
        /// <param name="engine">The engine.</param>
        /// <param name="name">The name of the constant to add or replace.</param>
        /// <param name="value">The value to interact with.</param>
        public static void SetConstant(this Engine engine, String name, Object value)
        {
            engine.Globals[name] = value.WrapObject();
        }

        /// <summary>
        /// Exposes all static methods and the type's constructors in the object
        /// that can be freely placed.
        /// </summary>
        /// <typeparam name="T">The type to expose.</typeparam>
        /// <param name="engine">The engine.</param>
        public static IPlacement SetStatic<T>(this Engine engine)
        {
            return engine.SetStatic(typeof(T));
        }

        /// <summary>
        /// Exposes all static methods and the type's constructors in the object
        /// that can be freely placed.
        /// </summary>
        /// <param name="engine">The engine.</param>
        /// <param name="type">The type to expose.</param>
        public static IPlacement SetStatic(this Engine engine, Type type)
        {
            var name = engine.Globals.Keys.FindName(type);
            var obj = type.Expose();
            return new Placement(engine, name, obj);
        }

        /// <summary>
        /// Exposes all types in the assembly that satisfy the optional condition
        /// in an object that can be freely placed.
        /// </summary>
        /// <param name="engine">The engine.</param>
        /// <param name="lib">The library containing the types to expose.</param>
        /// <param name="shouldInclude">The optional inclusion checker.</param>
        public static IPlacement SetStatic(this Engine engine, Assembly lib, Predicate<Type> shouldInclude = null)
        {
            var types = lib.GetExportedTypes();
            var libNameParts = lib.GetName().Name.Split(new[] { '.', ',', ' ', '-', '+' }, StringSplitOptions.RemoveEmptyEntries);
            var libName = String.Join(String.Empty, libNameParts);
            var obj = new Dictionary<String, Object>();

            foreach (var type in types)
            {
                if (shouldInclude == null || shouldInclude.Invoke(type))
                {
                    var name = obj.Keys.FindName(type);
                    var value = type.Expose();
                    obj[name] = value;
                }
            }

            return new Placement(engine, libName, obj);
        }

        /// <summary>
        /// Exposes all types in an object that can be freely placed. Here no
        /// default name is given.
        /// </summary>
        /// <param name="engine">The engine.</param>
        /// <param name="types">The types to include.</param>
        public static IPlacement SetStatic(this Engine engine, IEnumerable<Type> types)
        {
            var obj = new Dictionary<String, Object>();

            foreach (var type in types)
            {
                var name = obj.Keys.FindName(type);
                var value = type.Expose();
                obj[name] = value;
            }

            return new Placement(engine, null, obj);
        }

        /// <summary>
        /// Finds the missing symbols (if any) in the given source.
        /// </summary>
        /// <param name="engine">The engine.</param>
        /// <param name="source">The source code to inspect.</param>
        /// <returns>The variable expressions pointing to the missing symbols.</returns>
        public static IEnumerable<VariableExpression> FindMissingSymbols(this Engine engine, String source)
        {
            var parser = engine.Parser;
            var ast = parser.ParseStatements(source);
            var symbols = ast.ToBlock().FindMissingSymbols();

            for (var i = symbols.Count - 1; i >= 0; i--)
            {
                var symbol = symbols[i];
                var name = symbol.Name;

                if (engine.Scope.ContainsKey(name) || engine.Globals.ContainsKey(name))
                {
                    symbols.RemoveAt(i);
                }
            }

            return symbols;
        }

        /// <summary>
        /// Interprets the given source and returns the result in form of a future.
        /// </summary>
        /// <param name="engine">The engine.</param>
        /// <param name="source">The source to interpret.</param>
        /// <returns>The result in form of a future. Any callbacks will be aggregated here.</returns>
        public static Future InterpretAsync(this Engine engine, String source)
        {
            var future = new Future();
            var result = engine.Interpret(source);
            var promise = result as Future;

            if (promise != null)
            {
                promise.ContinueWith(future);
            }
            else
            {
                future.SetResult(result);
            }

            return future;
        }

        /// <summary>
        /// Adds a plugin from the given type. This requires that the type represents a
        /// static class that ends with "Plugin". Meta-data is given in form of public
        /// static string fields, while static properties and methods are considered
        /// content.
        /// </summary>
        /// <param name="engine">The engine.</param>
        /// <param name="type">The type to represent as a plugin.</param>
        /// <returns>The plugin if any.</returns>
        public static Plugin AddPlugin(this Engine engine, Type type)
        {
            if (type.Name.Length > 6 && type.Name.EndsWith("Plugin"))
            {
                if (type.IsSealed && type.IsAbstract)
                {
                    var plugin = ConstructStaticPlugin(type);
                    engine.AddPlugin(plugin);
                    return plugin;
                }

                var constructor = type.GetConstructor(new[] { typeof(Engine) });

                if (constructor != null)
                {
                    var obj = constructor.Invoke(new[] { engine });
                    var plugin = ConstructInstancePlugin(obj);
                    engine.AddPlugin(plugin);
                    return plugin;
                }
            }

            return null;
        }

        /// <summary>
        /// Adds all plugins found in the given assembly.
        /// </summary>
        /// <param name="engine">The engine.</param>
        /// <param name="assembly">The assembly to investigate.</param>
        /// <returns>The list with all added plugins.</returns>
        public static IEnumerable<Plugin> AddPlugins(this Engine engine, Assembly assembly)
        {
            var types = assembly.GetExportedTypes();
            var plugins = new List<Plugin>();

            foreach (var type in types)
            {
                var plugin = engine.AddPlugin(type);

                if (plugin != null)
                {
                    plugins.Add(plugin);
                }
            }

            return plugins;
        }

        /// <summary>
        /// Gets the currently stored global symbols.
        /// </summary>
        /// <param name="engine">The engine containing the global symbols.</param>
        /// <returns>The enumeration over all global symbols.</returns>
        public static IEnumerable<String> GetGlobalSymbols(this Engine engine)
        {
            foreach (var item in engine.Scope)
            {
                yield return item.Key;
            }

            foreach (var item in engine.Globals)
            {
                yield return item.Key;
            }
        }

        /// <summary>
        /// Gets the currently stored global items, i.e., key-value pairs.
        /// </summary>
        /// <param name="engine">The engine containing the global scope.</param>
        /// <returns>The dictionary with all global items.</returns>
        public static IDictionary<String, Object> GetGlobalItems(this Engine engine)
        {
            var symbols = new Dictionary<String, Object>();

            foreach (var item in engine.Globals)
            {
                symbols.Add(item.Key, item.Value);
            }

            foreach (var item in engine.Scope)
            {
                symbols[item.Key] = item.Value;
            }

            return symbols;
        }

        /// <summary>
        /// Looks up for completion at the given index in the provided source.
        /// </summary>
        /// <param name="engine">The engine.</param>
        /// <param name="source">The source code to use as basis.</param>
        /// <param name="index">The index where the cursor is.</param>
        /// <returns>The enumeration over potential completion symbols.</returns>
        public static IEnumerable<String> GetCompletionAt(this Engine engine, String source, Int32 index)
        {
            var scanner = source.GetScanner();
            var stream = scanner.ToTokenStream();
            var ast = engine.Parser.ParseStatements(stream);
            var items = engine.GetGlobalItems();
            var position = scanner.GetPositionAt(index);
            return ast.GetCompletionAt(position, items);
        }

        internal static void Apply(this Engine engine, Configuration configuration)
        {
            if (!configuration.IsEvalForbidden)
            {
                var func = new Func<String, Object>(engine.Interpret);
                engine.SetFunction("eval", func);
            }

            if (configuration.IsEngineExposed)
            {
                engine.SetConstant("engine", engine);
            }

            if (configuration.IsThisAvailable)
            {
                engine.SetConstant("this", engine.Scope);
            }
        }

        private static Dictionary<String, String> GetMetadata(Type type)
        {
            var flags = BindingFlags.Public | BindingFlags.Static;
            var fields = type.GetFields(flags);
            var meta = new Dictionary<String, String>();

            foreach (var field in fields)
            {
                if (field.FieldType == typeof(String))
                {
                    var name = meta.Keys.FindName(field);
                    var value = field.GetValue(null);
                    meta.Add(name, value as String);
                }
            }

            return meta;
        }

        private static Dictionary<String, Object> GetContent(IEnumerable<PropertyInfo> properties, IEnumerable<MethodInfo> methods, Object instance)
        {
            var content = new Dictionary<String, Object>();

            foreach (var property in properties)
            {
                if (property.GetIndexParameters().Length == 0 && property.CanRead && !property.IsSpecialName)
                {
                    var name = content.Keys.FindName(property);
                    var value = property.GetValue(instance, null).WrapObject();
                    content.Add(name, value);
                }
            }

            foreach (var method in methods)
            {
                if (!method.IsSpecialName)
                {
                    var name = content.Keys.FindName(method);
                    var value = method.WrapFunction(instance);
                    content.Add(name, value);
                }
            }

            return content;
        }

        private static Plugin ConstructStaticPlugin(Type type)
        {
            var flags = BindingFlags.Public | BindingFlags.Static;
            var properties = type.GetProperties(flags);
            var methods = type.GetMethods(flags);
            var meta = GetMetadata(type);
            var content = GetContent(properties, methods, null);
            return new Plugin(meta, content);
        }

        private static Plugin ConstructInstancePlugin(Object obj)
        {
            var type = obj.GetType();
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var properties = type.GetProperties(flags);
            var methods = type.GetMethods(flags);
            var meta = GetMetadata(type);
            var content = GetContent(properties, methods, obj);
            return new Plugin(meta, content);
        }

        sealed class Placement : IPlacement
        {
            private readonly Engine _engine;
            private readonly String _name;
            private readonly IDictionary<String, Object> _obj;

            public Placement(Engine engine, String name, IDictionary<String, Object> obj)
            {
                _engine = engine;
                _name = name;
                _obj = obj;
            }

            public void WithName(String name)
            {
                if (String.IsNullOrEmpty(name))
                {
                    throw new ArgumentException("The given name has to be non-empty.", "name");
                }

                _engine.Globals[name] = _obj;
            }

            public void WithDefaultName()
            {
                WithName(_name);
            }

            public void Scattered()
            {
                foreach (var item in _obj)
                {
                    _engine.Globals[item.Key] = item.Value;
                }
            }
        }
    }
}
