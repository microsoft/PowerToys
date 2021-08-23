// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces
{
    internal class SystemPath
    {
        private static readonly Regex WindowsPath = new Regex(@"^([a-zA-Z]:)", RegexOptions.Compiled);

        public static string RealPath(string path)
        {
            if (WindowsPath.IsMatch(path))
            {
                string windowsPath = path.Replace("/", "\\");
                return $"{windowsPath[0]}".ToUpper() + windowsPath.Remove(0, 1);
            }
            else
            {
                return path;
            }
        }
    }
}
