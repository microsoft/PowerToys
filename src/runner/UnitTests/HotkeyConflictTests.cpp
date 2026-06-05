// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CppUnitTest.h"

#include "../hotkey_conflict_detector.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RunnerUnitTests
{
    TEST_CLASS(HotkeyConflictTests)
    {
    public:
        TEST_METHOD(HasConflict_TwoModulesSameHotkey_InAppConflict)
        {
            using namespace HotkeyConflictDetector;

            auto& manager = HotkeyConflictManager::GetInstance();
            const Hotkey hotkey{ .win = false, .ctrl = false, .shift = false, .alt = false, .key = 'T' };

            Assert::IsTrue(manager.AddHotkey(hotkey, L"ModuleA", 1, true));
            Assert::AreEqual(static_cast<int>(InAppConflict), static_cast<int>(manager.HasConflict(hotkey, L"ModuleB", 1)));

            manager.RemoveHotkeyByModule(L"ModuleA");
            manager.RemoveHotkeyByModule(L"ModuleB");
        }
    };
}
