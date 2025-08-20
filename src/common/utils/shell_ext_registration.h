// Shared runtime shell extension registration utility for PowerToys modules.
// Provides a generic EnsureRegistered function so individual modules only need
// to supply a specification (CLSID, sentinel, handler key paths, etc.).

#pragma once

#include <string>
#include <vector>
#include <windows.h>
#include <shlwapi.h>

#include "../logger/logger.h"

namespace runtime_shell_ext
{
    struct Spec
    {
        // Mandatory
        std::wstring clsid;                 // e.g. {GUID}
        std::wstring sentinelKey;           // e.g. Software\\Microsoft\\PowerToys\\ModuleName
        std::wstring sentinelValue;         // e.g. ContextMenuRegistered
        std::vector<std::wstring> dllFileCandidates; // relative filenames (pick first existing)
        std::vector<std::wstring> contextMenuHandlerKeyPaths; // full HKCU relative paths where default value = CLSID

        // Optional
        std::wstring friendlyName;          // if non-empty written as default under CLSID root
        bool writeOptInEmptyValue = true;   // write ContextMenuOptIn="" under CLSID root (legacy pattern)
        bool writeThreadingModel = true;    // write Apartment threading model
        std::vector<std::wstring> extraAssociationPaths; // additional key paths (DragDropHandlers etc.) default=CLSID
        std::vector<std::wstring> systemFileAssocExtensions; // e.g. .png -> Software\\Classes\\SystemFileAssociations\\.png\\ShellEx\\ContextMenuHandlers\\<HandlerName>
        std::wstring systemFileAssocHandlerName; // e.g. ImageResizer
        std::wstring representativeSystemExt;    // used to decide if associations need repair (.png)
        bool logRepairs = true;
    };

    namespace detail
    {
        // Minimal RAII wrapper for HKEY
        struct unique_hkey
        {
            HKEY h{ nullptr };
            unique_hkey() = default;
            explicit unique_hkey(HKEY handle) : h(handle) {}
            ~unique_hkey() { if (h) RegCloseKey(h); }
            unique_hkey(const unique_hkey&) = delete;
            unique_hkey& operator=(const unique_hkey&) = delete;
            unique_hkey(unique_hkey&& other) noexcept : h(other.h) { other.h = nullptr; }
            unique_hkey& operator=(unique_hkey&& other) noexcept { if (this != &other) { if (h) RegCloseKey(h); h = other.h; other.h = nullptr; } return *this; }
            HKEY get() const { return h; }
            HKEY* put() { if (h) { RegCloseKey(h); h = nullptr; } return &h; }
        };
        inline std::wstring base_dir_from_module(HMODULE h)
        {
            wchar_t buf[MAX_PATH];
            if (GetModuleFileNameW(h, buf, MAX_PATH))
            {
                PathRemoveFileSpecW(buf);
                return buf;
            }
            return L"";
        }

        inline std::wstring pick_existing_dll(const std::wstring& base, const std::vector<std::wstring>& candidates)
        {
            for (const auto& rel : candidates)
            {
                std::wstring full = base + L"\\" + rel;
                if (GetFileAttributesW(full.c_str()) != INVALID_FILE_ATTRIBUTES)
                {
                    return full;
                }
            }
            if (!candidates.empty())
            {
                return base + L"\\" + candidates.front();
            }
            return L"";
        }

        inline bool sentinel_exists(const Spec& spec)
        {
            unique_hkey key;
            if (RegOpenKeyExW(HKEY_CURRENT_USER, spec.sentinelKey.c_str(), 0, KEY_READ, key.put()) != ERROR_SUCCESS)
                return false;
            DWORD v = 0; DWORD sz = sizeof(v);
            return RegQueryValueExW(key.get(), spec.sentinelValue.c_str(), nullptr, nullptr, reinterpret_cast<LPBYTE>(&v), &sz) == ERROR_SUCCESS && v == 1;
        }

        inline void write_sentinel(const Spec& spec)
        {
            unique_hkey key;
            if (RegCreateKeyExW(HKEY_CURRENT_USER, spec.sentinelKey.c_str(), 0, nullptr, 0, KEY_WRITE, nullptr, key.put(), nullptr) == ERROR_SUCCESS)
            {
                DWORD one = 1;
                RegSetValueExW(key.get(), spec.sentinelValue.c_str(), 0, REG_DWORD, reinterpret_cast<const BYTE*>(&one), sizeof(one));
            }
        }

        inline void write_inproc_server(const Spec& spec, const std::wstring& dllPath)
        {
            using namespace std::string_literals;
            std::wstring clsidRoot = L"Software\\Classes\\CLSID\\"s + spec.clsid;
            std::wstring inprocKey = clsidRoot + L"\\InprocServer32";
            {
                unique_hkey key;
                if (RegCreateKeyExW(HKEY_CURRENT_USER, clsidRoot.c_str(), 0, nullptr, 0, KEY_WRITE, nullptr, key.put(), nullptr) == ERROR_SUCCESS)
                {
                    if (!spec.friendlyName.empty())
                    {
                        RegSetValueExW(key.get(), nullptr, 0, REG_SZ, reinterpret_cast<const BYTE*>(spec.friendlyName.c_str()), static_cast<DWORD>((spec.friendlyName.size() + 1) * sizeof(wchar_t)));
                    }
                    if (spec.writeOptInEmptyValue)
                    {
                        const wchar_t* optIn = L"ContextMenuOptIn";
                        const wchar_t empty = L'\0';
                        RegSetValueExW(key.get(), optIn, 0, REG_SZ, reinterpret_cast<const BYTE*>(&empty), sizeof(empty));
                    }
                }
            }
            unique_hkey key;
            if (RegCreateKeyExW(HKEY_CURRENT_USER, inprocKey.c_str(), 0, nullptr, 0, KEY_WRITE, nullptr, key.put(), nullptr) == ERROR_SUCCESS)
            {
                RegSetValueExW(key.get(), nullptr, 0, REG_SZ, reinterpret_cast<const BYTE*>(dllPath.c_str()), static_cast<DWORD>((dllPath.size() + 1) * sizeof(wchar_t)));
                if (spec.writeThreadingModel)
                {
                    const wchar_t* tm = L"Apartment";
                    RegSetValueExW(key.get(), L"ThreadingModel", 0, REG_SZ, reinterpret_cast<const BYTE*>(tm), static_cast<DWORD>((wcslen(tm) + 1) * sizeof(wchar_t)));
                }
            }
        }

        inline std::wstring read_inproc_server(const Spec& spec)
        {
            using namespace std::string_literals;
            std::wstring inprocKey = L"Software\\Classes\\CLSID\\"s + spec.clsid + L"\\InprocServer32";
            unique_hkey key;
            if (RegOpenKeyExW(HKEY_CURRENT_USER, inprocKey.c_str(), 0, KEY_READ, key.put()) != ERROR_SUCCESS)
                return L"";
            wchar_t buf[MAX_PATH]; DWORD sz = sizeof(buf);
            if (RegQueryValueExW(key.get(), nullptr, nullptr, nullptr, reinterpret_cast<LPBYTE>(buf), &sz) == ERROR_SUCCESS)
                return std::wstring(buf);
            return L"";
        }

        inline void write_default_value_key(const std::wstring& keyPath, const std::wstring& value)
        {
            unique_hkey key;
            if (RegCreateKeyExW(HKEY_CURRENT_USER, keyPath.c_str(), 0, nullptr, 0, KEY_WRITE, nullptr, key.put(), nullptr) == ERROR_SUCCESS)
            {
                RegSetValueExW(key.get(), nullptr, 0, REG_SZ, reinterpret_cast<const BYTE*>(value.c_str()), static_cast<DWORD>((value.size() + 1) * sizeof(wchar_t)));
            }
        }

        inline bool representative_association_exists(const Spec& spec)
        {
            using namespace std::string_literals;
            if (spec.representativeSystemExt.empty() || spec.systemFileAssocHandlerName.empty())
                return true;
            std::wstring keyPath = L"Software\\Classes\\SystemFileAssociations\\"s + spec.representativeSystemExt + L"\\ShellEx\\ContextMenuHandlers\\" + spec.systemFileAssocHandlerName;
            unique_hkey key;
            return RegOpenKeyExW(HKEY_CURRENT_USER, keyPath.c_str(), 0, KEY_READ, key.put()) == ERROR_SUCCESS;
        }
    }

    inline bool EnsureRegistered(const Spec& spec, HMODULE moduleInstance)
    {
        using namespace std::string_literals;
        auto base = detail::base_dir_from_module(moduleInstance);
        auto dllPath = detail::pick_existing_dll(base, spec.dllFileCandidates);
        if (dllPath.empty())
        {
            Logger::error(L"Runtime registration: cannot locate dll path for CLSID {}", spec.clsid);
            return false;
        }
        bool exists = detail::sentinel_exists(spec);
        bool repaired = false;
        if (exists)
        {
            auto current = detail::read_inproc_server(spec);
            if (_wcsicmp(current.c_str(), dllPath.c_str()) != 0)
            {
                detail::write_inproc_server(spec, dllPath);
                repaired = true;
            }
            if (!detail::representative_association_exists(spec))
            {
                repaired = true;
            }
        }
        if (!exists)
        {
            detail::write_inproc_server(spec, dllPath);
        }
        if (!exists || repaired)
        {
            for (const auto& path : spec.contextMenuHandlerKeyPaths)
            {
                detail::write_default_value_key(path, spec.clsid);
            }
            for (const auto& path : spec.extraAssociationPaths)
            {
                detail::write_default_value_key(path, spec.clsid);
            }
            if (!spec.systemFileAssocExtensions.empty() && !spec.systemFileAssocHandlerName.empty())
            {
                for (const auto& ext : spec.systemFileAssocExtensions)
                {
                    std::wstring path = L"Software\\Classes\\SystemFileAssociations\\"s + ext + L"\\ShellEx\\ContextMenuHandlers\\" + spec.systemFileAssocHandlerName;
                    detail::write_default_value_key(path, spec.clsid);
                }
            }
        }
        if (!exists)
        {
            detail::write_sentinel(spec);
            Logger::info(L"Runtime registration completed for CLSID {}", spec.clsid);
        }
        else if (repaired && spec.logRepairs)
        {
            Logger::info(L"Runtime registration repaired for CLSID {}", spec.clsid);
        }
        return true;
    }

    inline bool Unregister(const Spec& spec)
    {
        using namespace std::string_literals;
        // Remove handler key paths
        for (const auto& path : spec.contextMenuHandlerKeyPaths)
        {
            RegDeleteTreeW(HKEY_CURRENT_USER, path.c_str());
        }
        // Remove extra association paths (e.g., drag & drop handlers)
        for (const auto& path : spec.extraAssociationPaths)
        {
            RegDeleteTreeW(HKEY_CURRENT_USER, path.c_str());
        }
        // Remove per-extension system file association handler keys
        if (!spec.systemFileAssocExtensions.empty() && !spec.systemFileAssocHandlerName.empty())
        {
            for (const auto& ext : spec.systemFileAssocExtensions)
            {
                std::wstring keyPath = L"Software\\Classes\\SystemFileAssociations\\"s + ext + L"\\ShellEx\\ContextMenuHandlers\\" + spec.systemFileAssocHandlerName;
                RegDeleteTreeW(HKEY_CURRENT_USER, keyPath.c_str());
            }
        }
        // Remove CLSID branch
        if (!spec.clsid.empty())
        {
            std::wstring clsidRoot = L"Software\\Classes\\CLSID\\"s + spec.clsid;
            RegDeleteTreeW(HKEY_CURRENT_USER, clsidRoot.c_str());
        }
        // Remove sentinel value (not deleting entire key to avoid disturbing other values)
        if (!spec.sentinelKey.empty() && !spec.sentinelValue.empty())
        {
            HKEY hKey{};
            if (RegOpenKeyExW(HKEY_CURRENT_USER, spec.sentinelKey.c_str(), 0, KEY_SET_VALUE, &hKey) == ERROR_SUCCESS)
            {
                RegDeleteValueW(hKey, spec.sentinelValue.c_str());
                RegCloseKey(hKey);
            }
        }
        Logger::info(L"Successfully unregistered CLSID {}", spec.clsid);
        return true;
    }
}
