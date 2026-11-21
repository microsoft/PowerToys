#pragma once

#include <Windows.h>
#include <objbase.h>
#include <array>
#include <cstring>
#include <string>

namespace WorkspacesWindowProperties
{
    namespace Properties
    {
        const wchar_t LaunchedByWorkspacesID[] = L"PowerToys_LaunchedByWorkspaces";
        const wchar_t WorkspacesAppID[] = L"PowerToys_WorkspacesAppId";
    }

    namespace detail
    {
        inline constexpr size_t GuidPartCount = sizeof(GUID) / sizeof(ULONG_PTR);
        using GuidParts = std::array<ULONG_PTR, GuidPartCount>;

        inline const auto& GuidPartNames()
        {
            static const auto names = [] {
                std::array<std::wstring, GuidPartCount> result;
                for (size_t i = 0; i < result.size(); ++i)
                {
                    result[i] = Properties::WorkspacesAppID + std::to_wstring(i);
                }
                return result;
            }();
            return names;
        }

        struct GuidProperties
        {
            GuidParts parts{};
            size_t found{};
        };

        inline BOOL CALLBACK CollectGuidPart(HWND, LPWSTR name, HANDLE value, ULONG_PTR context)
        {
            auto& stamp = *reinterpret_cast<GuidProperties*>(context);
            if (!IS_INTRESOURCE(name))
            {
                const auto& names = GuidPartNames();
                for (size_t i = 0; i < names.size(); ++i)
                {
                    if (CompareStringOrdinal(name, -1, names[i].c_str(), -1, TRUE) == CSTR_EQUAL)
                    {
                        stamp.parts[i] = reinterpret_cast<ULONG_PTR>(value);
                        ++stamp.found;
                        break;
                    }
                }
            }
            return stamp.found != stamp.parts.size();
        }
    }

    inline void StampWorkspacesLaunchedProperty(HWND window)
    {
        ::SetPropW(window, Properties::LaunchedByWorkspacesID, reinterpret_cast<HANDLE>(1));
    }

    inline void StampWorkspacesGuidProperty(HWND window, const std::wstring& appId)
    {
        const auto text = appId.size() == 36 ? L"{" + appId + L"}" : appId;
        GUID guid{};
        if (text.size() != 38 || text.front() != L'{' || text.back() != L'}' || FAILED(CLSIDFromString(text.c_str(), &guid)))
        {
            return;
        }

        detail::GuidParts parts{};
        std::memcpy(parts.data(), &guid, sizeof(guid));
        const auto& names = detail::GuidPartNames();
        // Publish the first word last so an interrupted replacement has no complete stamp.
        RemovePropW(window, names.front().c_str());
        for (size_t i = 1; i < parts.size(); ++i)
        {
            if (!SetPropW(window, names[i].c_str(), reinterpret_cast<HANDLE>(parts[i])))
            {
                return;
            }
        }
        SetPropW(window, names.front().c_str(), reinterpret_cast<HANDLE>(parts.front()));
    }

    inline std::wstring GetGuidFromHwnd(HWND window)
    {
        const auto& names = detail::GuidPartNames();
        detail::GuidProperties stamp;
        for (size_t i = 0; i < names.size(); ++i)
        {
            stamp.parts[i] = reinterpret_cast<ULONG_PTR>(GetPropW(window, names[i].c_str()));
            if (!stamp.parts[i])
            {
                break;
            }
            ++stamp.found;
        }
        if (stamp.found != stamp.parts.size() || GetPropW(window, names.front().c_str()) != reinterpret_cast<HANDLE>(stamp.parts.front()))
        {
            // GetPropW returns null for both a missing property and a stored zero word.
            // Enumeration includes zero-valued properties written by older versions too.
            stamp = {};
            EnumPropsExW(window, detail::CollectGuidPart, reinterpret_cast<LPARAM>(&stamp));
            if (stamp.found != stamp.parts.size())
            {
                return {};
            }
        }

        GUID guid;
        std::memcpy(&guid, stamp.parts.data(), sizeof(guid));
        wchar_t text[39];
        return StringFromGUID2(guid, text, ARRAYSIZE(text)) ? text : L"";
    }
}
