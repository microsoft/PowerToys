using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.CommandArgs
{
    interface ICommandArg
    {
        string Command { get; }
        void Execute(IList<string> args);
    }
}
