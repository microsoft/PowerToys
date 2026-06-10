// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "WorkspacesSvcClient.h"
#include "../WorkspacesSettingsService/protocol/Protocol.h"

#include <windows.h>
#include <vector>
#include <cstring>

namespace WorkspacesSvcClient
{
    namespace
    {
        using WorkspacesSvc::kPipeName;
        using WorkspacesSvc::kMaxPayloadBytes;
        using WorkspacesSvc::Opcode;
        using WorkspacesSvc::Status;

        struct PipeHandle
        {
            HANDLE h = INVALID_HANDLE_VALUE;
            ~PipeHandle()
            {
                if (h != INVALID_HANDLE_VALUE) CloseHandle(h);
            }
        };

        // Opens a connected pipe handle, waiting briefly for the service if
        // it isn't running yet (the SCM may need to launch it on demand).
        bool Connect(PipeHandle& out)
        {
            for (int attempt = 0; attempt < 3; ++attempt)
            {
                HANDLE h = CreateFileW(kPipeName,
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
                WaitNamedPipeW(kPipeName, 2000);
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
            case Status::Ok:                   return Result::Ok;
            case Status::AuthFailToken:
            case Status::AuthFailCallerPath:   return Result::AuthRejected;
            case Status::BadRequest:
            case Status::UnknownOpcode:        return Result::ProtocolError;
            case Status::PayloadTooLarge:
            case Status::JsonInvalid:
            case Status::SchemaUnsupported:    return Result::PayloadInvalid;
            case Status::IoError:
            case Status::Internal:             return Result::ServerError;
            }
            return Result::ServerError;
        }

        Result RoundTrip(Opcode op, const void* payload, uint32_t payloadLen,
                         std::vector<BYTE>& outResp)
        {
            outResp.clear();
            if (payloadLen > kMaxPayloadBytes)
            {
                return Result::PayloadInvalid;
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
        std::vector<BYTE> resp;
        return RoundTrip(Opcode::Ping, nullptr, 0, resp);
    }

    Result GetSettings(std::string& outJsonUtf8)
    {
        std::vector<BYTE> resp;
        Result r = RoundTrip(Opcode::GetSettings, nullptr, 0, resp);
        if (r == Result::Ok)
        {
            outJsonUtf8.assign(reinterpret_cast<const char*>(resp.data()), resp.size());
        }
        else
        {
            outJsonUtf8.clear();
        }
        return r;
    }

    Result PutSettings(const std::string& jsonUtf8)
    {
        std::vector<BYTE> resp;
        return RoundTrip(Opcode::PutSettings,
                         jsonUtf8.data(),
                         static_cast<uint32_t>(jsonUtf8.size()),
                         resp);
    }

    Result MigrateFromLegacy(const std::string& legacyJsonUtf8)
    {
        std::vector<BYTE> resp;
        return RoundTrip(Opcode::MigrateFromLegacy,
                         legacyJsonUtf8.data(),
                         static_cast<uint32_t>(legacyJsonUtf8.size()),
                         resp);
    }
}
