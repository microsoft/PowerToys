// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Win32;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Wox.Infrastructure.Exception
{
    public static class ExceptionFormatter
    {
        public static string FormatException(System.Exception exception)
        {
            return CreateExceptionReport(exception);
        }

        // todo log /display line by line
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
            sb.AppendLine(CultureInfo.InvariantCulture, $"* Command Line: {Environment.CommandLine}");

            // Using InvariantCulture since this is internal
            sb.AppendLine(CultureInfo.InvariantCulture, $"* Timestamp: {DateTime.Now.ToString(CultureInfo.InvariantCulture)}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"* Wox version: {Constant.Version}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"* OS Version: {Environment.OSVersion.VersionString}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"* IntPtr Length: {IntPtr.Size}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"* x64: {Environment.Is64BitOperatingSystem}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"* CLR Version: {Environment.Version}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"* Installed .NET Framework: ");
            foreach (var result in GetFrameworkVersionFromRegistry())
            {
                sb.Append("   * ");
                sb.AppendLine(result);
            }

            sb.AppendLine();
            sb.AppendLine("## Assemblies - " + AppDomain.CurrentDomain.FriendlyName);
            sb.AppendLine();

            // GlobalAssemblyCache - .NET Core and .NET 5 and later: false in all cases.
            // Source https://learn.microsoft.com/dotnet/api/system.reflection.assembly.globalassemblycache?view=net-6.0
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                sb.Append("* ");
                sb.Append(assembly.FullName);
                sb.Append(" (");

                if (assembly.IsDynamic)
                {
                    sb.Append("dynamic assembly doesn't has location");
                }
                else if (string.IsNullOrEmpty(assembly.Location))
                {
                    sb.Append("location is null or empty");
                }
                else
                {
                    sb.Append(assembly.Location);
                }

                sb.AppendLine(")");
            }

            return sb.ToString();
        }

        // http://msdn.microsoft.com/library/hh925568%28v=vs.110%29.aspx
        private static List<string> GetFrameworkVersionFromRegistry()
        {
            try
            {
                var result = new List<string>();
                using (RegistryKey ndpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
                {
                    foreach (string versionKeyName in ndpKey.GetSubKeyNames())
                    {
                        // Using InvariantCulture since this is internal and involves version key
                        if (versionKeyName.StartsWith('v'))
                        {
                            RegistryKey versionKey = ndpKey.OpenSubKey(versionKeyName);
                            string name = (string)versionKey.GetValue("Version", string.Empty);
                            string sp = versionKey.GetValue("SP", string.Empty).ToString();
                            string install = versionKey.GetValue("Install", string.Empty).ToString();
                            if (!string.IsNullOrEmpty(install))
                            {
                                if (!string.IsNullOrEmpty(sp) && install == "1")
                                {
                                    // Using InvariantCulture since this is internal
                                    result.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1} SP{2}", versionKeyName, name, sp));
                                }
                                else
                                {
                                    // Using InvariantCulture since this is internal
                                    result.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1}", versionKeyName, name));
                                }
                            }

                            if (!string.IsNullOrEmpty(name))
                            {
                                continue;
                            }

                            foreach (string subKeyName in versionKey.GetSubKeyNames())
                            {
                                RegistryKey subKey = versionKey.OpenSubKey(subKeyName);
                                name = (string)subKey.GetValue("Version", string.Empty);
                                if (!string.IsNullOrEmpty(name))
                                {
                                    sp = subKey.GetValue("SP", string.Empty).ToString();
                                }

                                install = subKey.GetValue("Install", string.Empty).ToString();
                                if (!string.IsNullOrEmpty(install))
                                {
                                    if (!string.IsNullOrEmpty(sp) && install == "1")
                                    {
                                        // Using InvariantCulture since this is internal
                                        result.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} SP{3}", versionKeyName, subKeyName, name, sp));
                                    }
                                    else if (install == "1")
                                    {
                                        // Using InvariantCulture since this is internal
                                        result.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", versionKeyName, subKeyName, name));
                                    }
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
                        {
                            result.Add("v4.5");
                        }

                        if (releaseKey == 378675)
                        {
                            result.Add("v4.5.1 installed with Windows 8.1");
                        }

                        if (releaseKey == 378758)
                        {
                            result.Add("4.5.1 installed on Windows 8, Windows 7 SP1, or Windows Vista SP2");
                        }
                    }
                }

                return result;
            }
            catch (System.Exception e)
            {
                Log.Exception("Could not get framework version from registry", e, MethodBase.GetCurrentMethod().DeclaringType);
                return new List<string>();
            }
        }
    }
}
