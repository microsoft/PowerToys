using System.Collections.Generic;

namespace Wox.CommandArgs
{
    interface ICommandArg
    {
        string Command { get; }
        void Execute(IList<string> args);
    }
}
