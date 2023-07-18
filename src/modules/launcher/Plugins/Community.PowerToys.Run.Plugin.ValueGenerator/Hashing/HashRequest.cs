// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.ValueGenerator.Hashing
{
    public class HashRequest : IComputeRequest
    {
        public byte[] Result { get; set; }

        public bool IsSuccessful { get; set; }

        public string ErrorMessage { get; set; }

        public string Description
        {
            get
            {
                return $"{AlgorithmName}({Encoding.UTF8.GetString(DataToHash)})";
            }
        }

        public HashAlgorithmName AlgorithmName { get; set; }

        private byte[] DataToHash { get; set; }

        private static Dictionary<HashAlgorithmName, HashAlgorithm> _algorithms = new Dictionary<HashAlgorithmName, HashAlgorithm>()
        {
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
            { HashAlgorithmName.MD5, MD5.Create() },
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms

#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
            { HashAlgorithmName.SHA1, SHA1.Create() },
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
            { HashAlgorithmName.SHA256, SHA256.Create() },
            { HashAlgorithmName.SHA384, SHA384.Create() },
            { HashAlgorithmName.SHA512, SHA512.Create() },
        };

        public HashRequest(HashAlgorithmName algorithmName, byte[] dataToHash)
        {
            AlgorithmName = algorithmName;
            DataToHash = dataToHash ?? throw new ArgumentNullException(nameof(dataToHash));
        }

        public bool Compute()
        {
            if (DataToHash == null)
            {
                ErrorMessage = "Null data passed to hash request";
                Log.Exception(ErrorMessage, new InvalidOperationException(ErrorMessage), GetType());
                IsSuccessful = false;
            }
            else
            {
                Result = _algorithms[AlgorithmName].ComputeHash(DataToHash);
                IsSuccessful = true;
            }

            return IsSuccessful;
        }

        public string ResultToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var b in Result)
            {
                sb.Append(b.ToString("X2", null));
            }

            return sb.ToString();
        }
    }
}
