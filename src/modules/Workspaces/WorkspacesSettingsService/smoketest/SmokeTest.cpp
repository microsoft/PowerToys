// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.
//
// Console smoke test for PTSettingsSvc.
//
// Usage:
//   PowerToys.PTSettingsSvcSmokeTest.exe ping
//   PowerToys.PTSettingsSvcSmokeTest.exe get [<output-file>]
//   PowerToys.PTSettingsSvcSmokeTest.exe put <input-file>
//
// Pair with `PowerToys.PTSettingsSvc.exe --console` in another terminal
// when iterating without installing & registering the service.
//
// NB: this exe is NOT in the caller-binding allow-list, so the service
// will return AuthRejected unless one of the following holds:
//   * you copy/rename this exe to one of the allow-listed basenames
//     (e.g. PowerToys.WorkspacesEditor.exe) under the PT install folder
//     pointed to by HKLM\SOFTWARE\Classes\PowerToys\InstallFolder
//     (or by the PT_DEV_INSTALL_FOLDER env var in dev builds), AND
//   * that folder's DACL is admin-only writable.
//
// The verify-prototype.ps1 script automates both prerequisites.

#include "../../WorkspacesSettingsClient/PTSettingsClient.h"

#include <windows.h>
#include <cstdio>
#include <string>
#include <fstream>
#include <vector>

namespace
{
    std::vector<uint8_t> ReadAllBytes(const char* path)
    {
        std::ifstream f(path, std::ios::binary | std::ios::ate);
        if (!f) return {};
        std::streamsize size = f.tellg();
        if (size <= 0)
        {
            return {};
        }
        std::vector<uint8_t> buf(static_cast<size_t>(size));
        f.seekg(0, std::ios::beg);
        f.read(reinterpret_cast<char*>(buf.data()), size);
        return buf;
    }

    bool WriteAllBytes(const char* path, const std::vector<uint8_t>& bytes)
    {
        std::ofstream f(path, std::ios::binary | std::ios::trunc);
        if (!f) return false;
        if (!bytes.empty())
        {
            f.write(reinterpret_cast<const char*>(bytes.data()),
                    static_cast<std::streamsize>(bytes.size()));
        }
        return static_cast<bool>(f);
    }

    const char* Name(PTSettingsClient::Result r)
    {
        switch (r)
        {
        case PTSettingsClient::Result::Ok:                  return "Ok";
        case PTSettingsClient::Result::ServiceUnavailable:  return "ServiceUnavailable";
        case PTSettingsClient::Result::AuthRejected:        return "AuthRejected";
        case PTSettingsClient::Result::NamespaceUnknown:    return "NamespaceUnknown";
        case PTSettingsClient::Result::NotFound:            return "NotFound";
        case PTSettingsClient::Result::ProtocolError:       return "ProtocolError";
        case PTSettingsClient::Result::PayloadTooLarge:     return "PayloadTooLarge";
        case PTSettingsClient::Result::IoError:             return "IoError";
        case PTSettingsClient::Result::UnknownStatus:       return "UnknownStatus";
        }
        return "?";
    }
}

int main(int argc, char* argv[])
{
    if (argc < 2)
    {
        std::printf("usage: %s ping | get [<output-file>] | put <input-file>\n", argv[0]);
        return 2;
    }

    std::string cmd = argv[1];

    if (cmd == "ping")
    {
        auto rc = PTSettingsClient::Ping();
        std::printf("Ping -> %s\n", Name(rc));
        return rc == PTSettingsClient::Result::Ok ? 0 : 1;
    }

    if (cmd == "get")
    {
        std::vector<uint8_t> bytes;
        auto rc = PTSettingsClient::GetBlob(bytes);
        std::printf("GetBlob -> %s, %zu bytes\n", Name(rc), bytes.size());
        if (rc == PTSettingsClient::Result::Ok)
        {
            if (argc >= 3)
            {
                bool ok = WriteAllBytes(argv[2], bytes);
                std::printf("  wrote %zu bytes to %s%s\n",
                            bytes.size(), argv[2], ok ? "" : " (FAILED)");
                if (!ok) return 1;
            }
            else if (!bytes.empty())
            {
                std::fwrite(bytes.data(), 1, bytes.size(), stdout);
                std::printf("\n");
            }
        }
        return rc == PTSettingsClient::Result::Ok ||
               rc == PTSettingsClient::Result::NotFound ? 0 : 1;
    }

    if (cmd == "put" && argc >= 3)
    {
        auto bytes = ReadAllBytes(argv[2]);
        if (bytes.empty())
        {
            std::fprintf(stderr, "input file empty or unreadable: %s\n", argv[2]);
            return 2;
        }
        auto rc = PTSettingsClient::PutBlob(bytes);
        std::printf("PutBlob (%zu bytes) -> %s\n", bytes.size(), Name(rc));
        return rc == PTSettingsClient::Result::Ok ? 0 : 1;
    }

    std::fprintf(stderr, "unknown / incomplete command: %s\n", argv[1]);
    return 2;
}
