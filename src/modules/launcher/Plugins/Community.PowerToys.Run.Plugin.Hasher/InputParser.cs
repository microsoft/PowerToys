// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Community.PowerToys.Run.Plugin.Hasher
{
    public static class InputParser
    {
        private static readonly Regex RegValidAlgorithms = new Regex(
            @"^(MD|md)5(\s.+|\(.*\))$|" +
            @"^((SHA|sha)(1|256|384|512))(\s.+|\(.*\))$",
            RegexOptions.Compiled);

        public static HashRequest RequestedHash(string input)
        {
            if (!RegValidAlgorithms.IsMatch(input))
            {
                return null;
            }

            HashRequest hashRequest = new HashRequest();
            string content;

            Regex md5Regex = new Regex(@"^(MD|md)5(\s.*|\(.*\))$", RegexOptions.Compiled);
            Regex shaRegex = new Regex(@"^((SHA|sha)(1|256|384|512))(\s.+|\(.*\))$", RegexOptions.Compiled);

            if (md5Regex.IsMatch(input))
            {
                content = md5Regex.Match(input).Groups[1].Value.Trim();

                hashRequest.AlgorithmName = HashAlgorithmName.MD5;
            }
            else if (shaRegex.IsMatch(input))
            {
                var match = shaRegex.Match(input);
                content = match.Groups[4].Value.Trim();

                switch (match.Groups[3].Value)
                {
                    case "1":
                        hashRequest.AlgorithmName = HashAlgorithmName.SHA1;
                        break;

                    case "256":
                        hashRequest.AlgorithmName = HashAlgorithmName.SHA256;
                        break;

                    case "384":
                        hashRequest.AlgorithmName = HashAlgorithmName.SHA384;
                        break;

                    case "512":
                        hashRequest.AlgorithmName = HashAlgorithmName.SHA512;
                        break;
                    default:
                        return null;
                }
            }
            else
            {
                return null;
            }

            if (content.StartsWith('(') && content.EndsWith(')'))
            {
                content = content.Substring(1, content.Length - 2);
            }

            hashRequest.DataToHash = Encoding.UTF8.GetBytes(content);

            return hashRequest;
        }
    }
}
