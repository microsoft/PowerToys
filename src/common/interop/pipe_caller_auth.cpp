#include "pipe_caller_auth.h"

#include <wincrypt.h>
#include <wintrust.h>
#include <softpub.h>

#include <cwctype>
#include <cstring>
#include <map>
#include <mutex>

#pragma comment(lib, "wintrust.lib")
#pragma comment(lib, "crypt32.lib")
// Note: the file version is read via the PE resource (kernel32 only), NOT the version.dll APIs, to
// avoid a link-name collision with PowerToys' own static "Version.lib" project that the interop DLL
// references.

namespace interop_auth
{
    namespace
    {
        std::wstring ToLower(std::wstring s)
        {
            for (auto& c : s)
            {
                c = static_cast<wchar_t>(towlower(c));
            }
            return s;
        }

        std::wstring CanonicalizePath(const std::wstring& path)
        {
            // Backup semantics so this works for both files and directories.
            HANDLE h = CreateFileW(path.c_str(),
                                   0,
                                   FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                                   nullptr,
                                   OPEN_EXISTING,
                                   FILE_FLAG_BACKUP_SEMANTICS,
                                   nullptr);
            if (h == INVALID_HANDLE_VALUE)
            {
                // Fail closed: a path we cannot open/canonicalize must not slip past the
                // directory-prefix check as a non-canonical raw path.
                return {};
            }
            wchar_t buf[1024] = {};
            DWORD len = GetFinalPathNameByHandleW(h, buf, ARRAYSIZE(buf), FILE_NAME_NORMALIZED);
            CloseHandle(h);
            if (len == 0 || len >= ARRAYSIZE(buf))
            {
                return {};
            }
            std::wstring result(buf, len);
            if (result.rfind(L"\\\\?\\", 0) == 0)
            {
                result.erase(0, 4);
            }
            return result;
        }

        std::wstring BaseName(const std::wstring& path)
        {
            const auto pos = path.find_last_of(L"\\/");
            return pos == std::wstring::npos ? path : path.substr(pos + 1);
        }

        bool PathIsUnderDirectory(const std::wstring& canonicalFile, const std::wstring& directory)
        {
            if (directory.empty() || canonicalFile.empty())
            {
                return false;
            }
            std::wstring dir = ToLower(CanonicalizePath(directory));
            if (dir.empty())
            {
                // CanonicalizePath failed closed; an empty prefix must not match every path.
                return false;
            }
            if (!dir.empty() && dir.back() != L'\\')
            {
                dir.push_back(L'\\');
            }
            const std::wstring file = ToLower(canonicalFile);
            return file.size() > dir.size() && file.compare(0, dir.size(), dir) == 0;
        }

        bool BasenameAllowed(const std::wstring& canonicalFile, const std::vector<std::wstring>& allowed)
        {
            const std::wstring base = ToLower(BaseName(canonicalFile));
            for (const auto& a : allowed)
            {
                if (ToLower(a) == base)
                {
                    return true;
                }
            }
            return false;
        }

        bool LeafIsMicrosoft(PCCERT_CONTEXT cert)
        {
            if (!cert)
            {
                return false;
            }
            wchar_t name[256] = {};
            const DWORD n = CertGetNameStringW(cert, CERT_NAME_SIMPLE_DISPLAY_TYPE, 0, nullptr, name, ARRAYSIZE(name));
            // Case-insensitive: cert display-name casing can vary and this is a secondary check on top
            // of the machine-root Authenticode chain in ChainsToMachineRoot.
            return n > 1 && wcsstr(ToLower(name).c_str(), L"microsoft corporation") != nullptr;
        }

        // Anchor the signer's chain in the LOCAL MACHINE root store only (HCCE_LOCAL_MACHINE) rather than
        // WinVerifyTrust's default user+machine union. The Runner runs as the same user as a potential
        // attacker, so a user-writable CurrentUser\Root could otherwise forge a "Microsoft" signer; a
        // non-admin cannot plant a machine root, so this defeats the forge.
        bool ChainsToMachineRoot(PCCERT_CONTEXT leaf, HCERTSTORE additionalStore)
        {
            if (!leaf)
            {
                return false;
            }

            char codeSigningOid[] = szOID_PKIX_KP_CODE_SIGNING;
            LPSTR oids[] = { codeSigningOid };
            CERT_CHAIN_PARA para = {};
            para.cbSize = sizeof(para);
            para.RequestedUsage.dwType = USAGE_MATCH_TYPE_AND;
            para.RequestedUsage.Usage.cUsageIdentifier = 1;
            para.RequestedUsage.Usage.rgpszUsageIdentifier = oids;

            // Cached-only revocation: never hit the network; treat "unknown/offline" as not-revoked.
            const DWORD flags = CERT_CHAIN_CACHE_ONLY_URL_RETRIEVAL |
                                CERT_CHAIN_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT;

            PCCERT_CHAIN_CONTEXT chain = nullptr;
            if (!CertGetCertificateChain(HCCE_LOCAL_MACHINE, leaf, nullptr, additionalStore, &para, flags, nullptr, &chain))
            {
                return false;
            }

            bool ok = false;
            const DWORD ignore = CERT_TRUST_REVOCATION_STATUS_UNKNOWN | CERT_TRUST_IS_OFFLINE_REVOCATION;
            if ((chain->TrustStatus.dwErrorStatus & ~ignore) == 0)
            {
                CERT_CHAIN_POLICY_PARA policyPara = {};
                policyPara.cbSize = sizeof(policyPara);
                CERT_CHAIN_POLICY_STATUS policyStatus = {};
                policyStatus.cbSize = sizeof(policyStatus);
                if (CertVerifyCertificateChainPolicy(CERT_CHAIN_POLICY_AUTHENTICODE, chain, &policyPara, &policyStatus))
                {
                    ok = (policyStatus.dwError == 0);
                }
            }

            CertFreeCertificateChain(chain);
            return ok;
        }

        // Establishes that the file has a valid, untampered Authenticode signature (blocks unsigned,
        // tampered, and signature-stapled binaries). The *trust anchor* decision is intentionally NOT
        // taken from here (it consults the default user+machine store) — ChainsToMachineRoot re-anchors
        // it against the machine store.
        bool HasIntactAuthenticodeSignature(const std::wstring& path)
        {
            WINTRUST_FILE_INFO fileInfo = {};
            fileInfo.cbStruct = sizeof(fileInfo);
            fileInfo.pcwszFilePath = path.c_str();

            WINTRUST_DATA wd = {};
            wd.cbStruct = sizeof(wd);
            wd.dwUIChoice = WTD_UI_NONE;
            wd.fdwRevocationChecks = WTD_REVOKE_NONE;
            wd.dwUnionChoice = WTD_CHOICE_FILE;
            wd.pFile = &fileInfo;
            wd.dwStateAction = WTD_STATEACTION_VERIFY;
            wd.dwProvFlags = WTD_SAFER_FLAG | WTD_CACHE_ONLY_URL_RETRIEVAL;

            GUID action = WINTRUST_ACTION_GENERIC_VERIFY_V2;
            HWND noWindow = static_cast<HWND>(INVALID_HANDLE_VALUE);
            const LONG status = WinVerifyTrust(noWindow, &action, &wd);

            wd.dwStateAction = WTD_STATEACTION_CLOSE;
            WinVerifyTrust(noWindow, &action, &wd);

            return status == ERROR_SUCCESS;
        }

        bool VerifyMicrosoftSignedMachineRoot(const std::wstring& path)
        {
            // 1) Integrity + valid signature (default store). Rejects unsigned / tampered / stapled.
            if (!HasIntactAuthenticodeSignature(path))
            {
                return false;
            }

            // 2) Re-anchor trust in the machine root store and confirm the signer leaf is Microsoft.
            HCERTSTORE hStore = nullptr;
            HCRYPTMSG hMsg = nullptr;
            if (!CryptQueryObject(CERT_QUERY_OBJECT_FILE,
                                  path.c_str(),
                                  CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED_EMBED,
                                  CERT_QUERY_FORMAT_FLAG_BINARY,
                                  0,
                                  nullptr,
                                  nullptr,
                                  nullptr,
                                  &hStore,
                                  &hMsg,
                                  nullptr))
            {
                return false;
            }

            bool result = false;
            DWORD signerSize = 0;
            if (CryptMsgGetParam(hMsg, CMSG_SIGNER_INFO_PARAM, 0, nullptr, &signerSize) && signerSize > 0)
            {
                std::vector<BYTE> signerBuf(signerSize);
                if (CryptMsgGetParam(hMsg, CMSG_SIGNER_INFO_PARAM, 0, signerBuf.data(), &signerSize))
                {
                    auto* signer = reinterpret_cast<CMSG_SIGNER_INFO*>(signerBuf.data());
                    CERT_INFO certInfo = {};
                    certInfo.Issuer = signer->Issuer;
                    certInfo.SerialNumber = signer->SerialNumber;
                    PCCERT_CONTEXT leaf = CertGetSubjectCertificateFromStore(
                        hStore, X509_ASN_ENCODING | PKCS_7_ASN_ENCODING, &certInfo);
                    if (leaf)
                    {
                        result = LeafIsMicrosoft(leaf) && ChainsToMachineRoot(leaf, hStore);
                        CertFreeCertificateContext(leaf);
                    }
                }
            }

            if (hMsg)
            {
                CryptMsgClose(hMsg);
            }
            if (hStore)
            {
                CertCloseStore(hStore, 0);
            }
            return result;
        }

        // --- Per-process verification cache -------------------------------------------------------
        // The cache itself is the header-only interop_auth::VerificationCache, owned per pipe server so
        // verdicts are physically partitioned by policy (key = pid + creation-time). Only the helper to
        // read a process's unforgeable creation-time key lives here.
        unsigned long long ProcessCreationKey(HANDLE process)
        {
            FILETIME create = {}, exit = {}, kernel = {}, user = {};
            if (!GetProcessTimes(process, &create, &exit, &kernel, &user))
            {
                return 0;
            }
            return (static_cast<unsigned long long>(create.dwHighDateTime) << 32) | create.dwLowDateTime;
        }
    }

    unsigned long long GetModuleVersion(const std::wstring& path)
    {
        if (path.empty())
        {
            return 0;
        }
        // Read the fixed file-version from the PE's RT_VERSION resource using kernel32-only APIs
        // (LoadLibraryEx as a data/resource image), avoiding the version.dll import lib.
        HMODULE mod = LoadLibraryExW(path.c_str(), nullptr, LOAD_LIBRARY_AS_IMAGE_RESOURCE | LOAD_LIBRARY_AS_DATAFILE);
        if (!mod)
        {
            return 0;
        }
        unsigned long long result = 0;
        if (HRSRC res = FindResourceW(mod, MAKEINTRESOURCEW(1 /* VS_VERSION_INFO */), RT_VERSION))
        {
            if (HGLOBAL glob = LoadResource(mod, res))
            {
                const void* locked = LockResource(glob);
                const DWORD size = SizeofResource(mod, res);
                if (locked != nullptr && size >= sizeof(VS_FIXEDFILEINFO))
                {
                    std::vector<BYTE> bytes(size);
                    memcpy(bytes.data(), locked, size);
                    for (size_t i = 0; i + sizeof(VS_FIXEDFILEINFO) <= bytes.size(); i += sizeof(DWORD))
                    {
                        VS_FIXEDFILEINFO ffi{};
                        memcpy(&ffi, &bytes[i], sizeof(ffi));
                        if (ffi.dwSignature == 0xFEEF04BD)
                        {
                            result = (static_cast<unsigned long long>(ffi.dwFileVersionMS) << 32) | ffi.dwFileVersionLS;
                            break;
                        }
                    }
                }
            }
        }
        FreeLibrary(mod);
        return result;
    }

    unsigned long long GetOwnModuleVersion()
    {
        wchar_t self[MAX_PATH * 2] = {};
        const DWORD n = GetModuleFileNameW(nullptr, self, ARRAYSIZE(self));
        if (n == 0 || n >= ARRAYSIZE(self))
        {
            return 0;
        }
        return GetModuleVersion(self);
    }

    AuthResult AuthenticateClient(HANDLE pipe, const CallerPolicy& policy, VerificationCache& cache)
    {
        AuthResult res;
        if (!policy.enabled)
        {
            res.accepted = true;
            return res;
        }

        ULONG pid = 0;
        if (!GetNamedPipeClientProcessId(pipe, &pid))
        {
            res.reasonCode = L"no-client-pid";
            return res;
        }
        res.pid = pid;

        if (policy.expectedClientPid.has_value() && policy.expectedClientPid.value() != pid)
        {
            res.reasonCode = L"pid-mismatch";
            if (policy.logReject)
            {
                policy.logReject(res);
            }
            return res;
        }

        // Hold the process handle for the whole check so the PID cannot be recycled under us.
        HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
        if (!process)
        {
            res.reasonCode = L"open-process-failed";
            if (policy.logReject)
            {
                policy.logReject(res);
            }
            return res;
        }

        const unsigned long long createTime = ProcessCreationKey(process);

        {
            AuthResult cached;
            if (cache.TryGet(pid, createTime, cached))
            {
                cached.pid = pid;
                CloseHandle(process);
                // Do not re-log on a cache hit (dedup across the per-message connections).
                return cached;
            }
        }

        wchar_t imageBuf[MAX_PATH * 2] = {};
        DWORD cch = ARRAYSIZE(imageBuf);
        std::wstring canonical;
        if (QueryFullProcessImageNameW(process, 0, imageBuf, &cch))
        {
            canonical = CanonicalizePath(imageBuf);
        }
        res.imagePath = canonical;

        const wchar_t* reason = L"";
        bool accepted = false;

        if (canonical.empty())
        {
            reason = L"image-path-failed";
        }
        else if (!PathIsUnderDirectory(canonical, policy.expectedDirectory))
        {
            reason = L"bad-directory";
        }
        else if (!BasenameAllowed(canonical, policy.allowedBasenames))
        {
            reason = L"bad-basename";
        }
        else if (policy.expectedVersion != 0 && GetModuleVersion(canonical) != policy.expectedVersion)
        {
            reason = L"version-mismatch";
        }
        else
        {
            bool signatureOk = true;
            if (policy.requireMicrosoftSignature)
            {
#ifdef _DEBUG
                // DEV-ONLY: local builds are not Microsoft-signed. Directory, basename and version are
                // still enforced above. This relaxation is physically compiled out of Release.
                signatureOk = true;
#else
                signatureOk = VerifyMicrosoftSignedMachineRoot(canonical);
#endif
            }
            if (!signatureOk)
            {
                reason = L"not-microsoft-signed";
            }
            else
            {
                accepted = true;
            }
        }

        CloseHandle(process);

        res.accepted = accepted;
        res.reasonCode = reason;

        cache.Put(pid, createTime, res);

        // Required rejection logging (once per process instance, deduped by the cache above).
        if (!accepted && policy.logReject)
        {
            policy.logReject(res);
        }

        return res;
    }
}
