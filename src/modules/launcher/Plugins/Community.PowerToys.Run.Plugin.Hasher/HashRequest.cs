// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;

namespace Community.PowerToys.Run.Plugin.Hasher
{
    public class HashRequest
    {
        public HashAlgorithmName AlgorithmName { get; set; }

        public byte[] DataToHash { get; set; }
    }
}
