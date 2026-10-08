#include "pch.h"
#include <common/utils/atomic_file.h>

#include <array>
#include <atomic>
#include <filesystem>
#include <stdexcept>
#include <string>
#include <system_error>
#include <thread>
#include <winrt/Windows.Data.Json.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace winrt::Windows::Data::Json;

namespace UnitTestsCommonLib
{
    namespace
    {
        class TemporaryDirectory
        {
        public:
            TemporaryDirectory()
            {
                static std::atomic<unsigned int> sequence{ 0 };
                const auto parent = std::filesystem::temp_directory_path();
                for (unsigned int attempt = 0; attempt < 100; ++attempt)
                {
                    const auto candidate = parent / (L"PowerToys.AtomicFile.Tests." + std::to_wstring(GetCurrentProcessId()) + L"." + std::to_wstring(GetTickCount64()) + L"." + std::to_wstring(sequence.fetch_add(1)));
                    if (std::filesystem::create_directory(candidate))
                    {
                        m_path = candidate;
                        return;
                    }
                }

                throw std::runtime_error("Could not create a unique test directory");
            }

            ~TemporaryDirectory()
            {
                // Only the uniquely named directory successfully created by this instance is owned.
                std::error_code error;
                std::filesystem::remove_all(m_path, error);
            }

            TemporaryDirectory(const TemporaryDirectory&) = delete;
            TemporaryDirectory& operator=(const TemporaryDirectory&) = delete;

            const std::filesystem::path& path() const { return m_path; }

        private:
            std::filesystem::path m_path;
        };

        class FileHandle
        {
        public:
            explicit FileHandle(HANDLE handle) :
                m_handle(handle)
            {
            }

            ~FileHandle()
            {
                if (m_handle != INVALID_HANDLE_VALUE)
                {
                    CloseHandle(m_handle);
                }
            }

            FileHandle(const FileHandle&) = delete;
            FileHandle& operator=(const FileHandle&) = delete;

            HANDLE get() const { return m_handle; }

        private:
            HANDLE m_handle;
        };

        DWORD readFile(const std::filesystem::path& path, std::string& contents)
        {
            // A reader must allow the old file to be replaced while its handle remains open.
            FileHandle file(CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
            if (file.get() == INVALID_HANDLE_VALUE)
            {
                return GetLastError();
            }

            contents.clear();
            std::array<char, 4096> buffer;
            for (;;)
            {
                DWORD bytesRead = 0;
                if (!ReadFile(file.get(), buffer.data(), static_cast<DWORD>(buffer.size()), &bytesRead, nullptr))
                {
                    return GetLastError();
                }

                if (bytesRead == 0)
                {
                    return ERROR_SUCCESS;
                }

                contents.append(buffer.data(), bytesRead);
            }
        }

        void assertOnlyFile(const TemporaryDirectory& directory, const std::filesystem::path& expected)
        {
            size_t count = 0;
            for (const auto& entry : std::filesystem::directory_iterator(directory.path()))
            {
                Assert::AreEqual(expected.wstring(), entry.path().wstring(), L"An unexpected temporary file or directory was left behind");
                ++count;
            }

            Assert::AreEqual(size_t{ 1 }, count);
        }
    }

    TEST_CLASS (AtomicFileTests)
    {
    public:
        TEST_METHOD (WriteCreatesAndReplacesUtf8Json)
        {
            TemporaryDirectory directory;
            const auto destination = directory.path() / L"\u8BBE\u7F6E.json";
            const std::string original = "{\"message\":\"\xE4\xBD\xA0\xE5\xA5\xBD\",\"enabled\":false}";
            const std::string replacement = "{\"message\":\"\xF0\x9F\x8E\x89\",\"enabled\":true}";

            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), atomic_file::write(destination.wstring(), original));
            std::string contents;
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), readFile(destination, contents));
            Assert::AreEqual(original, contents);
            auto json = JsonObject::Parse(winrt::to_hstring(contents));
            Assert::AreEqual(std::wstring(L"\u4F60\u597D"), std::wstring(json.GetNamedString(L"message")));
            Assert::IsFalse(json.GetNamedBoolean(L"enabled"));
            assertOnlyFile(directory, destination);

            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), atomic_file::write(destination.wstring(), replacement));
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), readFile(destination, contents));
            Assert::AreEqual(replacement, contents);
            json = JsonObject::Parse(winrt::to_hstring(contents));
            Assert::AreEqual(std::wstring(L"\U0001F389"), std::wstring(json.GetNamedString(L"message")));
            Assert::IsTrue(json.GetNamedBoolean(L"enabled"));
            assertOnlyFile(directory, destination);
        }

        TEST_METHOD (WriteWhenReplacementIsBlockedPreservesOriginalAndRemovesTemporaryFile)
        {
            TemporaryDirectory directory;
            const auto destination = directory.path() / L"settings.json";
            const std::string original = "{\"enabled\":true,\"hotkey\":\"Ctrl+Alt+Space\"}";
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), atomic_file::write(destination.wstring(), original));

            {
                FileHandle reader(CreateFileW(destination.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
                Assert::IsTrue(reader.get() != INVALID_HANDLE_VALUE);

                const auto result = atomic_file::write(destination.wstring(), "{\"enabled\":false}");
                Assert::IsTrue(result != ERROR_SUCCESS, L"Replacement must fail while a reader denies delete sharing");
                std::string contents;
                Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), readFile(destination, contents));
                Assert::AreEqual(original, contents);
                assertOnlyFile(directory, destination);
            }

            // Once the blocking handle is closed, the same destination can be saved again.
            const std::string replacement = "{\"enabled\":false}";
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), atomic_file::write(destination.wstring(), replacement));
            std::string contents;
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), readFile(destination, contents));
            Assert::AreEqual(replacement, contents);
            assertOnlyFile(directory, destination);
        }

        TEST_METHOD (WriteWhenParentIsMissingLeavesNoArtifacts)
        {
            TemporaryDirectory directory;
            const auto existing = directory.path() / L"settings.json";
            const std::string original = "{\"enabled\":true}";
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), atomic_file::write(existing.wstring(), original));

            const auto destination = directory.path() / L"missing" / L"settings.json";
            Assert::IsTrue(atomic_file::write(destination.wstring(), "{\"enabled\":false}") != ERROR_SUCCESS);

            std::string contents;
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), readFile(existing, contents));
            Assert::AreEqual(original, contents);
            assertOnlyFile(directory, existing);
        }

        TEST_METHOD (ConcurrentWritesExposeOnlyCompleteJsonToReaders)
        {
            TemporaryDirectory directory;
            const auto destination = directory.path() / L"settings.json";
            const std::array<std::string, 3> documents{
                "{\"writer\":0,\"text\":\"" + std::string(4096, '0') + "\"}",
                "{\"writer\":1,\"text\":\"" + std::string(48 * 1024, 'A') + "\"}",
                "{\"writer\":2,\"text\":\"" + std::string(96 * 1024, 'B') + "\"}"
            };
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), atomic_file::write(destination.wstring(), documents[0]));

            constexpr unsigned int writesPerThread = 64;
            std::atomic<bool> start{ false };
            std::atomic<unsigned int> completedWriters{ 0 };
            std::atomic<unsigned int> completedReads{ 0 };
            std::atomic<unsigned int> successfulWrites{ 0 };
            std::atomic<DWORD> writeError{ ERROR_SUCCESS };
            std::atomic<bool> writerThrew{ false };

            const auto writer = [&](std::stop_token stop, size_t documentIndex) {
                // jthread cancellation also releases this gate if creating the second thread fails.
                while (!start.load() && !stop.stop_requested())
                {
                    std::this_thread::yield();
                }

                try
                {
                    for (unsigned int i = 0; i < writesPerThread && !stop.stop_requested(); ++i)
                    {
                        const auto result = atomic_file::write(destination.wstring(), documents[documentIndex]);
                        if (result != ERROR_SUCCESS)
                        {
                            DWORD expected = ERROR_SUCCESS;
                            writeError.compare_exchange_strong(expected, result);
                            break;
                        }

                        successfulWrites.fetch_add(1);
                        // Keep both writers alive until the reader has taken its first snapshot.
                        while (completedReads.load() == 0 && !stop.stop_requested())
                        {
                            std::this_thread::yield();
                        }
                    }
                }
                catch (...)
                {
                    writerThrew = true;
                }

                completedWriters.fetch_add(1);
            };

            std::jthread first(writer, size_t{ 1 });
            std::jthread second(writer, size_t{ 2 });
            start = true;

            DWORD readError = ERROR_SUCCESS;
            bool invalidJson = false;
            bool unexpectedContents = false;
            do
            {
                std::string contents;
                const auto result = readFile(destination, contents);
                if (result != ERROR_SUCCESS)
                {
                    readError = result;
                }
                else
                {
                    unexpectedContents |= contents != documents[0] && contents != documents[1] && contents != documents[2];
                    JsonObject parsed;
                    invalidJson |= !JsonObject::TryParse(winrt::to_hstring(contents), parsed);
                }

                completedReads.fetch_add(1);
            } while (completedWriters.load() != 2);

            first.join();
            second.join();

            Assert::IsFalse(writerThrew.load(), L"A writer threw an exception");
            const auto writeFailureMessage = L"A writer failed after " + std::to_wstring(successfulWrites.load()) + L" successful writes; reader error=" + std::to_wstring(readError);
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), writeError.load(), writeFailureMessage.c_str());
            Assert::AreEqual(writesPerThread * 2, successfulWrites.load());
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), readError, L"The destination must remain present and readable throughout replacement");
            Assert::IsFalse(invalidJson, L"A reader observed invalid JSON");
            Assert::IsFalse(unexpectedContents, L"A reader observed truncated or mixed document bytes");
            std::string finalContents;
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), readFile(destination, finalContents));
            Assert::IsTrue(finalContents == documents[1] || finalContents == documents[2]);
            assertOnlyFile(directory, destination);
        }
    };
}
