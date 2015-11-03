using System.Collections.Generic;

namespace Wox.CommandArgs
{
    public class HideStartCommandArg : ICommandArg
    {
        public string Command
        {
            get { return "hidestart"; }
        }

        public void Execute(IList<string> args)
        {
            //App.Window.ShowApp();
            //App.Window.HideApp();
        }
    }
}
