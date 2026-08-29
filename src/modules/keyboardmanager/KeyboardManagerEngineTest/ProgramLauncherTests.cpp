#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include <keyboardmanager/KeyboardManagerEngineLibrary/KeyboardEventHandlers.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingLogicTests
{
    TEST_CLASS (ProgramLauncherTests)
    {
    public:
        TEST_METHOD (NormalWindow_ShouldUseExplorerShell)
        {
            Assert::IsTrue(KeyboardEventHandlers::ProgramLauncher::ShouldUseExplorerShell(Shortcut::StartWindowType::Normal));
        }

        TEST_METHOD (HiddenWindow_ShouldUseCreateProcess)
        {
            Assert::IsFalse(KeyboardEventHandlers::ProgramLauncher::ShouldUseExplorerShell(Shortcut::StartWindowType::Hidden));
        }

        TEST_METHOD (EmptyWorkingDirectory_ShouldUseExecutableDirectory)
        {
            const auto workingDirectory = KeyboardEventHandlers::ProgramLauncher::GetWorkingDirectory(
                L"C:\\Program Files\\Example\\Example.exe",
                L"");

            Assert::AreEqual(L"C:\\Program Files\\Example", workingDirectory.c_str());
        }

        TEST_METHOD (ExecutableExtensionCheck_ShouldBeCaseInsensitive)
        {
            const auto workingDirectory = KeyboardEventHandlers::ProgramLauncher::GetWorkingDirectory(
                L"C:\\Tools\\Example.EXE",
                L"");

            Assert::AreEqual(L"C:\\Tools", workingDirectory.c_str());
        }

        TEST_METHOD (ConfiguredWorkingDirectory_ShouldTakePrecedence)
        {
            const auto workingDirectory = KeyboardEventHandlers::ProgramLauncher::GetWorkingDirectory(
                L"C:\\Program Files\\Example\\Example.exe",
                L"D:\\Application Data");

            Assert::AreEqual(L"D:\\Application Data", workingDirectory.c_str());
        }

        TEST_METHOD (ShellTargetWithoutWorkingDirectory_ShouldRemainEmpty)
        {
            const auto workingDirectory = KeyboardEventHandlers::ProgramLauncher::GetWorkingDirectory(
                L"C:\\Shortcuts\\Example.lnk",
                L"");

            Assert::IsTrue(workingDirectory.empty());
        }
    };
}
