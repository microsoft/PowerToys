using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.CommandArgs
{
    public class ToggleCommandArg:ICommandArg
    {
        public string Command
        {
            get { return "toggle"; }
        }

        public void Execute(IList<string> args)
        {
            App.Window.ToggleWox();
        }
    }
}
