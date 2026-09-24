// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
namespace PowerToys.ProtectedStorage.Build.Tests
{
    using System;
    using System.Formats.Asn1;
    using System.Security.Cryptography;
    using System.Security.Cryptography.Pkcs;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;

    public static class TimestampTests
    {
        private static void Reject(Action action, string name)
        {
            try { action(); }
            catch (Exception error) when (error is CryptographicException || error is ArgumentException)
            {
                return;
            }
            throw new Exception("Accepted invalid timestamp input: " + name);
        }

        private static X509Certificate2 Certificate(RSA key, bool timestamp)
        {
            var request = new CertificateRequest(timestamp ? "CN=InMemory TSA Test" : "CN=InMemory Release Test",
                key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var purposes = new OidCollection { new Oid(timestamp ? "1.3.6.1.5.5.7.3.8" : "1.3.6.1.5.5.7.3.3") };
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(purposes, true));
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(15));
        }

        private static SignedCms Sign(byte[] content, X509Certificate2 certificate, string digest = "2.16.840.1.101.3.4.2.1")
        {
            var cms = new SignedCms(new ContentInfo(content), true);
            var signer = new CmsSigner(certificate) { DigestAlgorithm = new Oid(digest), IncludeOption = X509IncludeOption.EndCertOnly };
            cms.ComputeSignature(signer);
            return cms;
        }

        private static byte[] Response(Rfc3161TimestampRequest request, X509Certificate2 tsa, bool wrongNonce = false)
        {
            var info = new Rfc3161TimestampTokenInfo(new Oid("1.3.6.1.4.1.311.99.1"), request.HashAlgorithmId,
                request.GetMessageHash(), new byte[] { 1 }, DateTimeOffset.UtcNow, null, false,
                wrongNonce ? new ReadOnlyMemory<byte>(new byte[] { 1 }) : request.GetNonce(), null, null);
            var token = new SignedCms(new ContentInfo(new Oid("1.2.840.113549.1.9.16.1.4"), info.Encode()));
            var signer = new CmsSigner(tsa) { DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1"), IncludeOption = X509IncludeOption.EndCertOnly };
            // RFC5035 SigningCertificateV2: default SHA256 + exact TSA cert hash.
            var ess = new AsnWriter(AsnEncodingRules.DER);
            ess.PushSequence();
            ess.PushSequence();
            ess.PushSequence();
            ess.WriteOctetString(SHA256.HashData(tsa.RawData));
            ess.PopSequence();
            ess.PopSequence();
            ess.PopSequence();
            signer.SignedAttributes.Add(new AsnEncodedData(new Oid("1.2.840.113549.1.9.16.2.47"), ess.Encode()));
            token.ComputeSignature(signer);
            var response = new AsnWriter(AsnEncodingRules.DER);
            response.PushSequence();
            response.PushSequence();
            response.WriteInteger(0);
            response.PopSequence();
            response.WriteEncodedValue(token.Encode());
            response.PopSequence();
            return response.Encode();
        }

        private static byte[] NoSigners()
        {
            var writer = new AsnWriter(AsnEncodingRules.DER);
            var explicitContent = new Asn1Tag(TagClass.ContextSpecific, 0, true);
            writer.PushSequence();
            writer.WriteObjectIdentifier("1.2.840.113549.1.7.2");
            writer.PushSequence(explicitContent);
            writer.PushSequence();
            writer.WriteInteger(1);
            writer.PushSetOf();
            writer.PopSetOf();
            writer.PushSequence();
            writer.WriteObjectIdentifier("1.2.840.113549.1.7.1");
            writer.PopSequence();
            writer.PushSetOf();
            writer.PopSetOf();
            writer.PopSequence();
            writer.PopSequence(explicitContent);
            writer.PopSequence();
            return writer.Encode();
        }

        public static string Run()
        {
            using (var key = RSA.Create(2048))
            using (var tsaKey = RSA.Create(2048))
            using (var certificate = Certificate(key, false))
            using (var tsa = Certificate(tsaKey, true))
            {
                byte[] content = Encoding.UTF8.GetBytes("{\"format\":1,\"test\":\"in-memory only\"}");
                var cms = Sign(content, certificate);
                byte[] unsignedTimestamp = cms.Encode();
                Timestamping.ReadDetached(content, unsignedTimestamp);
                if (Timestamping.HasValidTimestamp(cms.SignerInfos[0]))
                    throw new Exception("Missing timestamp was treated as evidence.");
                Reject(() => Timestamping.ReadDetached(Encoding.UTF8.GetBytes("tamper"), unsignedTimestamp), "tampered content");
                Reject(() => Timestamping.ReadDetached(content, new byte[] { 1, 2, 3 }), "malformed CMS");
                Reject(() => Timestamping.ReadDetached(content, NoSigners()), "zero signers");
                var multi = Sign(content, certificate);
                multi.ComputeSignature(new CmsSigner(certificate));
                Reject(() => Timestamping.ReadDetached(content, multi.Encode()), "multiple signers");
                Reject(() => Timestamping.ReadDetached(content, Sign(content, certificate, "1.3.14.3.2.26").Encode()), "SHA1");
                Reject(() => Timestamping.ValidateServer("http://localhost/tsa"), "arbitrary timestamp provider");
                Reject(() => Timestamping.ValidateServer("http://timestamp.acs.microsoft.com.evil.invalid"), "lookalike timestamp host");

                var request = Timestamping.CreateRequest(cms.SignerInfos[0]);
                byte[] response = Response(request, tsa);
                Reject(() => Timestamping.CompleteResponse(cms, request, Response(request, tsa, true)), "nonce mismatch");
                Reject(() => Timestamping.CompleteResponse(cms, request, new byte[] { 1 }), "malformed response");
                Reject(() => Timestamping.CompleteResponse(cms, request, new byte[Timestamping.MaximumResponseSize + 1]), "oversize response");
                byte[] trailing = new byte[response.Length + 1];
                response.CopyTo(trailing, 0);
                Reject(() => Timestamping.CompleteResponse(cms, request, trailing), "trailing response bytes");
                var other = Sign(Encoding.UTF8.GetBytes("different actual signature"), certificate);
                Reject(() => Timestamping.CompleteResponse(other, request, response), "timestamp on a different signer signature");
                Reject(() => Timestamping.CompleteResponse(cms, request, Response(request, certificate)), "TSA without timestamp EKU");

                byte[] timestamped = Timestamping.CompleteResponse(cms, request, response);
                var decoded = Timestamping.ReadDetached(content, timestamped);
                if (!Timestamping.HasValidTimestamp(decoded.SignerInfos[0]))
                    throw new Exception("Valid bound timestamp rejected.");
                if (!decoded.SignerInfos[0].GetSignature().AsSpan().SequenceEqual(
                    Timestamping.ReadDetached(content, unsignedTimestamp).SignerInfos[0].GetSignature()))
                    throw new Exception("Timestamp operation changed the actual signer signature.");
                Reject(() => Timestamping.CompleteResponse(decoded, request, response), "second timestamp");
                byte[] token = decoded.SignerInfos[0].UnsignedAttributes[0].Values[0].RawData;
                var microsoft = Timestamping.ReadDetached(content, unsignedTimestamp);
                microsoft.SignerInfos[0].AddUnsignedAttribute(new AsnEncodedData(new Oid(Timestamping.MicrosoftTimestampOid), token));
                if (!Timestamping.HasValidTimestamp(microsoft.SignerInfos[0]))
                    throw new Exception("Valid Microsoft RFC3161 timestamp rejected.");
                byte[] microsoftEncoded = microsoft.Encode();
                if (!Timestamping.AddTimestamp(content, microsoftEncoded, "http://timestamp.acs.microsoft.com")
                    .AsSpan().SequenceEqual(microsoftEncoded))
                    throw new Exception("Existing Microsoft RFC3161 timestamp was changed or supplemented.");
                Reject(() => Timestamping.CreateRequest(microsoft.SignerInfos[0]), "request over existing Microsoft timestamp");
                Reject(() => Timestamping.CompleteResponse(microsoft, request, response), "second timestamp over Microsoft timestamp");
                microsoft.SignerInfos[0].AddUnsignedAttribute(new AsnEncodedData(new Oid(Timestamping.TimestampOid), token));
                Reject(() => Timestamping.HasValidTimestamp(microsoft.SignerInfos[0]), "mixed standard and Microsoft timestamps");
                var duplicateMicrosoft = Timestamping.ReadDetached(content, microsoftEncoded);
                duplicateMicrosoft.SignerInfos[0].AddUnsignedAttribute(new AsnEncodedData(new Oid(Timestamping.MicrosoftTimestampOid), token));
                Reject(() => Timestamping.HasValidTimestamp(duplicateMicrosoft.SignerInfos[0]), "duplicate Microsoft timestamps");
                var malformedMicrosoft = Timestamping.ReadDetached(content, unsignedTimestamp);
                malformedMicrosoft.SignerInfos[0].AddUnsignedAttribute(new AsnEncodedData(new Oid(Timestamping.MicrosoftTimestampOid), new byte[] { 5, 0 }));
                Reject(() => Timestamping.HasValidTimestamp(malformedMicrosoft.SignerInfos[0]), "malformed Microsoft timestamp");
                var legacy = Timestamping.ReadDetached(content, unsignedTimestamp);
                legacy.SignerInfos[0].ComputeCounterSignature(new CmsSigner(certificate));
                Reject(() => Timestamping.HasValidTimestamp(legacy.SignerInfos[0]), "legacy countersignature");
                var stolen = Sign(Encoding.UTF8.GetBytes("a different document"), certificate);
                stolen.SignerInfos[0].AddUnsignedAttribute(new AsnEncodedData(new Oid(Timestamping.MicrosoftTimestampOid), token));
                Reject(() => Timestamping.HasValidTimestamp(stolen.SignerInfos[0]), "copied Microsoft timestamp");
                stolen = Sign(Encoding.UTF8.GetBytes("another different document"), certificate);
                foreach (CryptographicAttributeObject attribute in decoded.SignerInfos[0].UnsignedAttributes)
                    stolen.SignerInfos[0].AddUnsignedAttribute(attribute.Values[0]);
                Reject(() => Timestamping.HasValidTimestamp(stolen.SignerInfos[0]), "copied timestamp");
                foreach (CryptographicAttributeObject attribute in decoded.SignerInfos[0].UnsignedAttributes)
                    decoded.SignerInfos[0].AddUnsignedAttribute(attribute.Values[0]);
                Reject(() => Timestamping.HasValidTimestamp(decoded.SignerInfos[0]), "duplicate timestamp values");
                return "PASS in-memory CMS/RFC3161 mechanics: exact content, zero/multiple signers, SHA1, nonce, bounds, timestamp EKU, signature binding, standard/Microsoft timestamp preservation, missing/copied/mixed/duplicate/malformed timestamps, and legacy countersignature rejection. No production trust was granted.";
            }
        }
    }
}
