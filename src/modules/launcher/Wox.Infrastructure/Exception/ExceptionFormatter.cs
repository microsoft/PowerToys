using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.Win32;

namespace Wox.Infrastructure.Exception
{
    public class ExceptionFormatter
    {
        public static string FormatExcpetion(System.Exception exception)
        {
            return CreateExceptionReport(exception);
        }

        //todo log /display line by line 
        private static string CreateExceptionReport(System.Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("## Exception");
            sb.AppendLine();
            sb.AppendLine("```");

            var exlist = new List<StringBuilder>();

            while (ex != null)
            {
                var exsb = new StringBuilder();
                exsb.Append(ex.GetType().FullName);
                exsb.Append(": ");
                exsb.AppendLine(ex.Message);
                if (ex.Source != null)
                {
                    exsb.Append("   Source: ");
                    exsb.AppendLine(ex.Source);
                }
                if (ex.TargetSite != null)
                {
                    exsb.Append("   TargetAssembly: ");
                    exsb.AppendLine(ex.TargetSite.Module.Assembly.ToString());
                    exsb.Append("   TargetModule: ");
                    exsb.AppendLine(ex.TargetSite.Module.ToString());
                    exsb.Append("   TargetSite: ");
                    exsb.AppendLine(ex.TargetSite.ToString());
                }
                exsb.AppendLine(ex.StackTrace);
                exlist.Add(exsb);

                ex = ex.InnerException;
            }

            foreach (var result in exlist.Select(o => o.ToString()).Reverse())
            {
                sb.AppendLine(result);
            }
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("## Environment");
            sb.AppendLine($"* Command Line: {Environment.CommandLine}");
            sb.AppendLine($"* Timestamp: {DateTime.Now.ToString(CultureInfo.InvariantCulture)}");
            sb.AppendLine($"* Wox version: {Constant.Version}");
            sb.AppendLine($"* OS Version: {Environment.OSVersion.VersionString}");
            sb.AppendLine($"* IntPtr Length: {IntPtr.Size}");
            sb.AppendLine($"* x64: {Environment.Is64BitOperatingSystem}");
            sb.AppendLine($"* Python Path: {Constant.PythonPath}");
            sb.AppendLine($"* Everything SDK Path: {Constant.EverythingSDKPath}");
            sb.AppendLine($"* CLR Version: {Environment.Version}");
            sb.AppendLine($"* Installed .NET Framework: ");
            foreach (var result in GetFrameworkVersionFromRegistry())
            {
                sb.Append("   * ");
                sb.AppendLine(result);
            }

            sb.AppendLine();
            sb.AppendLine("## Assemblies - " + AppDomain.CurrentDomain.FriendlyName);
            sb.AppendLine();
            foreach (var ass in AppDomain.CurrentDomain.GetAssemblies().OrderBy(o => o.GlobalAssemblyCache ? 50 : 0))
            {
                sb.Append("* ");
                sb.Append(ass.FullName);
                sb.Append(" (");

                if (ass.IsDynamic)
                {
                    sb.Append("dynamic assembly doesn't has location");
                }
                else if (string.IsNullOrEmpty(ass.Location))
                {
                    sb.Append("location is null or empty");
                    
                }
                else
                {
                sb.Append(ass.Location);
                    
                }
                sb.AppendLine(")");
            }

            return sb.ToString();
        }

        // http://msdn.microsoft.com/en-us/library/hh925568%28v=vs.110%29.aspx
        private static List<string> GetFrameworkVersionFromRegistry()
        {
            try
            {
                var result = new List<string>();
                using (RegistryKey ndpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
                {
                    foreach (string versionKeyName in ndpKey.GetSubKeyNames())
                    {
                        if (versionKeyName.StartsWith("v"))
                        {
                            RegistryKey versionKey = ndpKey.OpenSubKey(versionKeyName);
                            string name = (string)versionKey.GetValue("Version", "");
                            string sp = versionKey.GetValue("SP", "").ToString();
                            string install = versionKey.GetValue("Install", "").ToString();
                            if (install != "")
                                if (sp != "" && install == "1")
                                    result.Add(string.Format("{0} {1} SP{2}", versionKeyName, name, sp));
                                else
                                    result.Add(string.Format("{0} {1}", versionKeyName, name));

                            if (name != "")
                            {
                                continue;
                            }
                            foreach (string subKeyName in versionKey.GetSubKeyNames())
                            {
                                RegistryKey subKey = versionKey.OpenSubKey(subKeyName);
                                name = (string)subKey.GetValue("Version", "");
                                if (name != "")
                                    sp = subKey.GetValue("SP", "").ToString();
                                install = subKey.GetValue("Install", "").ToString();
                                if (install != "")
                                {
                                    if (sp != "" && install == "1")
                                        result.Add(string.Format("{0} {1} {2} SP{3}", versionKeyName, subKeyName, name, sp));
                                    else if (install == "1")
                                        result.Add(string.Format("{0} {1} {2}", versionKeyName, subKeyName, name));
                                }

                            }

                        }
                    }
                }
                using (RegistryKey ndpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
                {
                    int releaseKey = (int)ndpKey.GetValue("Release");
                    {
                        if (releaseKey == 378389)
                            result.Add("v4.5");

                        if (releaseKey == 378675)
                            result.Add("v4.5.1 installed with Windows 8.1");

                        if (releaseKey == 378758)
                            result.Add("4.5.1 installed on Windows 8, Windows 7 SP1, or Windows Vista SP2");
                    }
                }
                return result;
            }
            catch (System.Exception e)
            {
                return new List<string>();
            }

        }
    }
}
