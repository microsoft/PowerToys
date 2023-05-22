// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography;
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
                    default:
                        return string.Empty;
                }
            }
        }

        private Guid? GuidNamespace { get; set; }

        private string GuidName { get; set; }

        private Guid GuidResult { get; set; }

        public GUIDRequest(int version, string guidNamespace = null, string name = null)
        {
            Version = version;

            if (guidNamespace != null)
            {
                Guid guid;
                if (GUIDGenerator.PredefinedNamespaces.TryGetValue(guidNamespace, out guid))
                {
                    GuidNamespace = guid;
                }
                else if (Guid.TryParse(guidNamespace, out guid))
                {
                    GuidNamespace = guid;
                }
                else
                {
                    throw new ArgumentException($"The first parameter needs to be a valid GUID or one of: {string.Join(", ", GUIDGenerator.PredefinedNamespaces.Keys)}");
                }
            }
            else
            {
                GuidNamespace = null;
            }

            GuidName = name;
            ErrorMessage = null;
        }

        public bool Compute()
        {
            IsSuccessful = true;
            try
            {
                switch (Version)
                {
                    case 1:
                        GuidResult = GUIDGenerator.V1();
                        break;
                    case 3:
                        if (GuidNamespace == null)
                        {
                            throw new InvalidOperationException("Null GUID namespace for version 3");
                        }

                        if (GuidName == null)
                        {
                            throw new InvalidOperationException("Null GUID name for version 3");
                        }

                        GuidResult = GUIDGenerator.V3(GuidNamespace.Value, GuidName);
                        break;
                    case 4:
                        GuidResult = GUIDGenerator.V4();
                        break;
                    case 5:
                        if (GuidNamespace == null)
                        {
                            throw new InvalidOperationException("Null GUID namespace for version 3");
                        }

                        if (GuidName == null)
                        {
                            throw new InvalidOperationException("Null GUID name for version 3");
                        }

                        GuidResult = GUIDGenerator.V5(GuidNamespace.Value, GuidName);
                        break;
                    default:
                        throw new ArgumentException("Undefined GUID version");
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

        public string FormatResult(IFormatProvider provider = null)
        {
            if (!IsSuccessful)
            {
                return ErrorMessage;
            }

            return $"GUIDv{Version}: {ResultToString()}";
        }
    }
}
