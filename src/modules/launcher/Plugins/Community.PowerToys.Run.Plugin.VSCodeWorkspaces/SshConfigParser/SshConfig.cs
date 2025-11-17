// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.SshConfigParser
{
    public class SshConfig
    {
        private static readonly Regex _sshConfig = new Regex(@"^(\w[\s\S]*?\w)$(?=(?:\s+^\w|\z))", RegexOptions.Multiline);
        private static readonly Regex _keyValue = new Regex(@"(\w+\s\S+)", RegexOptions.Multiline);

        public static IEnumerable<SshHost> ParseFile(string path)
        {
            return Parse(File.ReadAllText(path));
        }

        public static IEnumerable<SshHost> Parse(string str)
        {
            str = str.Replace("\r", string.Empty);
            var list = new List<SshHost>();
            foreach (Match match in _sshConfig.Matches(str))
            {
                var sshHost = new SshHost();
                string content = match.Groups.Values.ToList()[0].Value;
                foreach (Match match1 in _keyValue.Matches(content))
                {
                    var split = match1.Value.Split(" ");
                    var key = split[0];
                    var value = split[1];
                    sshHost.Properties[key] = value;
                }

                list.Add(sshHost);
            }

            return list;
        }
    }
}
