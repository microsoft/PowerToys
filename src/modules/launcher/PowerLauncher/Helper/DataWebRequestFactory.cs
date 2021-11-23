// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net;

namespace PowerLauncher.Helper
{
    public class DataWebRequestFactory : IWebRequestCreate
    {
        private class DataWebRequest : WebRequest
        {
            private readonly Uri _uri;

            public DataWebRequest(Uri uri)
            {
                _uri = uri;
            }

            public override WebResponse GetResponse()
            {
                return new DataWebResponse(_uri);
            }
        }

        private class DataWebResponse : WebResponse
        {
            private readonly string _contentType;
            private readonly byte[] _data;
            private readonly int _contentLength;

            public DataWebResponse(Uri uri)
            {
                string uriString = uri.AbsoluteUri;

                // Using Ordinal since this is internal and used with a symbol
                int commaIndex = uriString.IndexOf(',', StringComparison.Ordinal);
                int semicolonIndex = uriString.IndexOf(';', 0, commaIndex);
                _contentType = uriString.Substring(0, semicolonIndex);
                ReadOnlySpan<char> dataSpan = uriString.AsSpan(commaIndex + 1);
                _data = new byte[(dataSpan.Length / 4 * 3) + 2];
                if (!Convert.TryFromBase64Chars(dataSpan, _data, out _contentLength))
                {
                    throw new FormatException();
                }
            }

            public override string ContentType
            {
                get
                {
                    return _contentType;
                }

                set
                {
                    throw new NotSupportedException();
                }
            }

            public override long ContentLength
            {
                get
                {
                    return _contentLength;
                }

                set
                {
                    throw new NotSupportedException();
                }
            }

            public override Stream GetResponseStream()
            {
                return new MemoryStream(_data, 0, _contentLength);
            }
        }

        public WebRequest Create(Uri uri)
        {
            return new DataWebRequest(uri);
        }
    }
}
