// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <Windows.h>

#include <cstring>
#include <optional>
#include <string>

namespace file_change_stamp
{
    struct stamp
    {
        FILE_ID_INFO identity{};
        FILETIME last_write{};

        bool operator==(const stamp& other) const noexcept
        {
            return identity.VolumeSerialNumber == other.identity.VolumeSerialNumber &&
                   std::memcmp(identity.FileId.Identifier, other.identity.FileId.Identifier, sizeof(identity.FileId.Identifier)) == 0 &&
                   last_write.dwHighDateTime == other.last_write.dwHighDateTime &&
                   last_write.dwLowDateTime == other.last_write.dwLowDateTime;
        }
    };

    // Capture before reading; only cache the stamp after successfully consuming the data.
    inline std::optional<stamp> from_file(const std::wstring& path) noexcept
    {
        const auto file = CreateFileW(path.c_str(),
                                      FILE_READ_ATTRIBUTES,
                                      FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                                      nullptr,
                                      OPEN_EXISTING,
                                      FILE_ATTRIBUTE_NORMAL,
                                      nullptr);
        if (file == INVALID_HANDLE_VALUE)
        {
            return std::nullopt;
        }

        stamp result;
        const bool succeeded = GetFileInformationByHandleEx(file, FileIdInfo, &result.identity, sizeof(result.identity)) &&
                               GetFileTime(file, nullptr, nullptr, &result.last_write);
        CloseHandle(file);
        if (!succeeded)
        {
            return std::nullopt;
        }
        return result;
    }

    inline bool has_changed(const std::optional<stamp>& previous, const std::optional<stamp>& current) noexcept
    {
        // Missing/unsupported metadata is not evidence that cached data is current.
        return !previous || !current || !(*previous == *current);
    }
}
