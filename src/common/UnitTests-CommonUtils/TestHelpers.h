#pragma once

#include "pch.h"
#include <string>
#include <filesystem>
#include <fstream>
#include <random>

namespace TestHelpers
{
    // RAII helper for creating and cleaning up temporary files
    class TempFile
    {
    public:
        TempFile(const std::wstring& content = L"", const std::wstring& extension = L".txt")
        {
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);

            // Generate a unique filename
            std::random_device rd;
            std::mt19937 gen(rd());
            std::uniform_int_distribution<> dis(10000, 99999);

            m_path = std::wstring(tempPath) + L"test_" + std::to_wstring(dis(gen)) + extension;

            if (!content.empty())
            {
                std::wofstream file(m_path);
                file << content;
            }
        }

        ~TempFile()
        {
            if (std::filesystem::exists(m_path))
            {
                std::filesystem::remove(m_path);
            }
        }

        TempFile(const TempFile&) = delete;
        TempFile& operator=(const TempFile&) = delete;

        const std::wstring& path() const { return m_path; }

        void write(const std::string& content)
        {
            std::ofstream file(m_path, std::ios::binary);
            file << content;
        }

        void write(const std::wstring& content)
        {
            std::wofstream file(m_path);
            file << content;
        }

        std::wstring read()
        {
            std::wifstream file(m_path);
            return std::wstring((std::istreambuf_iterator<wchar_t>(file)),
                               std::istreambuf_iterator<wchar_t>());
        }

    private:
        std::wstring m_path;
    };

    // RAII helper for creating and cleaning up temporary directories
    class TempDirectory
    {
    public:
        TempDirectory()
        {
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);

            std::random_device rd;
            std::mt19937 gen(rd());
            std::uniform_int_distribution<> dis(10000, 99999);

            m_path = std::wstring(tempPath) + L"testdir_" + std::to_wstring(dis(gen));
            std::filesystem::create_directories(m_path);
        }

        ~TempDirectory()
        {
            if (std::filesystem::exists(m_path))
            {
                std::filesystem::remove_all(m_path);
            }
        }

        TempDirectory(const TempDirectory&) = delete;
        TempDirectory& operator=(const TempDirectory&) = delete;

        const std::wstring& path() const { return m_path; }

    private:
        std::wstring m_path;
    };

    // Registry test key path - use HKCU for non-elevated tests
    inline const std::wstring TestRegistryPath = L"Software\\PowerToys\\UnitTests";

    // RAII helper for registry key creation/cleanup
    class TestRegistryKey
    {
    public:
        TestRegistryKey(const std::wstring& subKey = L"")
        {
            m_path = TestRegistryPath;
            if (!subKey.empty())
            {
                m_path += L"\\" + subKey;
            }

            HKEY key;
            if (RegCreateKeyExW(HKEY_CURRENT_USER, m_path.c_str(), 0, nullptr,
                               REG_OPTION_NON_VOLATILE, KEY_ALL_ACCESS, nullptr,
                               &key, nullptr) == ERROR_SUCCESS)
            {
                RegCloseKey(key);
                m_created = true;
            }
        }

        ~TestRegistryKey()
        {
            if (m_created)
            {
                RegDeleteTreeW(HKEY_CURRENT_USER, m_path.c_str());
            }
        }

        TestRegistryKey(const TestRegistryKey&) = delete;
        TestRegistryKey& operator=(const TestRegistryKey&) = delete;

        bool isValid() const { return m_created; }
        const std::wstring& path() const { return m_path; }

        bool setStringValue(const std::wstring& name, const std::wstring& value)
        {
            HKEY key;
            if (RegOpenKeyExW(HKEY_CURRENT_USER, m_path.c_str(), 0, KEY_SET_VALUE, &key) != ERROR_SUCCESS)
            {
                return false;
            }

            auto result = RegSetValueExW(key, name.c_str(), 0, REG_SZ,
                                        reinterpret_cast<const BYTE*>(value.c_str()),
                                        static_cast<DWORD>((value.length() + 1) * sizeof(wchar_t)));
            RegCloseKey(key);
            return result == ERROR_SUCCESS;
        }

        bool setDwordValue(const std::wstring& name, DWORD value)
        {
            HKEY key;
            if (RegOpenKeyExW(HKEY_CURRENT_USER, m_path.c_str(), 0, KEY_SET_VALUE, &key) != ERROR_SUCCESS)
            {
                return false;
            }

            auto result = RegSetValueExW(key, name.c_str(), 0, REG_DWORD,
                                        reinterpret_cast<const BYTE*>(&value), sizeof(DWORD));
            RegCloseKey(key);
            return result == ERROR_SUCCESS;
        }

    private:
        std::wstring m_path;
        bool m_created = false;
    };

    // Helper to wait for a condition with timeout
    template<typename Predicate>
    bool WaitFor(Predicate pred, std::chrono::milliseconds timeout = std::chrono::milliseconds(5000))
    {
        auto start = std::chrono::steady_clock::now();
        while (!pred())
        {
            if (std::chrono::steady_clock::now() - start > timeout)
            {
                return false;
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(10));
        }
        return true;
    }
}
