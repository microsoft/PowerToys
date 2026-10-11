#include "../../../ProtectedStorage.Common/Common.h"
#include "../../../ProtectedStorage.Common/SignatureTrust.h"
#include <LocalTestSigner.h>
#include <memory>

namespace PowerToys::ProtectedStorage
{
    static_assert(std::wstring_view(App) == L"PowerToysProtectedStorageLocalTest");
    static_assert(std::string_view(ReleaseTrustPolicy) == "powertoys-local-test-v1");

    void VerifyAuthenticodeReleaseSignature(HANDLE, const std::wstring&)
    {
        // This test target cannot authorize production installers or PE signing.
        Fail("Authenticode installation is not supported by the isolated local test host", ERROR_NOT_SUPPORTED);
    }

    void VerifyDetachedReleaseSignature(const std::vector<BYTE>& content, const std::vector<BYTE>& signature)
    {
        Require(!content.empty() && content.size() <= 65536 && !signature.empty() && signature.size() <= 1024 * 1024,
            "local-test CMS bounds");
        Require(CryptGetMessageSignerCount(PKCS_7_ASN_ENCODING, signature.data(), static_cast<DWORD>(signature.size())) == 1,
            "local-test CMS requires one signer");
        CRYPT_VERIFY_MESSAGE_PARA parameters{ sizeof(parameters) };
        parameters.dwMsgAndCertEncodingType = X509_ASN_ENCODING | PKCS_7_ASN_ENCODING;
        const BYTE* data = content.data();
        DWORD size = static_cast<DWORD>(content.size());
        PCCERT_CONTEXT signer = nullptr;
        if (!CryptVerifyDetachedMessageSignature(&parameters, 0, signature.data(), static_cast<DWORD>(signature.size()),
            1, &data, &size, &signer))
            Fail("verify local-test CMS");
        std::unique_ptr<const CERT_CONTEXT, decltype(&CertFreeCertificateContext)> certificate(signer, CertFreeCertificateContext);
        Require(signer && HashBytes(signer->pbCertEncoded, signer->cbCertEncoded) == LocalTestSignerSha256,
            "local-test CMS signer does not match this build");
        Require(CertVerifyTimeValidity(nullptr, signer->pCertInfo) == 0, "expired local-test signing certificate");
    }
}
