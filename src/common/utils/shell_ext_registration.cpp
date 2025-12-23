#include "pch.h"
#include "shell_ext_registration.h"

#include "../logger/logger.h"

namespace runtime_shell_ext
{
    namespace detail
    {
        struct unique_hkey
        {
            HKEY h{ nullptr };
            unique_hkey() = default;
            explicit unique_hkey(HKEY handle) : h(handle) {}
            ~unique_hkey() { if (h) RegCloseKey(h); }
            unique_hkey(const unique_hkey&) = delete;
            unique_hkey& operator=(const unique_hkey&) = delete;
            unique_hkey(unique_hkey&& other) noexcept : h(other.h) { other.h = nullptr; }
            unique_hkey& operator=(unique_hkey&& other) noexcept
            {
                if (this != &other)
                {
                    if (h)
                    {
                        RegCloseKey(h);
                    }
                    h = other.h;
                    other.h = nullptr;
                }
                return *this;
            }
            HKEY get() const { return h; }
            HKEY* put()
            {
                if (h)
                {
                    RegCloseKey(h);
                    h = nullptr;
                }
                return &h;
            }
        };

        std::wstring base_dir_from_module(HMODULE moduleInstance)
        {
            wchar_t buf[MAX_PATH];
            if (GetModuleFileNameW(moduleInstance, buf, MAX_PATH))
            {
                PathRemoveFileSpecW(buf);
                return buf;
            }
            return L"";
        }

        std::wstring pick_existing_dll(const std::wstring& base, const std::vector<std::wstring>& candidates)
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

        bool sentinel_exists(const Spec& spec)
        {
            unique_hkey key;
            if (RegOpenKeyExW(HKEY_CURRENT_USER, spec.sentinelKey.c_str(), 0, KEY_READ, key.put()) != ERROR_SUCCESS)
            {
                return false;
            }
            DWORD value = 0;
            DWORD size = sizeof(value);
            return RegQueryValueExW(key.get(), spec.sentinelValue.c_str(), nullptr, nullptr, reinterpret_cast<LPBYTE>(&value), &size) == ERROR_SUCCESS && value == 1;
        }

        void write_sentinel(const Spec& spec)
        {
            unique_hkey key;
            if (RegCreateKeyExW(HKEY_CURRENT_USER, spec.sentinelKey.c_str(), 0, nullptr, 0, KEY_WRITE, nullptr, key.put(), nullptr) == ERROR_SUCCESS)
            {
                DWORD one = 1;
                RegSetValueExW(key.get(), spec.sentinelValue.c_str(), 0, REG_DWORD, reinterpret_cast<const BYTE*>(&one), sizeof(one));
            }
        }

        void write_inproc_server(const Spec& spec, const std::wstring& dllPath)
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

        std::wstring read_inproc_server(const Spec& spec)
        {
            using namespace std::string_literals;
            std::wstring inprocKey = L"Software\\Classes\\CLSID\\"s + spec.clsid + L"\\InprocServer32";
            unique_hkey key;
            if (RegOpenKeyExW(HKEY_CURRENT_USER, inprocKey.c_str(), 0, KEY_READ, key.put()) != ERROR_SUCCESS)
            {
                return L"";
            }
            wchar_t buf[MAX_PATH];
            DWORD size = sizeof(buf);
            if (RegQueryValueExW(key.get(), nullptr, nullptr, nullptr, reinterpret_cast<LPBYTE>(buf), &size) == ERROR_SUCCESS)
            {
                return std::wstring(buf);
            }
            return L"";
        }

        void write_default_value_key(const std::wstring& keyPath, const std::wstring& value)
        {
            unique_hkey key;
            if (RegCreateKeyExW(HKEY_CURRENT_USER, keyPath.c_str(), 0, nullptr, 0, KEY_WRITE, nullptr, key.put(), nullptr) == ERROR_SUCCESS)
            {
                RegSetValueExW(key.get(), nullptr, 0, REG_SZ, reinterpret_cast<const BYTE*>(value.c_str()), static_cast<DWORD>((value.size() + 1) * sizeof(wchar_t)));
            }
        }

        bool representative_association_exists(const Spec& spec)
        {
            using namespace std::string_literals;
            if (spec.representativeSystemExt.empty() || spec.systemFileAssocHandlerName.empty())
            {
                return true;
            }
            std::wstring keyPath = L"Software\\Classes\\SystemFileAssociations\\"s + spec.representativeSystemExt + L"\\ShellEx\\ContextMenuHandlers\\" + spec.systemFileAssocHandlerName;
            unique_hkey key;
            return RegOpenKeyExW(HKEY_CURRENT_USER, keyPath.c_str(), 0, KEY_READ, key.put()) == ERROR_SUCCESS;
        }
    }

    bool EnsureRegistered(const Spec& spec, HMODULE moduleInstance)
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
            for (const auto& ext : spec.systemFileAssocExtensions)
            {
                using namespace std::string_literals;
                auto baseKey = L"Software\\Classes\\SystemFileAssociations\\"s + ext + L"\\ShellEx\\ContextMenuHandlers\\" + spec.systemFileAssocHandlerName;
                detail::write_default_value_key(baseKey, spec.clsid);
            }
            if (spec.logRepairs)
            {
                Logger::info(L"Runtime shell extension registration repaired {}", spec.clsid);
            }
            detail::write_sentinel(spec);
        }
        else
        {
            Logger::trace(L"Runtime shell extension registration already up to date for {}", spec.clsid);
        }
        return true;
    }

    bool Unregister(const Spec& spec)
    {
        using namespace std::string_literals;

        for (const auto& path : spec.contextMenuHandlerKeyPaths)
        {
            RegDeleteTreeW(HKEY_CURRENT_USER, path.c_str());
        }

        for (const auto& path : spec.extraAssociationPaths)
        {
            RegDeleteTreeW(HKEY_CURRENT_USER, path.c_str());
        }

        if (!spec.systemFileAssocExtensions.empty() && !spec.systemFileAssocHandlerName.empty())
        {
            for (const auto& ext : spec.systemFileAssocExtensions)
            {
                std::wstring keyPath = L"Software\\Classes\\SystemFileAssociations\\";
                keyPath += ext;
                keyPath += L"\\ShellEx\\ContextMenuHandlers\\";
                keyPath += spec.systemFileAssocHandlerName;
                RegDeleteTreeW(HKEY_CURRENT_USER, keyPath.c_str());
            }
        }

        if (!spec.clsid.empty())
        {
            std::wstring clsidRoot = L"Software\\Classes\\CLSID\\"s + spec.clsid;
            RegDeleteTreeW(HKEY_CURRENT_USER, clsidRoot.c_str());
        }

        if (!spec.sentinelKey.empty() && !spec.sentinelValue.empty())
        {
            HKEY key{};
            if (RegOpenKeyExW(HKEY_CURRENT_USER, spec.sentinelKey.c_str(), 0, KEY_SET_VALUE, &key) == ERROR_SUCCESS)
            {
                RegDeleteValueW(key, spec.sentinelValue.c_str());
                RegCloseKey(key);
            }
        }

        Logger::info(L"Runtime shell extension unregistered for {}", spec.clsid);
        return true;
    }
}
