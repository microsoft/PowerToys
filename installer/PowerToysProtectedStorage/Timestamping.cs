// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Threading;
using System.Threading.Tasks;

namespace PowerToys.ProtectedStorage.Build
{
    // This creates/binds RFC3161 evidence, not product trust. The native verifier
    // must authorize the result before it can replace a release signature.
    public static class Timestamping
    {
        public const string TimestampOid = "1.2.840.113549.1.9.16.2.14";
        public const string MicrosoftTimestampOid = "1.3.6.1.4.1.311.3.3.1";
        public const int MaximumResponseSize = 1048576;

        public static SignedCms ReadDetached(byte[] content, byte[] signature)
        {
            if (content.Length == 0 || content.Length > 65536 ||
                signature.Length == 0 || signature.Length > MaximumResponseSize)
                throw new CryptographicException("Detached document/signature exceeds runtime bounds.");
            var cms = new SignedCms(new ContentInfo(content), true);
            cms.Decode(signature);
            if (!cms.Detached || !cms.ContentInfo.Content.AsSpan().SequenceEqual(content) ||
                cms.SignerInfos.Count != 1 ||
                cms.SignerInfos[0].DigestAlgorithm.Value != "2.16.840.1.101.3.4.2.1")
                throw new CryptographicException("Expected exact detached content and one SHA256 signer.");
            cms.CheckSignature(true);
            return cms;
        }

        public static bool HasValidTimestamp(SignerInfo signer)
        {
            CryptographicAttributeObject timestamp = null;
            foreach (CryptographicAttributeObject attribute in signer.UnsignedAttributes)
            {
                if (attribute.Oid.Value == "1.2.840.113549.1.9.6")
                    throw new CryptographicException("Legacy countersignatures are not RFC3161 evidence.");
                if (attribute.Oid.Value != TimestampOid && attribute.Oid.Value != MicrosoftTimestampOid)
                    continue;
                if (timestamp != null || attribute.Values.Count != 1)
                    throw new CryptographicException("Expected exactly one RFC3161 timestamp value.");
                timestamp = attribute;
            }
            if (timestamp == null)
                return false;
            byte[] value = timestamp.Values[0].RawData;
            if (!Rfc3161TimestampToken.TryDecode(value, out var token, out int consumed) ||
                consumed != value.Length || !token.VerifySignatureForSignerInfo(signer, out _))
                throw new CryptographicException("RFC3161 token is not bound to this signer signature.");
            return true;
        }

        public static Rfc3161TimestampRequest CreateRequest(SignerInfo signer)
        {
            if (HasValidTimestamp(signer))
                throw new CryptographicException("The signer already has an RFC3161 timestamp.");
            return Rfc3161TimestampRequest.CreateFromSignerInfo(
                signer, HashAlgorithmName.SHA256, nonce: RandomNumberGenerator.GetBytes(16),
                requestSignerCertificates: true);
        }

        public static byte[] CompleteResponse(SignedCms cms, Rfc3161TimestampRequest request, byte[] response)
        {
            if (cms.SignerInfos.Count != 1 || HasValidTimestamp(cms.SignerInfos[0]))
                throw new CryptographicException("Timestamp addition requires exactly one untimestamped signer.");
            if (response.Length == 0 || response.Length > MaximumResponseSize)
                throw new CryptographicException("RFC3161 response exceeds bounds.");
            var token = request.ProcessResponse(response, out int consumed);
            if (consumed != response.Length || !token.VerifySignatureForSignerInfo(cms.SignerInfos[0], out _))
                throw new CryptographicException("RFC3161 response is not bound to the actual signer signature.");
            cms.SignerInfos[0].AddUnsignedAttribute(new AsnEncodedData(new Oid(TimestampOid), token.AsSignedCms().Encode()));
            if (!HasValidTimestamp(cms.SignerInfos[0]))
                throw new CryptographicException("Missing RFC3161 timestamp after attachment.");
            byte[] result = cms.Encode();
            if (result.Length > MaximumResponseSize)
                throw new CryptographicException("Timestamped signature exceeds runtime bounds.");
            return result;
        }

        public static Uri ValidateServer(string server)
        {
            // No redirects or arbitrary provider scripts/endpoints. Only the
            // documented Microsoft public TSA and the approved corporate TSA.
            if (server != "http://timestamp.acs.microsoft.com" &&
                server != "http://timestamp.acs.microsoft.com/" &&
                server != "http://rfc3161.gtm.corp.microsoft.com/TSS/HttpTspServer")
                throw new ArgumentException("An approved Microsoft RFC3161 timestamp endpoint is required.", nameof(server));
            return new Uri(server, UriKind.Absolute);
        }

        public static byte[] AddTimestamp(byte[] content, byte[] signature, string server)
        {
            Uri endpoint = ValidateServer(server);
            SignedCms cms = ReadDetached(content, signature);
            if (HasValidTimestamp(cms.SignerInfos[0]))
                return signature;
            var request = CreateRequest(cms.SignerInfos[0]);
            byte[] response = SendRequest(endpoint, request.Encode()).GetAwaiter().GetResult();
            return CompleteResponse(cms, request, response);
        }

        private static async Task<byte[]> SendRequest(Uri endpoint, byte[] request)
        {
            using (var handler = new HttpClientHandler { AllowAutoRedirect = false })
            using (var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan })
            {
                for (int attempt = 0; attempt < 3; ++attempt)
                {
                    try
                    {
                        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                        using (var message = new HttpRequestMessage(HttpMethod.Post, endpoint))
                        {
                            message.Content = new ByteArrayContent(request);
                            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/timestamp-query");
                            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/timestamp-reply"));
                            using (var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false))
                            {
                                if (response.StatusCode == HttpStatusCode.RequestTimeout ||
                                    (int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                                    throw new HttpRequestException("Transient RFC3161 HTTP failure: " + (int)response.StatusCode);
                                if (response.StatusCode != HttpStatusCode.OK)
                                    throw new CryptographicException("RFC3161 HTTP failure (no redirect/fallback): " + (int)response.StatusCode);
                                if (response.Content.Headers.ContentType?.MediaType != "application/timestamp-reply" ||
                                    response.Content.Headers.ContentLength > MaximumResponseSize)
                                    throw new CryptographicException("Unexpected RFC3161 content type or response size.");
                                using (var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false))
                                using (var output = new MemoryStream())
                                {
                                    var buffer = new byte[8192];
                                    int count;
                                    while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, deadline.Token).ConfigureAwait(false)) != 0)
                                    {
                                        if (output.Length + count > MaximumResponseSize)
                                            throw new CryptographicException("RFC3161 response exceeds size limit.");
                                        output.Write(buffer, 0, count);
                                    }
                                    return output.ToArray();
                                }
                            }
                        }
                    }
                    catch (Exception error) when (attempt < 2 &&
                        (error is HttpRequestException || error is OperationCanceledException))
                    {
                        Console.Error.WriteLine($"WARNING: RFC3161 timestamp attempt {attempt + 1}/3 failed for {endpoint}: {error.Message}. Retrying in {attempt + 1}s.");
                        await Task.Delay(TimeSpan.FromSeconds(attempt + 1)).ConfigureAwait(false);
                    }
                }
            }
            throw new CryptographicException("RFC3161 request failed; untimestamped signatures are never accepted.");
        }
    }
}
