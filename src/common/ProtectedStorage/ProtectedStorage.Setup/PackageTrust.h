// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
#pragma once
#include "Product.h"
#include "..\ProtectedStorage.Common\Authentication.h"
#include <wincrypt.h>
#include <wintrust.h>
#include <softpub.h>
#include <bcrypt.h>
#include <iomanip>

namespace PowerToysProtectedStorage::Maintenance
{
    inline std::string Hash(std::vector<BYTE> bytes)
    {
        BYTE digest[32]{};
        if (bytes.size() > MAXDWORD ||
            BCryptHash(BCRYPT_SHA256_ALG_HANDLE, nullptr, 0, bytes.data(),
                       static_cast<ULONG>(bytes.size()), digest, sizeof(digest)) < 0)
            throw failure("SHA256", ERROR_INVALID_DATA);
        std::ostringstream text;
        for (const auto value : digest)
            text << std::hex << std::setfill('0') << std::setw(2) << static_cast<unsigned>(value);
        return text.str();
    }

    inline std::vector<BYTE> ReadLocked(HANDLE file, size_t maximum = 256ull * 1024 * 1024)
    {
        BY_HANDLE_FILE_INFORMATION information{};
        check(GetFileInformationByHandle(file, &information) != FALSE, "package file identity");
        if (information.dwFileAttributes & (FILE_ATTRIBUTE_REPARSE_POINT | FILE_ATTRIBUTE_DIRECTORY) ||
            information.nNumberOfLinks != 1 || information.nFileSizeHigh || information.nFileSizeLow > maximum)
            throw failure("unsafe package file", ERROR_ACCESS_DENIED);
        LARGE_INTEGER begin{};
        check(SetFilePointerEx(file, begin, nullptr, FILE_BEGIN) != FALSE, "package read offset");
        std::vector<BYTE> bytes(information.nFileSizeLow);
        DWORD read = 0;
        check(ReadFile(file, bytes.data(), static_cast<DWORD>(bytes.size()), &read, nullptr) != FALSE &&
              read == bytes.size(), "read exact package");
        return bytes;
    }

    inline void VerifySignedFile(const std::filesystem::path& path)
    {
        // There is deliberately no local/developer trust fallback.
        if (!product::has_resource(110))
            throw failure("release signing policy is not embedded", ERROR_TRUST_FAILURE);
        const auto pinBytes = product::resource(110);
        const std::string pin(pinBytes.begin(), pinBytes.end());
        if (pin.size() != 64 || pin.find_first_not_of("0123456789abcdef") != std::string::npos)
            throw failure("invalid release certificate pin", ERROR_TRUST_FAILURE);
        handle locked(CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING,
                                  FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        check(locked.value != INVALID_HANDLE_VALUE, "hold signed package");
        (void)ReadLocked(locked.value);
        WINTRUST_FILE_INFO file{ sizeof(file) };
        file.pcwszFilePath = path.c_str();
        file.hFile = locked.value;
        WINTRUST_DATA data{ sizeof(data) };
        data.dwUIChoice = WTD_UI_NONE;
        data.fdwRevocationChecks = WTD_REVOKE_WHOLECHAIN;
        data.dwUnionChoice = WTD_CHOICE_FILE;
        data.pFile = &file;
        data.dwStateAction = WTD_STATEACTION_VERIFY;
        data.dwProvFlags = WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT;
        GUID policy = WINTRUST_ACTION_GENERIC_VERIFY_V2;
        const auto status = WinVerifyTrust(nullptr, &policy, &data);
        const auto provider = WTHelperProvDataFromStateData(data.hWVTStateData);
        const auto signer = provider ? WTHelperGetProvSignerFromChain(provider, 0, FALSE, 0) : nullptr;
        const auto certificate = signer ? WTHelperGetProvCertFromChain(signer, 0) : nullptr;
        bool pinned = false;
        if (status == ERROR_SUCCESS && certificate && certificate->pCert)
        {
            const auto context = certificate->pCert;
            pinned = Hash({ context->pbCertEncoded, context->pbCertEncoded + context->cbCertEncoded }) == pin;
        }
        data.dwStateAction = WTD_STATEACTION_CLOSE;
        WinVerifyTrust(nullptr, &policy, &data);
        if (status != ERROR_SUCCESS || !pinned)
            throw failure("release Authenticode/pinned signer rejected", status ? static_cast<DWORD>(status) : ERROR_TRUST_FAILURE);
    }

    inline void VerifyMappedImage(HANDLE process, HANDLE executable)
    {
        try
        {
            PowerToys::ProtectedStorage::VerifyProcessImageFile(process, executable);
        }
        catch (const PowerToys::ProtectedStorage::Error&)
        {
            throw failure("requester executable mapping does not match the pinned image", ERROR_ACCESS_DENIED);
        }
    }

    inline void VerifySameImage(HANDLE requesterProcess, const std::filesystem::path& requester)
    {
        const std::filesystem::path current(product::module_path());
        handle original(CreateFileW(requester.c_str(), GENERIC_READ | GENERIC_EXECUTE, FILE_SHARE_READ, nullptr, OPEN_EXISTING,
                                    FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        handle elevated(CreateFileW(current.c_str(), GENERIC_READ | GENERIC_EXECUTE, FILE_SHARE_READ, nullptr, OPEN_EXISTING,
                                    FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        check(original.value != INVALID_HANDLE_VALUE && elevated.value != INVALID_HANDLE_VALUE, "lock authorization images");
        VerifyMappedImage(requesterProcess, original.value);
        VerifyMappedImage(GetCurrentProcess(), elevated.value);
        BY_HANDLE_FILE_INFORMATION first{}, second{};
        check(GetFileInformationByHandle(original.value, &first) && GetFileInformationByHandle(elevated.value, &second),
              "authorization image identity");
        if (first.dwVolumeSerialNumber != second.dwVolumeSerialNumber || first.nFileIndexHigh != second.nFileIndexHigh ||
            first.nFileIndexLow != second.nFileIndexLow || Hash(ReadLocked(original.value)) != Hash(ReadLocked(elevated.value)))
            throw failure("authorization image differs from original requester", ERROR_ACCESS_DENIED);
        VerifySignedFile(current);
    }
}
