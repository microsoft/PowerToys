// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "PipeServer.h"
#include "Bindings.h"
#include "CallerAuth.h"
#include "FileGuard.h"
#include "Paths.h"
#include "protocol/Protocol.h"
#include "protocol/PipeName.h"

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
        // Pipe SD (Approach 4): connect/read/write is granted to the OWNING
        // user's SID only (the per-user service instance serves exactly that
        // user); SYSTEM and BUILTIN\Administrators get full control for
        // diagnostics.  Everyone else is implicitly denied.  The protocol layer
        // still does the real access control (owner-SID match + caller image +
        // allow-list).  If the owner SID is unknown (dev/standalone), fall back
        // to Authenticated Users so console smoke tests still work.
        std::wstring BuildPipeSddl(const std::wstring& ownerSid)
        {
            const std::wstring grantee = ownerSid.empty() ? std::wstring(L"AU") : ownerSid;
            return L"D:"
                   L"(A;;GRGW;;;" + grantee + L")"   // owning user : connect/read/write
                   L"(A;;GA;;;SY)"                    // SYSTEM : full
                   L"(A;;GA;;;BA)";                   // BUILTIN\Administrators : full
        }

        // The pipe is created with FILE_FLAG_OVERLAPPED so that both the
        // ConnectNamedPipe wait and every read/write can be aborted the instant
        // the service is asked to stop.  The server handles one connection at a
        // time on the worker thread, so a single reusable manual-reset event and
        // a borrowed stop-event pointer are sufficient (no per-call allocation,
        // no cross-thread sharing of an OVERLAPPED).
        HANDLE g_ioEvent = nullptr;   // manual-reset; owned by RunPipeServer
        HANDLE g_stopEvt = nullptr;   // borrowed from ServiceMain / console

        // Wait for an overlapped op to complete OR for stop to be signalled.
        // On stop, cancels the pending I/O and reaps it (so the OVERLAPPED is
        // safe to leave scope) and returns false.
        bool WaitOverlapped(HANDLE pipe, OVERLAPPED& ov, DWORD& transferred)
        {
            HANDLE waits[2] = { g_ioEvent, g_stopEvt };
            DWORD w = WaitForMultipleObjects(2, waits, FALSE, INFINITE);
            if (w != WAIT_OBJECT_0)
            {
                CancelIoEx(pipe, &ov);
                DWORD reaped = 0;
                GetOverlappedResult(pipe, &ov, &reaped, TRUE);
                return false;
            }
            return GetOverlappedResult(pipe, &ov, &transferred, TRUE) != FALSE;
        }

        bool ReadExact(HANDLE pipe, void* buf, DWORD len)
        {
            BYTE* p = static_cast<BYTE*>(buf);
            DWORD remaining = len;
            while (remaining > 0)
            {
                OVERLAPPED ov{};
                ov.hEvent = g_ioEvent;
                ResetEvent(g_ioEvent);
                DWORD got = 0;
                if (!ReadFile(pipe, p, remaining, &got, &ov))
                {
                    if (GetLastError() != ERROR_IO_PENDING)
                    {
                        return false;
                    }
                    if (!WaitOverlapped(pipe, ov, got))
                    {
                        return false;
                    }
                }
                if (got == 0)
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
                OVERLAPPED ov{};
                ov.hEvent = g_ioEvent;
                ResetEvent(g_ioEvent);
                DWORD wrote = 0;
                if (!WriteFile(pipe, p, remaining, &wrote, &ov))
                {
                    if (GetLastError() != ERROR_IO_PENDING)
                    {
                        return false;
                    }
                    if (!WaitOverlapped(pipe, ov, wrote))
                    {
                        return false;
                    }
                }
                if (wrote == 0)
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
            std::wstring target = GetUserFilePath(id.userSidString,
                                                  id.binding->namespaceId,
                                                  id.binding->fileName);
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

            // The protected store root and per-user <sid> node are provisioned by
            // the elevated register path (owner=SYSTEM, protected DACL granting
            // this service's virtual account Full + the user RX — §12.8).  The
            // low-privilege runtime service only needs to create the namespace
            // child (which inherits that DACL) and write the file; it does NOT
            // touch owner/DACL (it lacks the privilege and doesn't need to).
            // EnsureDirectory is a best-effort no-op when the register path
            // already created these.
            HRESULT hr = EnsureDirectory(GetSettingsRoot());
            if (FAILED(hr))
            {
                SendStatus(pipe, Status::IoError);
                return;
            }

            hr = EnsureDirectory(GetUserFolder(id.userSidString));
            if (FAILED(hr))
            {
                SendStatus(pipe, Status::IoError);
                return;
            }

            // Ensure the <sid>\<namespace> folder.  It inherits the protected
            // DACL from the per-user node, so no tightening is needed here.
            std::wstring nsFolder = GetUserNamespaceFolder(id.userSidString,
                                                           id.binding->namespaceId);
            hr = EnsureDirectory(nsFolder);
            if (FAILED(hr))
            {
                SendStatus(pipe, Status::IoError);
                return;
            }

            hr = WriteFileAtomically(
                GetUserFilePath(id.userSidString,
                                id.binding->namespaceId,
                                id.binding->fileName),
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
            // Owner SID this instance serves; fall back to the current process
            // user for console/dev runs (see PipeName.h).
            std::wstring ownerSid = GetServiceOwnerSid();
            if (ownerSid.empty())
            {
                ownerSid = CurrentProcessUserSidString();
            }

            const std::wstring sddl = BuildPipeSddl(ownerSid);
            const std::wstring pipeName = BuildPipeName(ownerSid);

            PSECURITY_DESCRIPTOR sd = nullptr;
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
                    sddl.c_str(), SDDL_REVISION_1, &sd, nullptr))
            {
                return INVALID_HANDLE_VALUE;
            }

            SECURITY_ATTRIBUTES sa{};
            sa.nLength = sizeof(sa);
            sa.lpSecurityDescriptor = sd;
            sa.bInheritHandle = FALSE;

            HANDLE pipe = CreateNamedPipeW(
                pipeName.c_str(),
                PIPE_ACCESS_DUPLEX | FILE_FLAG_FIRST_PIPE_INSTANCE | FILE_FLAG_OVERLAPPED,
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
        g_stopEvt = stopEvent;
        g_ioEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!g_ioEvent)
        {
            return GetLastError();
        }

        DWORD rc = ERROR_SUCCESS;
        for (;;)
        {
            if (WaitForSingleObject(stopEvent, 0) == WAIT_OBJECT_0)
            {
                break;
            }

            HANDLE pipe = CreateProtectedPipe();
            if (pipe == INVALID_HANDLE_VALUE)
            {
                rc = GetLastError();
                break;
            }

            // Overlapped ConnectNamedPipe: rather than blocking indefinitely on
            // an idle pipe, wait on BOTH the connect completion and stopEvent so
            // a SERVICE_CONTROL_STOP (which only signals stopEvent) unblocks us
            // immediately even when no client ever connects.  The synchronous
            // ConnectNamedPipe(pipe, nullptr) used previously could not be
            // interrupted and left the service stuck in STOP_PENDING when idle.
            OVERLAPPED ov{};
            ov.hEvent = g_ioEvent;
            ResetEvent(g_ioEvent);

            bool connected = false;
            bool stopping = false;

            if (ConnectNamedPipe(pipe, &ov))
            {
                connected = true;   // unusual for an overlapped pipe
            }
            else
            {
                DWORD err = GetLastError();
                if (err == ERROR_PIPE_CONNECTED)
                {
                    connected = true;   // a client arrived before we called
                }
                else if (err == ERROR_IO_PENDING)
                {
                    HANDLE waits[2] = { g_ioEvent, stopEvent };
                    if (WaitForMultipleObjects(2, waits, FALSE, INFINITE) == WAIT_OBJECT_0)
                    {
                        DWORD dummy = 0;
                        connected = GetOverlappedResult(pipe, &ov, &dummy, TRUE) != FALSE;
                    }
                    else
                    {
                        stopping = true;
                        CancelIoEx(pipe, &ov);
                        DWORD dummy = 0;
                        GetOverlappedResult(pipe, &ov, &dummy, TRUE);
                    }
                }
        // else: connect failed outright — drop this pipe and loop.
            }

            if (stopping || WaitForSingleObject(stopEvent, 0) == WAIT_OBJECT_0)
            {
                CloseHandle(pipe);
                break;
            }

            if (connected)
            {
                HandleConnection(pipe);
                FlushFileBuffers(pipe);
                DisconnectNamedPipe(pipe);
            }

            CloseHandle(pipe);
        }

        CloseHandle(g_ioEvent);
        g_ioEvent = nullptr;
        g_stopEvt = nullptr;
        return rc;
    }
}
