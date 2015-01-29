using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.CommandArgs
{
    public class QueryCommandArg : ICommandArg
    {
        public string Command
        {
            get { return "query"; }
        }

        public void Execute(IList<string> args)
        {
            Console.WriteLine("test");
            if (args.Count > 0)
            {
                string query = args[0];
                App.Window.ChangeQuery(query);
            }
            App.Window.ShowApp();
        }
    }
}
