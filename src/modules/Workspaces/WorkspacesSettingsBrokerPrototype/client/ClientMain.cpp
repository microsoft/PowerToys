// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "ClientProtocol.h"

#include <windows.h>

#include <chrono>
#include <iostream>
#include <string>
#include <vector>

namespace
{
    struct Options
    {
        std::wstring command;
        uint16_t major = SettingsBrokerPrototype::kProtocolMajor;
        uint16_t minor = SettingsBrokerPrototype::kMaxProtocolMinor;
        uint32_t target = 0;
        std::wstring data;
        std::wstring dataFile;
        std::wstring outFile;
        DWORD milliseconds = 3000;
        DWORD delayBeforeSend = 0;
    };

    std::wstring JsonEscape(const std::wstring& value)
    {
        std::wstring escaped;
        for (const wchar_t character : value)
        {
            switch (character)
            {
            case L'\\': escaped += L"\\\\"; break;
            case L'"': escaped += L"\\\""; break;
            case L'\r': escaped += L"\\r"; break;
            case L'\n': escaped += L"\\n"; break;
            case L'\t': escaped += L"\\t"; break;
            default: escaped += character; break;
            }
        }
        return escaped;
    }

    bool ParseUnsigned(const wchar_t* text, unsigned long& value)
    {
        wchar_t* end = nullptr;
        value = wcstoul(text, &end, 10);
        return end && *end == L'\0';
    }

    bool ParseOptions(int argc, wchar_t** argv, Options& options)
    {
        if (argc < 2)
        {
            return false;
        }
        options.command = argv[1];
        for (int index = 2; index < argc; ++index)
        {
            if (index + 1 >= argc)
            {
                return false;
            }
            unsigned long parsed = 0;
            if (_wcsicmp(argv[index], L"--major") == 0 &&
                ParseUnsigned(argv[++index], parsed))
            {
                options.major = static_cast<uint16_t>(parsed);
            }
            else if (_wcsicmp(argv[index], L"--minor") == 0 &&
                     ParseUnsigned(argv[++index], parsed))
            {
                options.minor = static_cast<uint16_t>(parsed);
            }
            else if (_wcsicmp(argv[index], L"--target") == 0 &&
                     ParseUnsigned(argv[++index], parsed))
            {
                options.target = parsed;
            }
            else if (_wcsicmp(argv[index], L"--milliseconds") == 0 &&
                     ParseUnsigned(argv[++index], parsed))
            {
                options.milliseconds = parsed;
            }
            else if (_wcsicmp(argv[index], L"--delay-before-send") == 0 &&
                     ParseUnsigned(argv[++index], parsed))
            {
                options.delayBeforeSend = parsed;
            }
            else if (_wcsicmp(argv[index], L"--data") == 0)
            {
                options.data = argv[++index];
            }
            else if (_wcsicmp(argv[index], L"--data-file") == 0)
            {
                options.dataFile = argv[++index];
            }
            else if (_wcsicmp(argv[index], L"--out-file") == 0)
            {
                options.outFile = argv[++index];
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    bool ReadWholeFile(const std::wstring& path, std::vector<BYTE>& bytes)
    {
        HANDLE file = CreateFileW(path.c_str(),
                                  GENERIC_READ,
                                  FILE_SHARE_READ,
                                  nullptr,
                                  OPEN_EXISTING,
                                  FILE_ATTRIBUTE_NORMAL,
                                  nullptr);
        if (file == INVALID_HANDLE_VALUE)
        {
            return false;
        }
        LARGE_INTEGER size{};
        if (!GetFileSizeEx(file, &size) ||
            size.QuadPart < 0 ||
            size.QuadPart > SettingsBrokerPrototype::kMaxPayloadBytes)
        {
            CloseHandle(file);
            return false;
        }
        bytes.resize(static_cast<size_t>(size.QuadPart));
        DWORD read = 0;
        const BOOL ok = bytes.empty() ||
                        ReadFile(file, bytes.data(), static_cast<DWORD>(bytes.size()), &read, nullptr);
        CloseHandle(file);
        return ok && read == bytes.size();
    }

    bool WriteWholeFile(const std::wstring& path, const std::vector<BYTE>& bytes)
    {
        HANDLE file = CreateFileW(path.c_str(),
                                  GENERIC_WRITE,
                                  0,
                                  nullptr,
                                  CREATE_ALWAYS,
                                  FILE_ATTRIBUTE_NORMAL,
                                  nullptr);
        if (file == INVALID_HANDLE_VALUE)
        {
            return false;
        }
        DWORD written = 0;
        const BOOL ok = bytes.empty() ||
                        WriteFile(file,
                                  bytes.data(),
                                  static_cast<DWORD>(bytes.size()),
                                  &written,
                                  nullptr);
        CloseHandle(file);
        return ok && written == bytes.size();
    }

    std::vector<BYTE> Utf8Bytes(const std::wstring& text)
    {
        if (text.empty())
        {
            return {};
        }
        const int byteCount = WideCharToMultiByte(CP_UTF8,
                                                  0,
                                                  text.c_str(),
                                                  static_cast<int>(text.size()),
                                                  nullptr,
                                                  0,
                                                  nullptr,
                                                  nullptr);
        std::vector<BYTE> bytes(byteCount);
        WideCharToMultiByte(CP_UTF8,
                            0,
                            text.c_str(),
                            static_cast<int>(text.size()),
                            reinterpret_cast<char*>(bytes.data()),
                            byteCount,
                            nullptr,
                            nullptr);
        return bytes;
    }

    std::wstring Utf8Text(const std::vector<BYTE>& bytes)
    {
        if (bytes.empty())
        {
            return {};
        }
        const int charCount = MultiByteToWideChar(CP_UTF8,
                                                  MB_ERR_INVALID_CHARS,
                                                  reinterpret_cast<const char*>(bytes.data()),
                                                  static_cast<int>(bytes.size()),
                                                  nullptr,
                                                  0);
        if (charCount <= 0)
        {
            return L"<binary>";
        }
        std::wstring text(charCount, L'\0');
        MultiByteToWideChar(CP_UTF8,
                            MB_ERR_INVALID_CHARS,
                            reinterpret_cast<const char*>(bytes.data()),
                            static_cast<int>(bytes.size()),
                            text.data(),
                            charCount);
        return text;
    }

    int TransportFailure(const std::wstring& message)
    {
        std::wcout << L"{\"transport\":\"" << JsonEscape(message) << L"\"}" << std::endl;
        return 3;
    }
}

int wmain(int argc, wchar_t** argv)
{
    Options options;
    if (!ParseOptions(argc, argv, options))
    {
        std::wcerr << L"usage: <ping|get|put|slow|slow-read|create-instance|malformed|oversized> "
                      L"[--major N] [--minor N] [--target N] "
                      L"[--data TEXT|--data-file PATH] [--out-file PATH] "
                      L"[--milliseconds N] [--delay-before-send N]"
                   << std::endl;
        return 64;
    }

    if (_wcsicmp(options.command.c_str(), L"create-instance") == 0)
    {
        HANDLE additional = CreateNamedPipeW(
            SettingsBrokerPrototype::kPipeName,
            PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED,
            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT |
                PIPE_REJECT_REMOTE_CLIENTS,
            SettingsBrokerPrototype::kWorkerCount,
            4096,
            4096,
            SettingsBrokerPrototype::kIoTimeoutMs,
            nullptr);
        const DWORD createError =
            additional == INVALID_HANDLE_VALUE ? GetLastError() : ERROR_SUCCESS;
        if (additional != INVALID_HANDLE_VALUE)
        {
            CloseHandle(additional);
        }
        std::wcout << L"{\"createNamedPipeError\":" << createError << L"}"
                   << std::endl;
        return createError == ERROR_ACCESS_DENIED ? 0 : 4;
    }

    HANDLE pipe = INVALID_HANDLE_VALUE;
    DWORD verifiedServerPid = 0;
    std::wstring error;
    if (!SettingsBrokerPrototype::ConnectToBroker(pipe, verifiedServerPid, error))
    {
        return TransportFailure(error);
    }

    if (_wcsicmp(options.command.c_str(), L"slow") == 0)
    {
        const BYTE partial = static_cast<BYTE>(SettingsBrokerPrototype::kRequestMagic & 0xff);
        HANDLE event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!event)
        {
            CloseHandle(pipe);
            return TransportFailure(L"partial request event creation failed");
        }
        OVERLAPPED overlapped{};
        overlapped.hEvent = event;
        DWORD written = 0;
        BOOL writeOk = WriteFile(pipe,
                                 &partial,
                                 sizeof(partial),
                                 &written,
                                 &overlapped);
        if (!writeOk && GetLastError() == ERROR_IO_PENDING)
        {
            const DWORD wait =
                WaitForSingleObject(event, SettingsBrokerPrototype::kIoTimeoutMs);
            writeOk = wait == WAIT_OBJECT_0 &&
                      GetOverlappedResult(pipe, &overlapped, &written, FALSE);
            if (!writeOk && wait != WAIT_OBJECT_0)
            {
                CancelIoEx(pipe, &overlapped);
                if (WaitForSingleObject(
                        event,
                        SettingsBrokerPrototype::kCancelCompletionTimeoutMs) !=
                    WAIT_OBJECT_0)
                {
                    TerminateProcess(GetCurrentProcess(), ERROR_OPERATION_ABORTED);
                    ExitProcess(ERROR_OPERATION_ABORTED);
                }
                DWORD ignored = 0;
                GetOverlappedResult(pipe, &overlapped, &ignored, FALSE);
            }
        }
        CloseHandle(event);
        if (!writeOk || written != sizeof(partial))
        {
            CloseHandle(pipe);
            return TransportFailure(L"partial request write failed");
        }
        Sleep(options.milliseconds);
        CloseHandle(pipe);
        std::wcout << L"{\"status\":\"SlowComplete\"}" << std::endl;
        return 0;
    }

    std::vector<BYTE> payload;
    SettingsBrokerPrototype::Opcode opcode = SettingsBrokerPrototype::Opcode::Ping;
    const bool slowRead = _wcsicmp(options.command.c_str(), L"slow-read") == 0;
    if (_wcsicmp(options.command.c_str(), L"get") == 0 || slowRead)
    {
        opcode = SettingsBrokerPrototype::Opcode::Get;
    }
    else if (_wcsicmp(options.command.c_str(), L"put") == 0)
    {
        opcode = SettingsBrokerPrototype::Opcode::Put;
        if (!options.dataFile.empty())
        {
            if (!ReadWholeFile(options.dataFile, payload))
            {
                CloseHandle(pipe);
                return TransportFailure(L"cannot read data file");
            }
        }
        else
        {
            payload = Utf8Bytes(options.data);
        }
    }
    else if (_wcsicmp(options.command.c_str(), L"ping") != 0 &&
             _wcsicmp(options.command.c_str(), L"malformed") != 0 &&
             _wcsicmp(options.command.c_str(), L"oversized") != 0)
    {
        CloseHandle(pipe);
        return TransportFailure(L"unknown command");
    }

    SettingsBrokerPrototype::RequestHeader request{
        SettingsBrokerPrototype::kRequestMagic,
        sizeof(SettingsBrokerPrototype::RequestHeader),
        options.major,
        options.minor,
        static_cast<uint16_t>(opcode),
        options.target,
        static_cast<uint32_t>(payload.size()),
    };
    if (_wcsicmp(options.command.c_str(), L"malformed") == 0)
    {
        request.magic ^= 0xffffffffu;
    }
    if (_wcsicmp(options.command.c_str(), L"oversized") == 0)
    {
        request.payloadBytes = SettingsBrokerPrototype::kMaxPayloadBytes + 1;
        payload.clear();
    }

    const auto start = std::chrono::steady_clock::now();
    if (options.delayBeforeSend > 0)
    {
        Sleep(options.delayBeforeSend);
    }
    SettingsBrokerPrototype::ClientResponse response;
    if (!SettingsBrokerPrototype::SendRequest(pipe,
                                              request,
                                              payload,
                                              response,
                                              error,
                                              slowRead ? options.milliseconds : 0))
    {
        CloseHandle(pipe);
        return TransportFailure(error);
    }
    CloseHandle(pipe);
    const auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                             std::chrono::steady_clock::now() - start)
                             .count();

    const auto status =
        static_cast<SettingsBrokerPrototype::Status>(response.header.status);
    if (status == SettingsBrokerPrototype::Status::Ok &&
        opcode == SettingsBrokerPrototype::Opcode::Get &&
        !options.outFile.empty() &&
        !WriteWholeFile(options.outFile, response.payload))
    {
        return TransportFailure(L"cannot write output file");
    }

    std::wcout << L"{\"status\":\""
               << SettingsBrokerPrototype::StatusName(status)
               << L"\",\"major\":" << response.header.major
               << L",\"minor\":" << response.header.minor
               << L",\"capabilities\":" << response.header.capabilities
               << L",\"serverPid\":" << verifiedServerPid
               << L",\"serverPidVerified\":true"
               << L",\"payloadBytes\":" << response.header.payloadBytes
               << L",\"payloadUtf8\":\""
               << (slowRead ? L"" : JsonEscape(Utf8Text(response.payload)))
               << L"\",\"elapsedMs\":" << elapsed << L"}" << std::endl;

    return status == SettingsBrokerPrototype::Status::Ok ? 0 : 2;
}
