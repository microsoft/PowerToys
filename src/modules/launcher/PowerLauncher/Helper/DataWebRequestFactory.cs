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

#pragma warning disable SYSLIB0014 // Type or member is obsolete

            // TODO: Verify if it's dead code or replace with HttpClient
            public DataWebRequest(Uri uri)
            {
                _uri = uri;
            }
#pragma warning restore SYSLIB0014 // Type or member is obsolete

            public override WebResponse GetResponse()
            {
                return new DataWebResponse(_uri);
            }
        }

        private class DataWebResponse : WebResponse
        {
            private readonly string _contentType;
            private readonly byte[] _data;

            public DataWebResponse(Uri uri)
            {
                string uriString = uri.AbsoluteUri;

                // Using Ordinal since this is internal and used with a symbol
                int commaIndex = uriString.IndexOf(',', StringComparison.Ordinal);
                var headers = uriString.Substring(0, commaIndex).Split(';');
                _contentType = headers[0];
                string dataString = uriString.Substring(commaIndex + 1);
                _data = Convert.FromBase64String(dataString);
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
                    return _data.Length;
                }

                set
                {
                    throw new NotSupportedException();
                }
            }

            public override Stream GetResponseStream()
            {
                return new MemoryStream(_data);
            }
        }

        public WebRequest Create(Uri uri)
        {
            return new DataWebRequest(uri);
        }
    }
}
