#pragma once

#include <Windows.h>
#include <accctrl.h>
#include <aclapi.h>

#include <string>

namespace secure_named_pipe
{
    inline constexpr DWORD OutboundClientAccess = FILE_GENERIC_READ;
    inline constexpr DWORD OutboundPipeMode =
        PIPE_TYPE_MESSAGE |
        PIPE_READMODE_MESSAGE |
        PIPE_WAIT |
        PIPE_REJECT_REMOTE_CLIENTS;

    namespace details
    {
        class PipeSecurityAttributes
        {
        public:
            PipeSecurityAttributes() = default;
            PipeSecurityAttributes(const PipeSecurityAttributes&) = delete;
            PipeSecurityAttributes& operator=(const PipeSecurityAttributes&) = delete;

            ~PipeSecurityAttributes()
            {
                if (m_dacl)
                {
                    LocalFree(m_dacl);
                }

                if (m_logonSid)
                {
                    HeapFree(GetProcessHeap(), 0, m_logonSid);
                }
            }

            bool initialize()
            {
                HANDLE token = nullptr;
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token))
                {
                    return false;
                }

                const auto closeToken = [&]() {
                    CloseHandle(token);
                };

                if (!get_logon_sid(token, &m_logonSid))
                {
                    closeToken();
                    return false;
                }

                DWORD tokenUserSize = 0;
                GetTokenInformation(token, TokenUser, nullptr, 0, &tokenUserSize);
                auto* tokenUser = static_cast<TOKEN_USER*>(HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, tokenUserSize));
                if (!tokenUser ||
                    !GetTokenInformation(token, TokenUser, tokenUser, tokenUserSize, &tokenUserSize))
                {
                    if (tokenUser)
                    {
                        HeapFree(GetProcessHeap(), 0, tokenUser);
                    }
                    closeToken();
                    return false;
                }

                const DWORD userSidSize = GetLengthSid(tokenUser->User.Sid);
                const bool copiedUserSid =
                    userSidSize <= sizeof(m_userSid) &&
                    CopySid(userSidSize, m_userSid, tokenUser->User.Sid) == TRUE;
                HeapFree(GetProcessHeap(), 0, tokenUser);
                if (!copiedUserSid)
                {
                    closeToken();
                    return false;
                }

                DWORD administratorsSidSize = sizeof(m_administratorsSid);
                DWORD localSystemSidSize = sizeof(m_localSystemSid);
                if (!CreateWellKnownSid(WinBuiltinAdministratorsSid, nullptr, m_administratorsSid, &administratorsSidSize) ||
                    !CreateWellKnownSid(WinLocalSystemSid, nullptr, m_localSystemSid, &localSystemSidSize))
                {
                    closeToken();
                    return false;
                }

                TOKEN_ELEVATION elevation{};
                DWORD elevationSize = 0;
                if (!GetTokenInformation(token, TokenElevation, &elevation, sizeof(elevation), &elevationSize))
                {
                    closeToken();
                    return false;
                }

                closeToken();

                PSID ownerSid = elevation.TokenIsElevated ? static_cast<PSID>(m_administratorsSid) : static_cast<PSID>(m_userSid);
                const TRUSTEE_TYPE ownerType = elevation.TokenIsElevated ? TRUSTEE_IS_GROUP : TRUSTEE_IS_USER;

                EXPLICIT_ACCESS entries[3]{};
                set_entry(entries[0], FILE_ALL_ACCESS, ownerSid, ownerType);
                set_entry(entries[1], FILE_ALL_ACCESS, m_localSystemSid, TRUSTEE_IS_USER);
                set_entry(entries[2], OutboundClientAccess, m_logonSid, TRUSTEE_IS_USER);

                if (SetEntriesInAcl(ARRAYSIZE(entries), entries, nullptr, &m_dacl) != ERROR_SUCCESS ||
                    !InitializeSecurityDescriptor(&m_securityDescriptor, SECURITY_DESCRIPTOR_REVISION) ||
                    !SetSecurityDescriptorOwner(&m_securityDescriptor, ownerSid, FALSE) ||
                    !SetSecurityDescriptorGroup(&m_securityDescriptor, ownerSid, FALSE) ||
                    !SetSecurityDescriptorDacl(&m_securityDescriptor, TRUE, m_dacl, FALSE))
                {
                    return false;
                }

                m_attributes.nLength = sizeof(m_attributes);
                m_attributes.lpSecurityDescriptor = &m_securityDescriptor;
                m_attributes.bInheritHandle = FALSE;
                return true;
            }

            SECURITY_ATTRIBUTES* get()
            {
                return &m_attributes;
            }

        private:
            static void set_entry(EXPLICIT_ACCESS& entry, DWORD access, PSID sid, TRUSTEE_TYPE trusteeType)
            {
                entry.grfAccessPermissions = access;
                entry.grfAccessMode = SET_ACCESS;
                entry.grfInheritance = NO_INHERITANCE;
                entry.Trustee.TrusteeForm = TRUSTEE_IS_SID;
                entry.Trustee.TrusteeType = trusteeType;
                entry.Trustee.ptstrName = static_cast<LPTSTR>(sid);
            }

            static bool get_logon_sid(HANDLE token, PSID* logonSid)
            {
                DWORD groupsSize = 0;
                GetTokenInformation(token, TokenGroups, nullptr, 0, &groupsSize);
                auto* groups = static_cast<TOKEN_GROUPS*>(HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, groupsSize));
                if (!groups ||
                    !GetTokenInformation(token, TokenGroups, groups, groupsSize, &groupsSize))
                {
                    if (groups)
                    {
                        HeapFree(GetProcessHeap(), 0, groups);
                    }
                    return false;
                }

                bool found = false;
                for (DWORD index = 0; index < groups->GroupCount; ++index)
                {
                    if ((groups->Groups[index].Attributes & SE_GROUP_LOGON_ID) != SE_GROUP_LOGON_ID)
                    {
                        continue;
                    }

                    const DWORD sidSize = GetLengthSid(groups->Groups[index].Sid);
                    *logonSid = HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sidSize);
                    found = *logonSid && CopySid(sidSize, *logonSid, groups->Groups[index].Sid) == TRUE;
                    if (!found && *logonSid)
                    {
                        HeapFree(GetProcessHeap(), 0, *logonSid);
                        *logonSid = nullptr;
                    }
                    break;
                }

                HeapFree(GetProcessHeap(), 0, groups);
                return found;
            }

            SECURITY_DESCRIPTOR m_securityDescriptor{};
            SECURITY_ATTRIBUTES m_attributes{};
            PACL m_dacl = nullptr;
            PSID m_logonSid = nullptr;
            BYTE m_administratorsSid[SECURITY_MAX_SID_SIZE]{};
            BYTE m_localSystemSid[SECURITY_MAX_SID_SIZE]{};
            BYTE m_userSid[SECURITY_MAX_SID_SIZE]{};
        };
    }

    inline HANDLE create_outbound_server(const std::wstring& pipeName, DWORD bufferSize, bool overlapped)
    {
        details::PipeSecurityAttributes securityAttributes;
        if (!securityAttributes.initialize())
        {
            return INVALID_HANDLE_VALUE;
        }

        DWORD openMode = PIPE_ACCESS_OUTBOUND | FILE_FLAG_FIRST_PIPE_INSTANCE;
        if (overlapped)
        {
            openMode |= FILE_FLAG_OVERLAPPED;
        }

        return CreateNamedPipeW(
            pipeName.c_str(),
            openMode,
            OutboundPipeMode,
            1,
            bufferSize,
            0,
            0,
            securityAttributes.get());
    }
}
