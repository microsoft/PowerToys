#pragma once
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <string>
#include <vector>

namespace PowerToys::ProtectedStorage
{
    inline constexpr char ReleaseTrustPolicy[] = "microsoft-production-v1";

    // The caller must retain a read handle that excludes writes/replacement throughout
    // verification and subsequent consumption. Requires a validated timestamp;
    // the path is not independently reopened.
    void VerifyAuthenticodeReleaseSignature(HANDLE heldFile, const std::wstring& path);

    // A DER-encoded, detached CMS signature over exactly content. Requires one SHA-256
    // Microsoft production signer and one signature-bound RFC 3161 timestamp.
    // Trust/revocation use machine stores and cached URLs only. Unavailable revocation
    // is reported to OutputDebugString; known revocation and all other errors fail.
    void VerifyDetachedReleaseSignature(const std::vector<BYTE>& content, const std::vector<BYTE>& signature);
}
