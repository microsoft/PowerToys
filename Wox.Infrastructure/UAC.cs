using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using Size = System.Drawing.Size;

namespace Wox.Infrastructure
{

    public static class UAC
    {
        /// <summary>
        /// Execute methods that require Admin role, which will popup UAC window.
        /// 
        /// Notes:
        ///     1. Invoker method shouldn't have any parameters
        ///     2. Add  attribute [MethodImpl(MethodImplOptions.NoInlining)] to invoker method
        /// 
        /// Example:
        /// [MethodImpl(MethodImplOptions.NoInlining)]
        /// private void OnStartWithWindowUnChecked()
        /// {
        ///    UAC.ExecuteAdminMethod(() => SetStartup(false));
        /// } 
        /// 
        /// </summary>
        /// <param name="method"></param>
        public static void ExecuteAdminMethod(Action method)
        {
            if (method == null) return;
            if (Environment.OSVersion.Version.Major <= 5 || IsAdministrator())
            {
                method();
                return;
            }

            StackTrace stackTrace = new StackTrace();
            // Get calling method name
            MethodBase callingMethod = stackTrace.GetFrame(1).GetMethod();
            string methodName = callingMethod.Name;
            if (callingMethod.ReflectedType == null) return;

            string className = callingMethod.ReflectedType.Name;
            string nameSpace = callingMethod.ReflectedType.Namespace;
            string args = string.Format("UAC {0} {1} {2}", nameSpace,className,methodName);
            Debug.WriteLine(args);
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Wox.UAC.exe"),
                Arguments = args,
                CreateNoWindow = true,
                Verb = "runas"
            };

            try
            {
                var process = new Process();
                process.StartInfo = psi;
                process.Start();
            }
            catch (Exception e)
            {
                MessageBox.Show("Execute failed: " + e);
#if (DEBUG)
                {
                    throw;
                }
#endif

            }

        }

        private static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            if (identity != null)
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            return false;
        }
    }
}