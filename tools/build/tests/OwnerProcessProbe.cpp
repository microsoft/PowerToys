// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <common/utils/owner_process.h>
#include <fstream>
#include <iostream>

int wmain(int argc, wchar_t** argv)
{
    try
    {
        wil::unique_handle token;
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, token.put()))
        {
            return static_cast<int>(GetLastError());
        }
        const auto identity = owner_process::ReadIdentity(token.get());
        std::wcout << L"Probe actor: elevated=" << identity.elevated << L" session=" << identity.session
                   << L" integrity=" << identity.integrity << L"\n" << std::flush;
        if (argc == 5 && std::wstring_view(argv[1]) == L"--child")
        {
            if (!owner_process::IsNormalIdentity(identity) || identity.sid != argv[2] ||
                identity.session != std::stoul(argv[3]))
            {
                return ERROR_INVALID_OWNER;
            }
            std::ofstream output(std::filesystem::path(argv[4]), std::ios::binary);
            output << "{\"sameOwner\":true,\"sameSession\":true,\"elevated\":false,\"integrity\":"
                   << identity.integrity << "}\n";
            output.close();
            return output.fail() ? ERROR_WRITE_FAULT : ERROR_SUCCESS;
        }
        if (argc != 2)
        {
            return ERROR_BAD_ARGUMENTS;
        }
        wchar_t image[32768]{};
        if (!GetModuleFileNameW(nullptr, image, ARRAYSIZE(image)))
        {
            return static_cast<int>(GetLastError());
        }
        const auto directory = std::filesystem::path(image).parent_path();
        const auto arguments = L"--child " + identity.sid + L" " + std::to_wstring(identity.session) +
                               L" \"" + argv[1] + L"\"";
        wil::unique_handle child;
        const auto started = owner_process::Start(image, arguments, directory, child);
        if (started)
        {
            std::wcerr << L"Owner process start failed: " << started << L"\n";
            return static_cast<int>(started);
        }
        std::wcout << L"Created owner child PID=" << GetProcessId(child.get()) << L"\n" << std::flush;
        if (WaitForSingleObject(child.get(), 60000) != WAIT_OBJECT_0)
        {
            TerminateProcess(child.get(), WAIT_TIMEOUT);
            return WAIT_TIMEOUT;
        }
        DWORD exitCode = ERROR_GEN_FAILURE;
        if (!GetExitCodeProcess(child.get(), &exitCode))
        {
            return static_cast<int>(GetLastError());
        }
        return static_cast<int>(exitCode);
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << "\n";
        return ERROR_GEN_FAILURE;
    }
}
