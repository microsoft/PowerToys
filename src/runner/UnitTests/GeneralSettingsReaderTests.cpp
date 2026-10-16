// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CppUnitTest.h"

#include "../general_settings_reader.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RunnerUnitTests
{
    TEST_CLASS (GeneralSettingsReaderTests)
    {
    public:
        TEST_METHOD_INITIALIZE(CreateSettingsFile)
        {
            winrt::init_apartment(winrt::apartment_type::single_threaded);
            wchar_t directory[MAX_PATH]{};
            const auto length = GetTempPathW(ARRAYSIZE(directory), directory);
            Assert::IsTrue(length > 0 && length < ARRAYSIZE(directory));
            wchar_t path[MAX_PATH]{};
            Assert::IsTrue(GetTempFileNameW(directory, L"pts", 0, path) != 0);
            m_settingsPath = path;
        }

        TEST_METHOD_CLEANUP(RemoveSettingsFile)
        {
            std::filesystem::remove(m_settingsPath);
            winrt::uninit_apartment();
        }

        TEST_METHOD (MissingFileAllowsFirstRunSettings)
        {
            Assert::IsTrue(std::filesystem::remove(m_settingsPath));

            const auto loaded = general_settings::load_from_file_checked(m_settingsPath);

            Assert::IsTrue(loaded.has_value());
            Assert::AreEqual(0U, loaded->Size());
            Assert::IsFalse(std::filesystem::exists(m_settingsPath));
        }

        TEST_METHOD (ValidEmptyObjectIsNotAReadFailure)
        {
            WriteSettings("{}");

            const auto loaded = general_settings::load_from_file_checked(m_settingsPath);

            Assert::IsTrue(loaded.has_value());
            Assert::AreEqual(0U, loaded->Size());
        }

        TEST_METHOD (ExistingEmptyFileDoesNotAllowSaving)
        {
            const auto loaded = general_settings::load_from_file_checked(m_settingsPath);

            Assert::IsFalse(loaded.has_value(), L"An existing empty file may be an in-progress write, not first-run settings.");
            Assert::AreEqual(0ULL, static_cast<unsigned long long>(std::filesystem::file_size(m_settingsPath)));
        }

        TEST_METHOD (WhitespaceOnlyFileDoesNotAllowSaving)
        {
            WriteSettings(" \r\n\t");

            Assert::IsFalse(general_settings::load_from_file_checked(m_settingsPath).has_value());
        }

        TEST_METHOD (EmptyFileCanBeReadAfterWriteCompletes)
        {
            Assert::IsFalse(general_settings::load_from_file_checked(m_settingsPath).has_value());
            WriteSettings(R"({"enabled":{"File Locksmith":false}})");

            const auto loaded = general_settings::load_from_file_checked(m_settingsPath);

            Assert::IsTrue(loaded.has_value());
            Assert::IsFalse(loaded->GetNamedObject(L"enabled").GetNamedBoolean(L"File Locksmith"));
        }

        TEST_METHOD (PartialJsonDoesNotAllowSaving)
        {
            const std::string partial = R"({"enabled":{"File Locksmith":)";
            WriteSettings(partial);

            Assert::IsFalse(general_settings::load_from_file_checked(m_settingsPath).has_value());
            Assert::AreEqual(partial, ReadSettings());
        }

        TEST_METHOD (ValidFilePreservesAllModulePreferences)
        {
            WriteSettings(R"({"enabled":{"FancyZones":true,"File Locksmith":false}})");

            const auto loaded = general_settings::load_from_file_checked(m_settingsPath);

            Assert::IsTrue(loaded.has_value());
            const auto enabled = loaded->GetNamedObject(L"enabled");
            Assert::AreEqual(2U, enabled.Size());
            Assert::IsTrue(enabled.GetNamedBoolean(L"FancyZones"));
            Assert::IsFalse(enabled.GetNamedBoolean(L"File Locksmith"));
        }

        TEST_METHOD (LockedFileDoesNotAllowSavingAndCanBeReadAfterUnlock)
        {
            WriteSettings(R"({"enabled":{"File Locksmith":false}})");
            {
                wil::unique_handle lock{ CreateFileW(m_settingsPath.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr) };
                Assert::IsTrue(lock.is_valid());

                Assert::IsFalse(general_settings::load_from_file_checked(m_settingsPath).has_value());
            }

            const auto loaded = general_settings::load_from_file_checked(m_settingsPath);
            Assert::IsTrue(loaded.has_value());
            Assert::IsFalse(loaded->GetNamedObject(L"enabled").GetNamedBoolean(L"File Locksmith"));
        }

    private:
        std::wstring m_settingsPath;

        void WriteSettings(const std::string& contents)
        {
            std::ofstream file;
            file.exceptions(std::ios::failbit | std::ios::badbit);
            file.open(m_settingsPath, std::ios::binary | std::ios::trunc);
            file << contents;
            file.close();
        }

        std::string ReadSettings()
        {
            std::ifstream file{ m_settingsPath, std::ios::binary };
            return { std::istreambuf_iterator<char>{ file }, std::istreambuf_iterator<char>{} };
        }
    };
}
