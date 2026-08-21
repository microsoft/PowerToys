// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "PTSettingsClient.h"
#include "../WorkspacesSettingsService/protocol/Protocol.h"
#include "../WorkspacesSettingsService/protocol/PipeName.h"

#include <windows.h>
#include <vector>
#include <cstring>
#include <string>

namespace PTSettingsClient
{
    namespace
    {
        using PTSettingsSvc::kMaxPayloadBytes;
        using PTSettingsSvc::Opcode;
        using PTSettingsSvc::Status;

        // This client reaches ITS OWN user's service instance, whose pipe is
        // \\.\pipe\PTSettingsSvc_<SID> where <SID> is our own token SID
        //.  Computed once per process.
        const std::wstring& OwnPipeName()
        {
            static const std::wstring name =
                PTSettingsSvc::BuildPipeName(PTSettingsSvc::CurrentProcessUserSidString());
            return name;
        }

        struct PipeHandle
        {
            HANDLE h = INVALID_HANDLE_VALUE;
            ~PipeHandle()
            {
                if (h != INVALID_HANDLE_VALUE) CloseHandle(h);
            }
        };

        bool Connect(PipeHandle& out)
        {
            const std::wstring& pipeName = OwnPipeName();
            for (int attempt = 0; attempt < 3; ++attempt)
            {
                HANDLE h = CreateFileW(pipeName.c_str(),
                                       GENERIC_READ | GENERIC_WRITE,
                                       0,
                                       nullptr,
                                       OPEN_EXISTING,
                                       // Allow the server to impersonate us
                                       // so it can read our SID; anything
                                       // weaker yields an Anonymous token
                                       // and the server's auth check fails.
                                       SECURITY_SQOS_PRESENT | SECURITY_IMPERSONATION,
                                       nullptr);
                if (h != INVALID_HANDLE_VALUE)
                {
                    out.h = h;
                    return true;
                }
                DWORD err = GetLastError();
                if (err != ERROR_PIPE_BUSY && err != ERROR_FILE_NOT_FOUND)
                {
                    return false;
                }
                WaitNamedPipeW(pipeName.c_str(), 2000);
            }
            return false;
        }

        bool WriteAll(HANDLE h, const void* buf, DWORD len)
        {
            const BYTE* p = static_cast<const BYTE*>(buf);
            while (len > 0)
            {
                DWORD wrote = 0;
                if (!WriteFile(h, p, len, &wrote, nullptr) || wrote == 0) return false;
                p += wrote;
                len -= wrote;
            }
            return true;
        }

        bool ReadAll(HANDLE h, void* buf, DWORD len)
        {
            BYTE* p = static_cast<BYTE*>(buf);
            while (len > 0)
            {
                DWORD got = 0;
                if (!ReadFile(h, p, len, &got, nullptr) || got == 0) return false;
                p += got;
                len -= got;
            }
            return true;
        }

        Result MapStatus(Status s)
        {
            switch (s)
            {
            case Status::Ok:               return Result::Ok;
            case Status::AuthFailToken:
            case Status::AuthFailCaller:   return Result::AuthRejected;
            case Status::NamespaceUnknown: return Result::NamespaceUnknown;
            case Status::BadRequest:
            case Status::UnknownOpcode:    return Result::ProtocolError;
            case Status::PayloadTooLarge:  return Result::PayloadTooLarge;
            case Status::NotFound:         return Result::NotFound;
            case Status::IoError:          return Result::IoError;
            }
            return Result::UnknownStatus;
        }

        Result RoundTrip(Opcode op, const void* payload, uint32_t payloadLen,
                         std::vector<uint8_t>& outResp)
        {
            outResp.clear();
            if (payloadLen > kMaxPayloadBytes)
            {
                return Result::PayloadTooLarge;
            }

            PipeHandle pipe;
            if (!Connect(pipe))
            {
                return Result::ServiceUnavailable;
            }

            uint8_t opByte = static_cast<uint8_t>(op);
            if (!WriteAll(pipe.h, &opByte, sizeof(opByte)) ||
                !WriteAll(pipe.h, &payloadLen, sizeof(payloadLen)) ||
                (payloadLen > 0 && !WriteAll(pipe.h, payload, payloadLen)))
            {
                return Result::ProtocolError;
            }

            uint8_t statusByte = 0;
            uint32_t respLen = 0;
            if (!ReadAll(pipe.h, &statusByte, sizeof(statusByte)) ||
                !ReadAll(pipe.h, &respLen, sizeof(respLen)))
            {
                return Result::ProtocolError;
            }
            if (respLen > kMaxPayloadBytes)
            {
                return Result::ProtocolError;
            }
            if (respLen > 0)
            {
                outResp.resize(respLen);
                if (!ReadAll(pipe.h, outResp.data(), respLen))
                {
                    outResp.clear();
                    return Result::ProtocolError;
                }
            }
            return MapStatus(static_cast<Status>(statusByte));
        }
    }

    Result Ping()
    {
        std::vector<uint8_t> resp;
        return RoundTrip(Opcode::Ping, nullptr, 0, resp);
    }

    Result GetBlob(std::vector<uint8_t>& outBytes)
    {
        return RoundTrip(Opcode::GetBlob, nullptr, 0, outBytes);
    }

    Result PutBlob(const std::vector<uint8_t>& bytes)
    {
        std::vector<uint8_t> resp;
        return RoundTrip(Opcode::PutBlob,
                         bytes.data(),
                         static_cast<uint32_t>(bytes.size()),
                         resp);
    }
}
