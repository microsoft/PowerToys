// code block is from 
// unblocking https://github.com/Squirrel/Squirrel.Windows/blob/master/src/Squirrel/UpdateManager.cs
// https://github.com/Squirrel/Squirrel.Windows/blob/develop/COPYING
// license is MIT

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Squirrel
{
    public sealed partial class UpdateManager
    {
        public static void RestartApp(string exeToStart = null, string arguments = null)
        {
            // NB: Here's how this method works:
            //
            // 1. We're going to pass the *name* of our EXE and the params to 
            //    Update.exe
            // 2. Update.exe is going to grab our PID (via getting its parent), 
            //    then wait for us to exit.
            // 3. We exit cleanly, dropping any single-instance mutexes or 
            //    whatever.
            // 4. Update.exe unblocks, then we launch the app again, possibly 
            //    launching a different version than we started with (this is why
            //    we take the app's *name* rather than a full path)

            exeToStart = exeToStart ?? Path.GetFileName(Assembly.GetEntryAssembly().Location);
            var argsArg = arguments != null ?
                string.Format("-a \"{0}\"", arguments) : "";

            Process.Start(getUpdateExe(), string.Format("--processStartAndWait {0} {1}", exeToStart, argsArg));

            // NB: We have to give update.exe some time to grab our PID, but
            // we can't use WaitForInputIdle because we probably don't have
            // whatever WaitForInputIdle considers a message loop.
            Thread.Sleep(500);
            Environment.Exit(0);
        }

        static string getUpdateExe()
        {
            var assembly = Assembly.GetEntryAssembly();

            // Are we update.exe?
            if (assembly != null &&
                Path.GetFileName(assembly.Location).Equals("update.exe", StringComparison.OrdinalIgnoreCase) &&
                assembly.Location.IndexOf("app-", StringComparison.OrdinalIgnoreCase) == -1 &&
                assembly.Location.IndexOf("SquirrelTemp", StringComparison.OrdinalIgnoreCase) == -1)
            {
                return Path.GetFullPath(assembly.Location);
            }

            assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

            var updateDotExe = Path.Combine(Path.GetDirectoryName(assembly.Location), "..\\Update.exe");
            var target = new FileInfo(updateDotExe);

            if (!target.Exists) throw new Exception("Update.exe not found, not a Squirrel-installed app?");
            return target.FullName;
        }
    }
}
