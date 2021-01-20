namespace Mages.Core
{
    using Mages.Core.Ast;
    using Mages.Core.Runtime;
    using Mages.Core.Vm;
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Represents the central engine for any kind of evaluation.
    /// </summary>
    public class Engine
    {
        #region Fields

        private readonly IParser _parser;
        private readonly GlobalScope _scope;
        private readonly List<Plugin> _plugins;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new engine with the specified configuration. Otherwise a
        /// default configuration is used.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        public Engine(Configuration configuration = null)
        {
            var cfg = configuration ?? Configuration.Default;
            _parser = cfg.Parser ?? Configuration.Default.Parser;
            _scope = new GlobalScope(cfg.Scope);
            _plugins = new List<Plugin>();
            this.Apply(cfg);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the used parser instance.
        /// </summary>
        public IParser Parser
        {
            get { return _parser; }
        }

        /// <summary>
        /// Gets the used global scope.
        /// </summary>
        public IDictionary<String, Object> Scope
        {
            get { return _scope; }
        }

        /// <summary>
        /// Gets the used global function layer.
        /// </summary>
        public IDictionary<String, Object> Globals
        {
            get { return _scope.Parent; }
        }

        /// <summary>
        /// Gets the version of the engine.
        /// </summary>
        public String Version
        {
            get
            {
                var lib = Assembly.GetExecutingAssembly();
                return lib.GetName().Version.ToString(3);
            }
        }

        /// <summary>
        /// Gets the currently loaded plugins.
        /// </summary>
        public IEnumerable<Plugin> Plugins
        {
            get { return _plugins; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds the given plugin to the list of plugins.
        /// </summary>
        /// <param name="plugin">The plugin to add.</param>
        public void AddPlugin(Plugin plugin)
        {
            if (!_plugins.Contains(plugin))
            {
                _plugins.Add(plugin);
                
                foreach (var item in plugin.Content)
                {
                    if (!Globals.ContainsKey(item.Key))
                    {
                        Globals[item.Key] = item.Value;
                    }
                }
            }
        }

        /// <summary>
        /// Removes the plugin from the list of plugins.
        /// </summary>
        /// <param name="plugin">The plugin to remove.</param>
        public void RemovePlugin(Plugin plugin)
        {
            if (_plugins.Contains(plugin))
            {
                _plugins.Remove(plugin);

                foreach (var item in plugin.Content)
                {
                    var value = default(Object);

                    if (Globals.TryGetValue(item.Key, out value) && value == item.Value)
                    {
                        Globals.Remove(item.Key);
                    }
                }
            }
        }

        /// <summary>
        /// Compiles the given source and returns a function to execute later.
        /// </summary>
        /// <param name="source">The source to compile.</param>
        /// <returns>The function to invoke later.</returns>
        public Func<Object> Compile(String source)
        {
            var statements = _parser.ParseStatements(source);
            var operations = statements.MakeRunnable();
            return () =>
            {
                var context = new ExecutionContext(operations, _scope);
                context.Execute();
                return context.Pop();
            };
        }

        /// <summary>
        /// Interprets the given source and returns the result, if any.
        /// </summary>
        /// <param name="source">The source to interpret.</param>
        /// <returns>The result if available, otherwise null.</returns>
        public Object Interpret(String source)
        {
            var statements = _parser.ParseStatements(source);
            var operations = statements.MakeRunnable();
            var context = new ExecutionContext(operations, _scope);
            context.Execute();
            return context.Pop();
        }

        #endregion
    }
}
