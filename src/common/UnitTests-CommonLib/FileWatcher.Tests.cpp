#include "pch.h"
#include <common/SettingsAPI/FileWatcher.h>
#include <common/utils/atomic_file.h>

#include <chrono>
#include <condition_variable>
#include <cstddef>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <mutex>
#include <string>
#include <system_error>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonLib
{
    namespace
    {
        class WatcherTestDirectory
        {
        public:
            WatcherTestDirectory()
            {
                GUID id{};
                Assert::IsTrue(SUCCEEDED(CoCreateGuid(&id)));
                wchar_t idString[39]{};
                Assert::IsTrue(StringFromGUID2(id, idString, ARRAYSIZE(idString)) != 0);
                m_path = std::filesystem::temp_directory_path() / (std::wstring(L"PowerToys.FileWatcher.Tests.") + idString);
                Assert::IsTrue(std::filesystem::create_directory(m_path));
            }

            ~WatcherTestDirectory()
            {
                std::error_code error;
                std::filesystem::remove_all(m_path, error);
            }

            WatcherTestDirectory(const WatcherTestDirectory&) = delete;
            WatcherTestDirectory& operator=(const WatcherTestDirectory&) = delete;

            const std::filesystem::path& path() const { return m_path; }

        private:
            std::filesystem::path m_path;
        };

        class WatchedFileCache
        {
        public:
            void reload(const std::filesystem::path& path)
            {
                std::ifstream stream(path, std::ios::binary);
                const auto readError = stream ? ERROR_SUCCESS : GetLastError();
                {
                    std::lock_guard lock(m_mutex);
                    ++m_reloadAttempts;
                    m_lastReadError = readError;
                }
                if (!stream)
                {
                    return;
                }

                const std::string contents{ std::istreambuf_iterator<char>(stream), std::istreambuf_iterator<char>() };
                {
                    std::lock_guard lock(m_mutex);
                    m_contents = contents;
                }
                m_changed.notify_all();
            }

            bool waitFor(const std::string& expected)
            {
                std::unique_lock lock(m_mutex);
                const bool updated = m_changed.wait_for(lock, std::chrono::seconds(5), [&]() { return m_contents == expected; });
                if (!updated)
                {
                    const auto message = L"Cache reload attempts=" + std::to_wstring(m_reloadAttempts) + L", last read error=" + std::to_wstring(m_lastReadError) + L"\n";
                    Microsoft::VisualStudio::CppUnitTestFramework::Logger::WriteMessage(message.c_str());
                }
                return updated;
            }

        private:
            std::mutex m_mutex;
            std::condition_variable m_changed;
            std::string m_contents;
            unsigned int m_reloadAttempts = 0;
            DWORD m_lastReadError = ERROR_SUCCESS;
        };

    }

    TEST_CLASS (FileWatcherTests)
    {
    public:
        TEST_METHOD (FirstOrdinaryWriteRefreshesCache)
        {
            WatcherTestDirectory directory;
            const auto destination = directory.path() / L"SeTtInGs.json";
            const std::string contents = "{\"enabled\":false}";
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), atomic_file::write(destination.wstring(), contents));

            WIN32_FILE_ATTRIBUTE_DATA attributes{};
            Assert::IsTrue(GetFileAttributesExW(destination.c_str(), GetFileExInfoStandard, &attributes) != FALSE);
            ULARGE_INTEGER nextWriteTime{};
            nextWriteTime.HighPart = attributes.ftLastWriteTime.dwHighDateTime;
            nextWriteTime.LowPart = attributes.ftLastWriteTime.dwLowDateTime;
            nextWriteTime.QuadPart += 10000000; // One second in FILETIME units.
            const FILETIME lastWriteTime{ nextWriteTime.LowPart, nextWriteTime.HighPart };

            WatchedFileCache cache;
            FileWatcher watcher(destination.wstring(), [&]() { cache.reload(destination); });
            {
                // Change only metadata so the first write notification cannot race
                // a partially written file or be hidden by a later content write.
                wil::unique_hfile file(CreateFileW(destination.c_str(), FILE_WRITE_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, 0, nullptr));
                Assert::IsTrue(static_cast<bool>(file));
                Assert::IsTrue(SetFileTime(file.get(), nullptr, nullptr, &lastWriteTime) != FALSE);
            }

            Assert::IsTrue(cache.waitFor(contents), L"The first ordinary write did not refresh the cache");
        }

        TEST_METHOD (AtomicCreationRefreshesCache)
        {
            WatcherTestDirectory directory;
            const auto destination = directory.path() / L"settings.json";
            WatchedFileCache cache;
            FileWatcher watcher(destination.wstring(), [&]() { cache.reload(destination); }, true);

            const std::string contents = "{\"enabled\":false}";
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), atomic_file::write(destination.wstring(), contents));
            Assert::IsTrue(cache.waitFor(contents), L"The first atomic creation did not refresh the cache");
        }

        TEST_METHOD (FirstAtomicReplacementRefreshesCache)
        {
            WatcherTestDirectory directory;
            const auto destination = directory.path() / L"settings.json";
            const std::string original = "{\"enabled\":true}";
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), atomic_file::write(destination.wstring(), original));
            WatchedFileCache cache;
            cache.reload(destination);
            FileWatcher watcher(destination.wstring(), [&]() { cache.reload(destination); }, true);

            const std::string replacement = "{\"enabled\":false}";
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), atomic_file::write(destination.wstring(), replacement));
            Assert::IsTrue(cache.waitFor(replacement), L"The first atomic replacement did not refresh the cache");
        }

        TEST_METHOD (AtomicReplacementWithSameTimestampRefreshesCache)
        {
            WatcherTestDirectory directory;
            const auto destination = directory.path() / L"settings.json";
            const auto staged = directory.path() / L"settings.tmp";
            const std::string original = "{\"enabled\":true}";
            const std::string replacement = "{\"enabled\":false}";
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), atomic_file::write(destination.wstring(), original));
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), atomic_file::write(staged.wstring(), replacement));

            WIN32_FILE_ATTRIBUTE_DATA attributes{};
            Assert::IsTrue(GetFileAttributesExW(destination.c_str(), GetFileExInfoStandard, &attributes) != FALSE);
            {
                wil::unique_hfile file(CreateFileW(staged.c_str(), FILE_WRITE_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, 0, nullptr));
                Assert::IsTrue(static_cast<bool>(file));
                Assert::IsTrue(SetFileTime(file.get(), nullptr, nullptr, &attributes.ftLastWriteTime) != FALSE);
            }

            WatchedFileCache cache;
            cache.reload(destination);
            FileWatcher watcher(destination.wstring(), [&]() { cache.reload(destination); }, true);

            {
                wil::unique_hfile file(CreateFileW(staged.c_str(), DELETE, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, 0, nullptr));
                Assert::IsTrue(static_cast<bool>(file));
                const auto destinationName = destination.wstring();
                const auto nameLength = destinationName.size() * sizeof(wchar_t);
                std::vector<std::byte> renameBuffer(sizeof(FILE_RENAME_INFO) + nameLength);
                auto rename = reinterpret_cast<FILE_RENAME_INFO*>(renameBuffer.data());
                rename->Flags = FILE_RENAME_FLAG_REPLACE_IF_EXISTS | FILE_RENAME_FLAG_POSIX_SEMANTICS;
                rename->RootDirectory = nullptr;
                rename->FileNameLength = static_cast<DWORD>(nameLength);
                std::memcpy(rename->FileName, destinationName.data(), nameLength);
                Assert::IsTrue(SetFileInformationByHandle(file.get(), FileRenameInfoEx, rename, static_cast<DWORD>(renameBuffer.size())) != FALSE);
            }
            Assert::IsTrue(cache.waitFor(replacement), L"A replacement with the same timestamp did not refresh the cache");
        }
    };
}
