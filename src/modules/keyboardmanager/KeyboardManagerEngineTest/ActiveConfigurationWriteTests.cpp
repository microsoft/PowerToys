#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include <filesystem>
#include <fstream>
#include <thread>

#include <common/utils/json.h>
#include <keyboardmanager/common/SettingsFile.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingLogicTests
{
    // The engine saves a profile switch (auto-switch, cycle hotkey) with
    // KeyboardManagerSettingsFile::WriteActiveConfiguration and reports success only when it
    // returns true. Every test works in its own folder under the temp folder.
    TEST_CLASS (ActiveConfigurationWriteTests)
    {
    private:
        std::filesystem::path folder;
        std::filesystem::path settingsPath;

        static std::string ReadAll(const std::filesystem::path& path)
        {
            std::ifstream stream(path, std::ios::binary);
            return std::string{ std::istreambuf_iterator<char>{ stream }, std::istreambuf_iterator<char>{} };
        }

        static void WriteAll(const std::filesystem::path& path, const std::string& text)
        {
            std::ofstream stream(path, std::ios::binary | std::ios::trunc);
            stream << text;
        }

        size_t TemporaryFileCount() const
        {
            size_t count = 0;
            for (const auto& entry : std::filesystem::directory_iterator(folder))
            {
                if (entry.path().extension() == L".tmp")
                {
                    ++count;
                }
            }

            return count;
        }

        static constexpr const char* InitialSettings =
            R"({"name":"Keyboard Manager","properties":{"activeConfiguration":{"value":"default"},"keyboardConfigurations":{"value":["default","Work"]},"useNewEditor":{"value":true}},"version":"1"})";

    public:
        TEST_METHOD_INITIALIZE(CreateFolder)
        {
            wchar_t temp[MAX_PATH + 1]{};
            GetTempPathW(MAX_PATH, temp);
            folder = std::filesystem::path(temp) / (L"kbm-active-config-" + std::to_wstring(GetCurrentProcessId()) + L"-" + std::to_wstring(GetTickCount64()));
            std::filesystem::create_directories(folder);
            settingsPath = folder / L"settings.json";
            WriteAll(settingsPath, InitialSettings);
        }

        TEST_METHOD_CLEANUP(RemoveFolder)
        {
            SetFileAttributesW(settingsPath.c_str(), FILE_ATTRIBUTE_NORMAL);
            std::error_code ec;
            std::filesystem::remove_all(folder, ec);
        }

        TEST_METHOD (ReplacesTheValue_AndKeepsTheOtherProperties)
        {
            Assert::IsTrue(KeyboardManagerSettingsFile::WriteActiveConfiguration(settingsPath.wstring(), L"Work"));

            const auto parsed = json::from_file(settingsPath.wstring());
            Assert::IsTrue(parsed.has_value());
            const auto properties = parsed.value().GetNamedObject(L"properties");
            Assert::AreEqual(std::wstring(L"Work"), std::wstring(properties.GetNamedObject(L"activeConfiguration").GetNamedString(L"value")));
            Assert::AreEqual(2u, properties.GetNamedObject(L"keyboardConfigurations").GetNamedArray(L"value").Size());
            Assert::IsTrue(properties.GetNamedObject(L"useNewEditor").GetNamedBoolean(L"value"));
            Assert::AreEqual(std::wstring(L"Keyboard Manager"), std::wstring(parsed.value().GetNamedString(L"name")));
            Assert::AreEqual(size_t{ 0 }, TemporaryFileCount());
        }

        // The file cannot be replaced (here: read-only): the switch is reported as not saved, and the
        // previous file is byte for byte what it was.
        TEST_METHOD (WhenTheFileCannotBeReplaced_ReturnsFalse_AndLeavesTheOldFileIntact)
        {
            Assert::IsTrue(SetFileAttributesW(settingsPath.c_str(), FILE_ATTRIBUTE_READONLY) != 0);

            Assert::IsFalse(KeyboardManagerSettingsFile::WriteActiveConfiguration(settingsPath.wstring(), L"Work"));

            Assert::AreEqual(std::string(InitialSettings), ReadAll(settingsPath));
            Assert::AreEqual(size_t{ 0 }, TemporaryFileCount());
        }

        // A read-only file cannot be replaced by trying again, so it is not waited for.
        TEST_METHOD (ReadOnlyFile_FailsWithoutWaiting)
        {
            Assert::IsTrue(SetFileAttributesW(settingsPath.c_str(), FILE_ATTRIBUTE_READONLY) != 0);

            const ULONGLONG before = GetTickCount64();
            Assert::IsFalse(KeyboardManagerSettingsFile::WriteActiveConfiguration(settingsPath.wstring(), L"Work"));

            // Ten tries 50 ms apart would take about 450 ms.
            Assert::IsTrue(GetTickCount64() - before < 300);
        }

        // While the engine waits for a busy file, the editor may save a change to it (a new profile
        // in the list). The engine must then write that newer file with only the active profile
        // changed, not the copy it read before waiting.
        TEST_METHOD (ChangeSavedWhileWaiting_IsKept)
        {
            const HANDLE reader = CreateFileW(settingsPath.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
            Assert::IsTrue(reader != INVALID_HANDLE_VALUE);
            const std::filesystem::path path = settingsPath;
            std::thread other([reader, path] {
                Sleep(90);

                // Written in place through a second handle (the reader blocks only the rename).
                std::ofstream stream(path, std::ios::binary | std::ios::trunc);
                stream << R"({"name":"Keyboard Manager","properties":{"activeConfiguration":{"value":"default"},"keyboardConfigurations":{"value":["default","Work","Home"]},"useNewEditor":{"value":true}},"version":"1"})";
                stream.close();
                CloseHandle(reader);
            });

            const bool saved = KeyboardManagerSettingsFile::WriteActiveConfiguration(settingsPath.wstring(), L"Work");
            other.join();

            Assert::IsTrue(saved);
            const auto parsed = json::from_file(settingsPath.wstring());
            Assert::IsTrue(parsed.has_value());
            const auto properties = parsed.value().GetNamedObject(L"properties");
            Assert::AreEqual(std::wstring(L"Work"), std::wstring(properties.GetNamedObject(L"activeConfiguration").GetNamedString(L"value")));
            Assert::AreEqual(3u, properties.GetNamedObject(L"keyboardConfigurations").GetNamedArray(L"value").Size());
        }

        TEST_METHOD (IsSameProfile_IgnoresLetterCase)
        {
            Assert::IsTrue(KeyboardManagerSettingsFile::IsSameProfile(L"Work", L"work"));
            Assert::IsTrue(KeyboardManagerSettingsFile::IsSameProfile(L"default", L"Default"));
            Assert::IsTrue(KeyboardManagerSettingsFile::IsSameProfile(L"", L""));
            Assert::IsFalse(KeyboardManagerSettingsFile::IsSameProfile(L"Work", L"Work2"));
            Assert::IsFalse(KeyboardManagerSettingsFile::IsSameProfile(L"Work", L""));
        }

        // Another program keeps the file open without delete sharing for the whole call (as a reader
        // that never lets go): same result.
        TEST_METHOD (WhenAReaderNeverReleasesTheFile_ReturnsFalse_AndLeavesTheOldFileIntact)
        {
            const HANDLE reader = CreateFileW(settingsPath.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
            Assert::IsTrue(reader != INVALID_HANDLE_VALUE);

            const bool saved = KeyboardManagerSettingsFile::WriteActiveConfiguration(settingsPath.wstring(), L"Work");
            CloseHandle(reader);

            Assert::IsFalse(saved);
            Assert::AreEqual(std::string(InitialSettings), ReadAll(settingsPath));
            Assert::AreEqual(size_t{ 0 }, TemporaryFileCount());
        }

        // A reader that has the file open only for a moment does not make the switch fail.
        TEST_METHOD (WaitsForAReaderThatReleasesTheFile)
        {
            const HANDLE reader = CreateFileW(settingsPath.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
            Assert::IsTrue(reader != INVALID_HANDLE_VALUE);
            std::thread releaser([reader] {
                Sleep(150);
                CloseHandle(reader);
            });

            const bool saved = KeyboardManagerSettingsFile::WriteActiveConfiguration(settingsPath.wstring(), L"Work");
            releaser.join();

            Assert::IsTrue(saved);
            const auto parsed = json::from_file(settingsPath.wstring());
            Assert::IsTrue(parsed.has_value());
            Assert::AreEqual(std::wstring(L"Work"), std::wstring(parsed.value().GetNamedObject(L"properties").GetNamedObject(L"activeConfiguration").GetNamedString(L"value")));
            Assert::AreEqual(size_t{ 0 }, TemporaryFileCount());
        }

        // A settings.json that cannot be parsed is not written back (that would erase the profile list).
        TEST_METHOD (UnreadableSettings_ReturnsFalse_AndDoesNotTouchTheFile)
        {
            const std::string broken = R"({ "properties": )";
            WriteAll(settingsPath, broken);

            Assert::IsFalse(KeyboardManagerSettingsFile::WriteActiveConfiguration(settingsPath.wstring(), L"Work"));

            Assert::AreEqual(broken, ReadAll(settingsPath));
            Assert::AreEqual(size_t{ 0 }, TemporaryFileCount());
        }

        TEST_METHOD (MissingSettings_ReturnsFalse_AndCreatesNothing)
        {
            std::filesystem::remove(settingsPath);

            Assert::IsFalse(KeyboardManagerSettingsFile::WriteActiveConfiguration(settingsPath.wstring(), L"Work"));

            Assert::IsFalse(std::filesystem::exists(settingsPath));
            Assert::AreEqual(size_t{ 0 }, TemporaryFileCount());
        }
    };
}
