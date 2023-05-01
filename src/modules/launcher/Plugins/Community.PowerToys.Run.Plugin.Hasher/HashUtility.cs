// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Community.PowerToys.Run.Plugin.Hasher
{
    public static class HashUtility
    {
        private static Dictionary<HashAlgorithmName, HashAlgorithm> _algorithms = new Dictionary<HashAlgorithmName, HashAlgorithm>()
        {
#pragma warning disable CA5350
#pragma warning disable CA5351
            { HashAlgorithmName.MD5, MD5.Create() },
            { HashAlgorithmName.SHA1, SHA1.Create() },
#pragma warning restore CA5350
#pragma warning restore CA5351
            { HashAlgorithmName.SHA256, SHA256.Create() },
            { HashAlgorithmName.SHA384, SHA384.Create() },
            { HashAlgorithmName.SHA512, SHA512.Create() },
        };

        public static List<HashResult> HashData(byte[] data)
        {
            var results = new List<HashResult>();

            foreach (var algorithm in _algorithms)
            {
                results.Add(new HashResult
                {
                    Algorithm = algorithm.Key.ToString(),
                    Content = data,
                    Hash = algorithm.Value.ComputeHash(data),
                });
            }

            return results;
        }

        public static HashResult ComputeHashRequest(HashRequest request)
        {
            return new HashResult
            {
                Algorithm = request.AlgorithmName.ToString(),
                Content = request.DataToHash,
                Hash = _algorithms[request.AlgorithmName].ComputeHash(request.DataToHash),
            };
        }
    }
}
