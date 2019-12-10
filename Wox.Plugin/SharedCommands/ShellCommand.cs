using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wox.Plugin.SharedCommands
{
    public static class ShellCommand
    {
        public static ProcessStartInfo SetProcessStartInfo(this string fileName, string workingDirectory="", string arguments = "", string verb = "")
        {
            var info = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                Arguments = arguments,
                Verb = verb
            };

            return info;
        }
    }
}
