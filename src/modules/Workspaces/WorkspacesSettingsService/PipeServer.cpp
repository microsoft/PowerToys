// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "PipeServer.h"
#include "Bindings.h"
#include "CallerAuth.h"
#include "FileGuard.h"
#include "Paths.h"
#include "protocol/Protocol.h"

#include <windows.h>
#include <sddl.h>
#include <aclapi.h>
#include <vector>
#include <string>
#include <cstring>

#pragma comment(lib, "Advapi32.lib")

namespace PTSettingsSvc
{
    namespace
    {
        // Pipe SD: Authenticated Users may connect; SYSTEM and BUILTIN\Administrators
        // get full control for diagnostics; everyone else is implicitly denied
        // because the DACL doesn't grant them anything.  The protocol layer
        // does the real access control (caller image + allow-list).
        constexpr const wchar_t* kPipeSddl =
            L"D:"
            L"(A;;GRGW;;;AU)"   // Authenticated Users : connect/read/write
            L"(A;;GA;;;SY)"     // SYSTEM : full
            L"(A;;GA;;;BA)";    // BUILTIN\Administrators : full

        bool ReadExact(HANDLE pipe, void* buf, DWORD len)
        {
            BYTE* p = static_cast<BYTE*>(buf);
            DWORD remaining = len;
            while (remaining > 0)
            {
                DWORD got = 0;
                if (!ReadFile(pipe, p, remaining, &got, nullptr) || got == 0)
                {
                    return false;
                }
                p += got;
                remaining -= got;
            }
            return true;
        }

        bool WriteExact(HANDLE pipe, const void* buf, DWORD len)
        {
            const BYTE* p = static_cast<const BYTE*>(buf);
            DWORD remaining = len;
            while (remaining > 0)
            {
                DWORD wrote = 0;
                if (!WriteFile(pipe, p, remaining, &wrote, nullptr) || wrote == 0)
                {
                    return false;
                }
                p += wrote;
                remaining -= wrote;
            }
            return true;
        }

        void SendResponse(HANDLE pipe, Status status,
                          const std::vector<BYTE>& payload = {})
        {
            uint8_t st = static_cast<uint8_t>(status);
            uint32_t len = static_cast<uint32_t>(payload.size());
            WriteExact(pipe, &st, sizeof(st));
            WriteExact(pipe, &len, sizeof(len));
            if (len > 0)
            {
                WriteExact(pipe, payload.data(), len);
            }
        }

        void SendStatus(HANDLE pipe, Status status)
        {
            SendResponse(pipe, status);
        }

        void HandleGetBlob(HANDLE pipe, const CallerIdentity& id)
        {
            std::wstring target = GetUserBlobPath(id.binding->namespaceId,
                                                  id.userSidString);
            std::vector<BYTE> bytes;
            HRESULT hr = ReadFileFully(target, kMaxPayloadBytes, bytes);
            if (hr == HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND) ||
                hr == HRESULT_FROM_WIN32(ERROR_PATH_NOT_FOUND))
            {
                // Brand new user / namespace — explicit NotFound so the
                // caller can distinguish "blob is empty" from "blob doesn't
                // exist yet" (matters for migration).
                SendStatus(pipe, Status::NotFound);
                return;
            }
            if (FAILED(hr))
            {
                SendStatus(pipe, hr == HRESULT_FROM_WIN32(ERROR_FILE_TOO_LARGE)
                                     ? Status::PayloadTooLarge
                                     : Status::IoError);
                return;
            }
            SendResponse(pipe, Status::Ok, bytes);
        }

        void HandlePutBlob(HANDLE pipe, const CallerIdentity& id,
                           const std::vector<BYTE>& payload)
        {
            // No structural / schema check on the payload.  The service is
            // payload-agnostic; the caller is responsible for whatever
            // shape it wants on disk.  See Design-v6-Final.md §4.

            // Ensure intermediate <namespace>\ folder exists.  Inherits the
            // root's PROTECTED DACL (svc:F, admin:F, AuthUsers:RX); no
            // tightening needed at this level.
            std::wstring nsFolder = GetNamespaceFolder(id.binding->namespaceId);
            if (!CreateDirectoryW(nsFolder.c_str(), nullptr))
            {
                DWORD err = GetLastError();
                if (err != ERROR_ALREADY_EXISTS)
                {
                    SendStatus(pipe, Status::IoError);
                    return;
                }
            }

            // Ensure <namespace>\<sid>\ exists and tighten its DACL so user
            // A can't read user B's blob (replace blanket AuthUsers:RX with
            // specific user-SID:RX).  See Design-v6-Final.md §9.
            HRESULT hr = EnsureUserFolder(
                GetUserNamespaceFolder(id.binding->namespaceId, id.userSidString),
                id.userSidString);
            if (FAILED(hr))
            {
                SendStatus(pipe, Status::IoError);
                return;
            }

            hr = WriteFileAtomically(
                GetUserBlobPath(id.binding->namespaceId, id.userSidString),
                payload);
            SendStatus(pipe, FAILED(hr) ? Status::IoError : Status::Ok);
        }

        void HandleConnection(HANDLE pipe)
        {
            CallerIdentity id;
            HRESULT hr = AuthenticateCaller(pipe, id);
            if (FAILED(hr))
            {
                Status s = (hr == E_ACCESSDENIED)
                               ? Status::AuthFailCaller
                               : (hr == HRESULT_FROM_WIN32(ERROR_NOT_FOUND))
                                     ? Status::NamespaceUnknown
                                     : Status::AuthFailToken;
                SendStatus(pipe, s);
                return;
            }

            // ── Read request frame ─────────────────────────────────
            uint8_t op = 0;
            uint32_t plen = 0;
            if (!ReadExact(pipe, &op, sizeof(op)) ||
                !ReadExact(pipe, &plen, sizeof(plen)))
            {
                SendStatus(pipe, Status::BadRequest);
                return;
            }
            if (plen > kMaxPayloadBytes)
            {
                SendStatus(pipe, Status::PayloadTooLarge);
                return;
            }

            std::vector<BYTE> payload(plen);
            if (plen > 0 && !ReadExact(pipe, payload.data(), plen))
            {
                SendStatus(pipe, Status::BadRequest);
                return;
            }

            // ── Dispatch ───────────────────────────────────────────
            switch (static_cast<Opcode>(op))
            {
            case Opcode::Ping:
                SendStatus(pipe, Status::Ok);
                break;

            case Opcode::GetBlob:
                HandleGetBlob(pipe, id);
                break;

            case Opcode::PutBlob:
                HandlePutBlob(pipe, id, payload);
                break;

            default:
                SendStatus(pipe, Status::UnknownOpcode);
                break;
            }
        }

        HANDLE CreateProtectedPipe()
        {
            PSECURITY_DESCRIPTOR sd = nullptr;
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
                    kPipeSddl, SDDL_REVISION_1, &sd, nullptr))
            {
                return INVALID_HANDLE_VALUE;
            }

            SECURITY_ATTRIBUTES sa{};
            sa.nLength = sizeof(sa);
            sa.lpSecurityDescriptor = sd;
            sa.bInheritHandle = FALSE;

            HANDLE pipe = CreateNamedPipeW(
                kPipeName,
                PIPE_ACCESS_DUPLEX | FILE_FLAG_FIRST_PIPE_INSTANCE,
                PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT |
                    PIPE_REJECT_REMOTE_CLIENTS,
                /*nMaxInstances*/ PIPE_UNLIMITED_INSTANCES,
                /*nOutBufferSize*/ 64 * 1024,
                /*nInBufferSize*/ 64 * 1024,
                /*nDefaultTimeOut*/ 5000,
                &sa);

            LocalFree(sd);
            return pipe;
        }
    }

    DWORD RunPipeServer(HANDLE stopEvent)
    {
        for (;;)
        {
            if (WaitForSingleObject(stopEvent, 0) == WAIT_OBJECT_0)
            {
                return ERROR_SUCCESS;
            }

            HANDLE pipe = CreateProtectedPipe();
            if (pipe == INVALID_HANDLE_VALUE)
            {
                return GetLastError();
            }

            // ConnectNamedPipe blocks until a client opens the pipe.  The
            // service control handler signals stopEvent AND closes the pipe
            // handle (via DisconnectNamedPipe from the stop handler) to
            // unblock us during shutdown — we observe that path via
            // ERROR_BROKEN_PIPE / ERROR_INVALID_HANDLE.
            BOOL connected = ConnectNamedPipe(pipe, nullptr);
            DWORD err = connected ? ERROR_SUCCESS : GetLastError();
            if (!connected && err == ERROR_PIPE_CONNECTED)
            {
                connected = TRUE;
            }

            if (WaitForSingleObject(stopEvent, 0) == WAIT_OBJECT_0)
            {
                CloseHandle(pipe);
                return ERROR_SUCCESS;
            }

            if (connected)
            {
                HandleConnection(pipe);
                FlushFileBuffers(pipe);
                DisconnectNamedPipe(pipe);
            }

            CloseHandle(pipe);
        }
    }
}
