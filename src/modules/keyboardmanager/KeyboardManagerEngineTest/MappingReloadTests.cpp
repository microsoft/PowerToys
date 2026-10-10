// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "MockedInput.h"
#include "TestHelpers.h"
#include <filesystem>
#include <keyboardmanager/KeyboardManagerEngineLibrary/KeyboardEventHandlers.h>
#include <keyboardmanager/KeyboardManagerEngineLibrary/State.h>
#include <keyboardmanager/common/Helpers.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingLogicTests
{
    TEST_CLASS (MappingReloadTests)
    {
    private:
        KeyboardManagerInput::MockedInput input;
        State state;
        std::filesystem::path settingsFolder;

        void SendKey(WORD key, bool keyUp = false)
        {
            input.SendVirtualInput({ { .type = INPUT_KEYBOARD, .ki = { .wVk = key, .dwFlags = keyUp ? KEYEVENTF_KEYUP : 0UL } } });
        }

        void ReloadConfiguration()
        {
            Assert::IsTrue(state.SaveSettingsToFolder(settingsFolder.wstring()));
            Assert::IsTrue(state.LoadSettingsFromFolder(settingsFolder.wstring()) == MappingConfigurationLoadResult::Loaded);
        }

    public:
        TEST_METHOD_INITIALIZE(Initialize)
        {
            TestHelpers::ResetTestEnv(input, state);
            input.SetHookProc([this](LowlevelKeyboardEvent* event) -> intptr_t {
                // Mirror the production hook's encoding before the remapping handler runs.
                event->lParam->vkCode = Helpers::EncodeKeyNumpadOrigin(event->lParam->vkCode, (event->lParam->flags & LLKHF_EXTENDED) != 0);
                return KeyboardEventHandlers::HandleSingleKeyRemapEvent(input, event, state);
            });

            wchar_t tempFile[MAX_PATH]{};
            const auto tempFolder = std::filesystem::temp_directory_path();
            Assert::IsTrue(GetTempFileNameW(tempFolder.c_str(), L"kmr", 0, tempFile) != 0);
            Assert::IsTrue(DeleteFileW(tempFile) != FALSE);
            settingsFolder = tempFile;
            Assert::IsTrue(std::filesystem::create_directory(settingsFolder));
        }

        TEST_METHOD_CLEANUP(Cleanup)
        {
            input.SetHookProc(nullptr);
            if (!settingsFolder.empty())
            {
                std::error_code error;
                std::filesystem::remove_all(settingsFolder, error);
            }
        }

        TEST_METHOD (NumpadToShift_SuccessfulReloadPreservesMatchingRelease)
        {
            Assert::IsTrue(state.AddSingleKeyRemap(VK_NUMPAD1, static_cast<DWORD>(VK_LSHIFT)));
            SendKey(VK_NUMPAD1);
            Assert::IsTrue(input.GetVirtualKeyState(VK_LSHIFT));
            Assert::IsTrue(input.GetVirtualKeyState(VK_SHIFT));

            ReloadConfiguration();

            // Shift changes this physical key's release from NumPad1 to nonextended End.
            SendKey(VK_END, true);
            Assert::IsFalse(input.GetVirtualKeyState(VK_LSHIFT));
            Assert::IsFalse(input.GetVirtualKeyState(VK_SHIFT));
        }

        TEST_METHOD (NumpadToShiftShortcut_SuccessfulReloadPreservesMatchingRelease)
        {
            Shortcut target(std::vector<int32_t>{ VK_LSHIFT, 'A' });
            Assert::IsTrue(state.AddSingleKeyRemap(VK_NUMPAD1, target));
            SendKey(VK_NUMPAD1);
            Assert::IsTrue(input.GetVirtualKeyState(VK_LSHIFT));
            Assert::IsTrue(input.GetVirtualKeyState(VK_SHIFT));
            Assert::IsTrue(input.GetVirtualKeyState('A'));

            ReloadConfiguration();

            SendKey(VK_END, true);
            Assert::IsFalse(input.GetVirtualKeyState(VK_LSHIFT));
            Assert::IsFalse(input.GetVirtualKeyState(VK_SHIFT));
            Assert::IsFalse(input.GetVirtualKeyState('A'));
        }
    };
}
