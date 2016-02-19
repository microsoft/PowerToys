using System.Collections.Generic;

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
            if (App.Window.IsVisible)
            {
                App.API.HideApp();
            }
            else
            {
                App.API.ShowApp();
            }
        }
    }
}
