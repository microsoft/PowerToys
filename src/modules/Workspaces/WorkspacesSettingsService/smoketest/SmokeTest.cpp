// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.
//
// Console smoke test for PTWorkspacesSvc.
//
// Usage:
//   PowerToys.WorkspacesSvcSmokeTest.exe ping
//   PowerToys.WorkspacesSvcSmokeTest.exe get
//   PowerToys.WorkspacesSvcSmokeTest.exe put  <file.json>
//   PowerToys.WorkspacesSvcSmokeTest.exe migrate <legacy.json>
//
// Pair with `WorkspacesSettingsService.exe --console` in another terminal
// when iterating without installing the MSI / registering the service.
//
// NB: this exe will normally be REJECTED by the service's AuthFailCallerPath
// check (it isn't called PowerToys.WorkspacesEditor.exe and doesn't live in
// the install folder).  That is exactly the point — it demonstrates the
// caller-allow-list working.  To exercise the success path, rename / copy
// the smoke test exe over Editor's name inside the install folder, or run
// the prototype service in console mode with the caller check temporarily
// disabled.

#include "../../WorkspacesSettingsClient/WorkspacesSvcClient.h"

#include <windows.h>
#include <cstdio>
#include <string>
#include <fstream>
#include <sstream>

namespace
{
    std::string ReadAllText(const char* path)
    {
        std::ifstream f(path, std::ios::binary);
        if (!f) return {};
        std::ostringstream ss;
        ss << f.rdbuf();
        return ss.str();
    }

    const char* Name(WorkspacesSvcClient::Result r)
    {
        switch (r)
        {
        case WorkspacesSvcClient::Result::Ok:                  return "Ok";
        case WorkspacesSvcClient::Result::ServiceUnavailable:  return "ServiceUnavailable";
        case WorkspacesSvcClient::Result::AuthRejected:        return "AuthRejected";
        case WorkspacesSvcClient::Result::ProtocolError:       return "ProtocolError";
        case WorkspacesSvcClient::Result::ServerError:         return "ServerError";
        case WorkspacesSvcClient::Result::PayloadInvalid:      return "PayloadInvalid";
        }
        return "?";
    }
}

int main(int argc, char* argv[])
{
    if (argc < 2)
    {
        std::printf("usage: %s ping | get | put <file.json> | migrate <legacy.json>\n", argv[0]);
        return 2;
    }

    std::string cmd = argv[1];

    if (cmd == "ping")
    {
        auto rc = WorkspacesSvcClient::Ping();
        std::printf("Ping -> %s\n", Name(rc));
        return rc == WorkspacesSvcClient::Result::Ok ? 0 : 1;
    }

    if (cmd == "get")
    {
        std::string body;
        auto rc = WorkspacesSvcClient::GetSettings(body);
        std::printf("GetSettings -> %s, %zu bytes\n", Name(rc), body.size());
        if (!body.empty())
        {
            std::fwrite(body.data(), 1, body.size(), stdout);
            std::printf("\n");
        }
        return rc == WorkspacesSvcClient::Result::Ok ? 0 : 1;
    }

    if (cmd == "put" && argc >= 3)
    {
        auto body = ReadAllText(argv[2]);
        auto rc = WorkspacesSvcClient::PutSettings(body);
        std::printf("PutSettings -> %s\n", Name(rc));
        return rc == WorkspacesSvcClient::Result::Ok ? 0 : 1;
    }

    if (cmd == "migrate" && argc >= 3)
    {
        auto body = ReadAllText(argv[2]);
        auto rc = WorkspacesSvcClient::MigrateFromLegacy(body);
        std::printf("MigrateFromLegacy -> %s\n", Name(rc));
        return rc == WorkspacesSvcClient::Result::Ok ? 0 : 1;
    }

    std::fprintf(stderr, "unknown command: %s\n", argv[1]);
    return 2;
}
