// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.ValueGenerator.GUID
{
    public class GUIDRequest : IComputeRequest
    {
        public byte[] Result { get; set; }

        public bool IsSuccessful { get; set; }

        public string ErrorMessage { get; set; }

        private int Version { get; set; }

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
                else if (!Guid.TryParse(guidNamespace, out guid))
                {
                    throw new ArgumentException($"For GUIDs versions 3 and 5, the first parameter needs to be a valid GUID or one of: {string.Join(", ", GUIDGenerator.PredefinedNamespaces.Keys)}");
                }
                else
                {
                    GuidNamespace = guid;
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
                        throw new InvalidOperationException("Undefined GUID version");
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
            if (ErrorMessage != null)
            {
                return ErrorMessage;
            }

            return GuidResult.ToString();
        }

        public string FormatResult(IFormatProvider provider = null)
        {
            if (ErrorMessage != null)
            {
                return ErrorMessage;
            }

            return $"GUIDv{Version}: {ResultToString()}";
        }
    }
}
