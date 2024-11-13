// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography;
using WinRT;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.ValueGenerator.GUID
{
    public class GUIDRequest : IComputeRequest
    {
        public byte[] Result { get; set; }

        public bool IsSuccessful { get; set; }

        public string ErrorMessage { get; set; }

        private int Version { get; set; }

        public string Description
        {
            get
            {
                switch (Version)
                {
                    case 1:
                        return "Version 1: Time base GUID";
                    case 3:
                    case 5:
                        string hashAlgorithm;
                        if (Version == 3)
                        {
                            hashAlgorithm = HashAlgorithmName.MD5.ToString();
                        }
                        else
                        {
                            hashAlgorithm = HashAlgorithmName.SHA1.ToString();
                        }

                        return $"Version {Version} ({hashAlgorithm}): Namespace and name based GUID.";
                    case 4:
                        return "Version 4: Randomly generated GUID";
                    case 7:
                        return "Version 7: Time-ordered randomly generated GUID";
                    default:
                        return string.Empty;
                }
            }
        }

        private Guid? GuidNamespace { get; set; }

        private string GuidName { get; set; }

        private Guid GuidResult { get; set; }

        private static readonly string NullNamespaceError = $"The first parameter needs to be a valid GUID or one of: {string.Join(", ", GUIDGenerator.PredefinedNamespaces.Keys)}";

        public GUIDRequest(int version, string guidNamespace = null, string name = null)
        {
            Version = version;

            if (Version is < 1 or > 7 or 2 or 6)
            {
                throw new ArgumentException("Unsupported GUID version. Supported versions are 1, 3, 4, 5, and 7");
            }

            if (version is 3 or 5)
            {
                if (guidNamespace == null)
                {
                    throw new ArgumentNullException(null, NullNamespaceError);
                }

                if (GUIDGenerator.PredefinedNamespaces.TryGetValue(guidNamespace.ToLowerInvariant(), out Guid guid))
                {
                    GuidNamespace = guid;
                }
                else if (Guid.TryParse(guidNamespace, out guid))
                {
                    GuidNamespace = guid;
                }
                else
                {
                    throw new ArgumentNullException(null, NullNamespaceError);
                }

                if (name == null)
                {
                    throw new ArgumentNullException(nameof(name));
                }
                else
                {
                    GuidName = name;
                }
            }
            else
            {
                GuidNamespace = null;
            }

            ErrorMessage = null;
        }

        public bool Compute()
        {
            IsSuccessful = true;
            try
            {
                Guid guid = Version switch
                {
                    1 => GUIDGenerator.V1(),
                    3 => GUIDGenerator.V3(GuidNamespace.Value, GuidName),
                    4 => GUIDGenerator.V4(),
                    5 => GUIDGenerator.V5(GuidNamespace.Value, GuidName),
                    7 => GUIDGenerator.V7(),
                    _ => default,
                };
                if (guid != default)
                {
                    GuidResult = guid;
                }

                Result = GuidResult.ToByteArray();
            }
            catch (InvalidOperationException e)
            {
                Log.Exception(e.Message, e, GetType());
                ErrorMessage = e.Message;
                IsSuccessful = false;
            }

            return IsSuccessful;
        }

        public string ResultToString()
        {
            if (!IsSuccessful)
            {
                return ErrorMessage;
            }

            return GuidResult.ToString();
        }
    }
}
