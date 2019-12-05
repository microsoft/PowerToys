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
        public static ProcessStartInfo SetCMDRunAsAdministrator(this string fullPath, string parentDirectory)
        {
            var info = new ProcessStartInfo
            {
                FileName = fullPath,
                WorkingDirectory = parentDirectory,
                Verb = "runas"
            };

            return info;
        }
    }
}
