using System;
using System.Text.RegularExpressions;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces
{
    class SystemPath
    {
        private static readonly Regex WindowsPath = new Regex(@"^([a-zA-Z]:)", RegexOptions.Compiled);

        public static string RealPath(string path)
        {
            if (WindowsPath.IsMatch(path))
            {
                String windowsPath = path.Replace("/", "\\");
                return $"{windowsPath[0]}".ToUpper() + windowsPath.Remove(0,1);
            }
            else
            {
                return path;
            }
        }
    }
}
