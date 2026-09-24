#include "pch.h"

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include <filesystem>
#include <fstream>
#include <keyboardmanager/common/MappingConfiguration.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingUITests
{
    TEST_CLASS (MappingConfigurationTests)
    {
        std::filesystem::path settingsFolder;

        void WriteFile(const std::wstring& name, const std::string& contents)
        {
            std::ofstream file(settingsFolder / name, std::ios::binary);
            file << contents;
            file.close();
            Assert::IsTrue(file.good());
        }

    public:
        TEST_METHOD_INITIALIZE(CreateSettingsFolder)
        {
            wchar_t tempFile[MAX_PATH];
            const auto tempFolder = std::filesystem::temp_directory_path();
            Assert::IsTrue(GetTempFileNameW(tempFolder.c_str(), L"kmt", 0, tempFile) != 0);
            Assert::IsTrue(DeleteFileW(tempFile) != FALSE);
            settingsFolder = tempFile;
            Assert::IsTrue(std::filesystem::create_directory(settingsFolder));
        }

        TEST_METHOD_CLEANUP(RemoveSettingsFolder)
        {
            if (!settingsFolder.empty())
            {
                std::error_code error;
                std::filesystem::remove_all(settingsFolder, error);
            }
        }

        TEST_METHOD (MissingModuleSettings_FirstSaveCanBeReopened)
        {
            MappingConfiguration firstSession;
            Assert::IsTrue(firstSession.LoadSettingsFromFolder(settingsFolder.wstring()) == MappingConfigurationLoadResult::NewConfiguration);
            Assert::AreEqual(KeyboardManagerConstants::DefaultConfiguration, firstSession.currentConfig);
            Assert::IsTrue(firstSession.AddSingleKeyRemap(0x41, static_cast<DWORD>(0x42)));
            Assert::IsTrue(firstSession.SaveSettingsToFolder(settingsFolder.wstring()));
            Assert::IsFalse(std::filesystem::exists(settingsFolder / L"settings.json"));

            // The editor and engine use the same folder loader, including its default-profile
            // selection when a module settings file has not been created yet.
            MappingConfiguration nextSession;
            Assert::IsTrue(nextSession.LoadSettingsFromFolder(settingsFolder.wstring()) == MappingConfigurationLoadResult::Loaded);
            Assert::AreEqual(static_cast<size_t>(1), nextSession.singleKeyReMap.size());
            Assert::AreEqual(static_cast<DWORD>(0x42), std::get<DWORD>(nextSession.singleKeyReMap.at(0x41)));
            Assert::IsFalse(std::filesystem::exists(settingsFolder / L"settings.json"));
        }

        TEST_METHOD (SelectedMissingProfile_CanBeSavedAndReopened)
        {
            WriteFile(L"settings.json", R"({"properties":{"activeConfiguration":{"value":"custom"}}})");

            MappingConfiguration firstSession;
            Assert::IsTrue(firstSession.LoadSettingsFromFolder(settingsFolder.wstring()) == MappingConfigurationLoadResult::NewConfiguration);
            Assert::AreEqual(std::wstring(L"custom"), firstSession.currentConfig);
            Assert::IsTrue(firstSession.AddSingleKeyToTextRemap(0x41, L"sample text"));
            Assert::IsTrue(firstSession.SaveSettingsToFolder(settingsFolder.wstring()));
            Assert::IsFalse(std::filesystem::exists(settingsFolder / L"default.json"));

            MappingConfiguration nextSession;
            Assert::IsTrue(nextSession.LoadSettingsFromFolder(settingsFolder.wstring()) == MappingConfigurationLoadResult::Loaded);
            Assert::AreEqual(std::wstring(L"custom"), nextSession.currentConfig);
            Assert::AreEqual(std::wstring(L"sample text"), std::get<std::wstring>(nextSession.singleKeyToTextReMap.at(0x41)));
        }

        TEST_METHOD (InvalidModuleSettings_DoNotFallBackToDefaultProfile)
        {
            MappingConfiguration savedConfig;
            Assert::IsTrue(savedConfig.AddSingleKeyRemap(0x41, static_cast<DWORD>(0x42)));
            Assert::IsTrue(savedConfig.SaveSettingsToFolder(settingsFolder.wstring()));

            const std::string invalidSettings[] = {
                "{",
                "{}",
                R"({"properties":{"activeConfiguration":{"value":""}}})",
                R"({"properties":{"activeConfiguration":{"value":1}}})",
            };
            for (const auto& contents : invalidSettings)
            {
                WriteFile(L"settings.json", contents);
                MappingConfiguration loadedConfig;
                Assert::IsTrue(loadedConfig.LoadSettingsFromFolder(settingsFolder.wstring()) == MappingConfigurationLoadResult::Failed);
                Assert::IsTrue(loadedConfig.singleKeyReMap.empty());
            }
        }

        TEST_METHOD (UnreadableExistingProfile_IsNotTreatedAsNew)
        {
            // A directory at the profile path exists but cannot be read as a configuration.
            Assert::IsTrue(std::filesystem::create_directory(settingsFolder / L"default.json"));
            MappingConfiguration loadedConfig;
            Assert::IsTrue(loadedConfig.LoadSettingsFromFolder(settingsFolder.wstring()) == MappingConfigurationLoadResult::Failed);
        }

        TEST_METHOD (InvalidProfile_DoesNotReplaceExistingMappings)
        {
            MappingConfiguration loadedConfig;
            Assert::IsTrue(loadedConfig.AddSingleKeyRemap(0x43, static_cast<DWORD>(0x44)));

            // The valid first entry is parsed before the malformed entry. None of the partial
            // read may replace the configuration currently in use.
            WriteFile(L"default.json", R"({
                "remapKeys":{"inProcess":[
                    {"originalKeys":"65","newRemapKeys":"66"},
                    {"originalKeys":"invalid","newRemapKeys":"67"}]},
                "remapKeysToText":{"inProcess":[]},
                "remapShortcuts":{"global":[],"appSpecific":[]},
                "remapShortcutsToText":{"global":[],"appSpecific":[]}})");

            Assert::IsTrue(loadedConfig.LoadSettingsFromFolder(settingsFolder.wstring()) == MappingConfigurationLoadResult::Failed);
            Assert::AreEqual(static_cast<size_t>(1), loadedConfig.singleKeyReMap.size());
            Assert::AreEqual(static_cast<DWORD>(0x44), std::get<DWORD>(loadedConfig.singleKeyReMap.at(0x43)));
        }

        TEST_METHOD (ProgramMapping_SaveAndReloadPreservesStartupOptions)
        {
            MappingConfiguration savedConfig;
            Shortcut origin(L"17;65");
            origin.exactMatch = true;
            Shortcut program;
            program.operationType = Shortcut::OperationType::RunProgram;
            program.runProgramFilePath = L"C:\\Apps\\Example.exe";
            program.runProgramArgs = L"--test";
            program.runProgramStartInDir = L"C:\\Working directory";
            program.elevationLevel = Shortcut::ElevationLevel::Elevated;
            program.alreadyRunningAction = Shortcut::ProgramAlreadyRunningAction::StartAnother;
            program.startWindowType = Shortcut::StartWindowType::Hidden;
            Assert::IsTrue(savedConfig.AddOSLevelShortcut(origin, program));
            Assert::IsTrue(savedConfig.AddAppSpecificShortcut(L"example.exe", origin, program));
            Assert::IsTrue(savedConfig.SaveSettingsToFolder(settingsFolder.wstring()));

            MappingConfiguration loadedConfig;
            Assert::IsTrue(loadedConfig.LoadSettingsFromFolder(settingsFolder.wstring()) == MappingConfigurationLoadResult::Loaded);
            const auto& globalProgram = std::get<Shortcut>(loadedConfig.osLevelShortcutReMap.at(origin).targetShortcut);
            const auto& appProgram = std::get<Shortcut>(loadedConfig.appSpecificShortcutReMap.at(L"example.exe").at(origin).targetShortcut);
            for (const auto* loadedProgram : { &globalProgram, &appProgram })
            {
                Assert::AreEqual(program.runProgramFilePath, loadedProgram->runProgramFilePath);
                Assert::AreEqual(program.runProgramArgs, loadedProgram->runProgramArgs);
                Assert::AreEqual(program.runProgramStartInDir, loadedProgram->runProgramStartInDir);
                Assert::IsTrue(program.elevationLevel == loadedProgram->elevationLevel);
                Assert::IsTrue(program.alreadyRunningAction == loadedProgram->alreadyRunningAction);
                Assert::IsTrue(program.startWindowType == loadedProgram->startWindowType);
            }

            Assert::IsTrue(loadedConfig.osLevelShortcutReMap.begin()->first.exactMatch);
            Assert::IsTrue(loadedConfig.appSpecificShortcutReMap.at(L"example.exe").begin()->first.exactMatch);
        }
    };
}
