using System;
using System.IO;
using System.Net;

namespace Wox.Helper
{
    public class DataWebRequestFactory : IWebRequestCreate
    {
        class DataWebRequest : WebRequest
        {
            private readonly Uri m_uri;

            public DataWebRequest(Uri uri)
            {
                m_uri = uri;
            }

            public override WebResponse GetResponse()
            {
                return new DataWebResponse(m_uri);
            }
        }

        class DataWebResponse : WebResponse
        {
            private readonly string m_contentType;
            private readonly byte[] m_data;

            public DataWebResponse(Uri uri)
            {
                string uriString = uri.AbsoluteUri;

                int commaIndex = uriString.IndexOf(',');
                var headers = uriString.Substring(0, commaIndex).Split(';');
                m_contentType = headers[0];
                string dataString = uriString.Substring(commaIndex + 1);
                m_data = Convert.FromBase64String(dataString);
            }

            public override string ContentType
            {
                get { return m_contentType; }
                set
                {
                    throw new NotSupportedException();
                }
            }

            public override long ContentLength
            {
                get { return m_data.Length; }
                set
                {
                    throw new NotSupportedException();
                }
            }

            public override Stream GetResponseStream()
            {
                return new MemoryStream(m_data);
            }
        }

        public WebRequest Create(Uri uri)
        {
            return new DataWebRequest(uri);
        }
    }
}
