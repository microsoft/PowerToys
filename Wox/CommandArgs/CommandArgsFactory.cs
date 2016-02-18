using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Wox.Helper;

namespace Wox.CommandArgs
{
    internal static class CommandArgsFactory
    {
        private static List<ICommandArg> commandArgs;

        static CommandArgsFactory()
        {
            var type = typeof(ICommandArg);
            commandArgs = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(p => type.IsAssignableFrom(p) && !p.IsInterface)
                    .Select(t => Activator.CreateInstance(t) as ICommandArg).ToList();
        }

        public static void Execute(IList<string> args)
        {
            // todo restart command line args?
            if (args.Count > 0 && args[0] != SingleInstance<App>.Restart)
            {
                string command = args[0];
                ICommandArg cmd = commandArgs.FirstOrDefault(o => o.Command.ToLower() == command);
                if (cmd != null)
                {
                    args.RemoveAt(0); //remove command itself
                    cmd.Execute(args);
                }
            }
            else
            {
                App.API.ShowApp();
            }
        }
    }
}
