// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography;
using System.Text;
using Community.PowerToys.Run.Plugin.ValueGenerator.Base64;
using Community.PowerToys.Run.Plugin.ValueGenerator.GUID;
using Community.PowerToys.Run.Plugin.ValueGenerator.Hashing;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.ValueGenerator
{
    public class InputParser
    {
        public IComputeRequest ParseInput(Query query)
        {
            IComputeRequest request;

            if (query.Terms.Count == 0)
            {
                throw new FormatException("Empty request");
            }

            string command = query.Terms[0];

            if (command.ToLower(null) == "md5")
            {
                int commandIndex = query.RawUserQuery.IndexOf(command, StringComparison.InvariantCultureIgnoreCase);
                string content = query.RawUserQuery.Substring(commandIndex + command.Length).Trim();

                if (content == string.Empty)
                {
                    throw new FormatException("Empty hash request");
                }

                Log.Debug($"Will calculate MD5 hash for: {content}", GetType());
                request = new HashRequest(HashAlgorithmName.MD5, Encoding.UTF8.GetBytes(content));
            }
            else if (command.StartsWith("sha", StringComparison.InvariantCultureIgnoreCase))
            {
                int commandIndex = query.RawUserQuery.IndexOf(command, StringComparison.InvariantCultureIgnoreCase);
                string content = query.RawUserQuery.Substring(commandIndex + command.Length).Trim();
                HashAlgorithmName algorithmName;

                switch (command.Substring(3))
                {
                    case "1":
                        algorithmName = HashAlgorithmName.SHA1;
                        break;

                    case "256":
                        algorithmName = HashAlgorithmName.SHA256;
                        break;

                    case "384":
                        algorithmName = HashAlgorithmName.SHA384;
                        break;

                    case "512":
                        algorithmName = HashAlgorithmName.SHA512;
                        break;
                    default:
                        throw new ArgumentException("Unknown SHA variant. Supported variants: SHA1, SHA256, SHA384, SHA512");
                }

                if (content == string.Empty)
                {
                    throw new FormatException("Empty hash request");
                }

                Log.Debug($"Will calculate {algorithmName} hash for: {content}", GetType());
                request = new HashRequest(algorithmName, Encoding.UTF8.GetBytes(content));
            }
            else if (command.StartsWith("guid", StringComparison.InvariantCultureIgnoreCase) ||
                     command.StartsWith("uuid", StringComparison.InvariantCultureIgnoreCase))
            {
                string content = query.Search.Substring(command.Length).Trim();

                // Default to version 4
                int version = 4;
                string versionQuery = command.Substring(4);

                if (versionQuery.Length > 0)
                {
                    if (versionQuery.StartsWith("v", StringComparison.InvariantCultureIgnoreCase))
                    {
                        versionQuery = versionQuery.Substring(1);
                    }

                    if (!int.TryParse(versionQuery, null, out version))
                    {
                        throw new ArgumentException("Could not determine requested GUID version");
                    }
                }

                if (version == 3 || version == 5)
                {
                    string[] sParameters = content.Split(" ");

                    if (sParameters.Length != 2)
                    {
                        throw new ArgumentException("GUID versions 3 and 5 require 2 parameters - a namespace GUID and a name");
                    }

                    string namespaceParameter = sParameters[0];
                    string nameParameter = sParameters[1];

                    request = new GUIDRequest(version, namespaceParameter, nameParameter);
                }
                else
                {
                    request = new GUIDRequest(version);
                }
            }
            else if (command.ToLower(null) == "base64")
            {
                int commandIndex = query.RawUserQuery.IndexOf(command, StringComparison.InvariantCultureIgnoreCase);
                string content = query.RawUserQuery.Substring(commandIndex + command.Length).Trim();
                request = new Base64Request(Encoding.UTF8.GetBytes(content));
            }
            else if (command.ToLower(null) == "base64d")
            {
                int commandIndex = query.RawUserQuery.IndexOf(command, StringComparison.InvariantCultureIgnoreCase);
                string content = query.RawUserQuery.Substring(commandIndex + command.Length).Trim();
                request = new Base64DecodeRequest(content);
            }
            else
            {
                throw new FormatException($"Invalid Query: {query.RawUserQuery}");
            }

            return request;
        }
    }
}
