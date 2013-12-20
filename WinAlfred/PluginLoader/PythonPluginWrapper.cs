using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using WinAlfred.Plugin;

namespace WinAlfred.PluginLoader
{
    public class PythonPluginWrapper : IPlugin
    {
        private static ScriptEngine engine;
        private static ScriptScope scope;
        private object pythonInstance;

        static PythonPluginWrapper()
        {
            //creating engine and stuff
            engine = Python.CreateEngine();
            scope = engine.CreateScope();

            var paths = engine.GetSearchPaths();
            paths.Add(AppDomain.CurrentDomain.BaseDirectory + @"PythonEnv\2.7\Lib\");
            engine.SetSearchPaths(paths);
        }

        public PythonPluginWrapper(string file)
        {
            pythonInstance = GetPythonClassInstance(file, "winAlfred");
        }

        private object GetPythonClassInstance(string file, string className)
        {
            ScriptSource source = engine.CreateScriptSourceFromFile(file);
            CompiledCode compiled = source.Compile();

            //now executing this code (the code should contain a class)
            compiled.Execute(scope);

            //now creating an object that could be used to access the stuff inside a python script
            return engine.Operations.Invoke(scope.GetVariable(className));
        }

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            object invokeMember = engine.Operations.InvokeMember(pythonInstance, "query", query.RawQuery);
            results.Add(new Result()
            {
                Title = invokeMember.ToString()
            });
            return results;
        }

        public void Init()
        {

        }
    }
}
