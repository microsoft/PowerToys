#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include <keyboardmanager/KeyboardManagerEngineLibrary/KeyboardEventHandlers.h>
#include <filesystem>
#include <PowerScripts/PowerScripts.Client.Native/HostSession.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingLogicTests
{
    TEST_CLASS (ProgramLauncherTests)
    {
    public:
        TEST_METHOD (PowerScriptsHotkey_PreservesEncodedParameters)
        {
            const auto parsed = PowerScripts::ParseHotkeyInvocation(L"run sample --consumer keyboard-manager --no-consent --set-base64 bmFtZQB2YWx1ZQ==");
            Assert::IsTrue(parsed.has_value());
            Assert::AreEqual(size_t{ 6 }, parsed->size());
            Assert::AreEqual(L"bmFtZQB2YWx1ZQ==", parsed->back().c_str());
            Assert::IsTrue(std::find(parsed->begin(), parsed->end(), L"--no-consent") == parsed->end());
        }

        TEST_METHOD (PowerScriptsHotkey_RequestsConsentForCurrentAndLegacyBindings)
        {
            const auto current = PowerScripts::ParseHotkeyInvocation(L"run sample --consumer keyboard-manager");
            const auto legacy = PowerScripts::ParseHotkeyInvocation(L"run sample --no-consent");
            Assert::IsTrue(current.has_value());
            Assert::IsTrue(legacy.has_value());
            Assert::IsTrue(*current == *legacy);
            Assert::AreEqual(size_t{ 4 }, current->size());
        }

        TEST_METHOD (PowerScriptsHotkey_DoesNotInterceptOtherCommandsOrConsumers)
        {
            Assert::IsFalse(PowerScripts::ParseHotkeyInvocation(L"trust approve sample").has_value());
            Assert::IsFalse(PowerScripts::ParseHotkeyInvocation(L"run sample --consumer command-line --no-consent").has_value());
            Assert::IsFalse(PowerScripts::ParseHotkeyInvocation(L"run sample").has_value());
            Assert::IsFalse(PowerScripts::ParseHotkeyInvocation(L"run sample --consumer keyboard-manager --set-base64 \"value with spaces\"").has_value());
            Assert::IsFalse(PowerScripts::ParseHotkeyInvocation(L"run sample --consumer keyboard-manager --no-consent --root other").has_value());
        }

        TEST_METHOD (PowerScriptsHotkey_LaunchArgumentsAllowFirstUseConsent)
        {
            GUID id{};
            Assert::AreEqual(S_OK, CoCreateGuid(&id));
            wchar_t name[40]{};
            Assert::IsTrue(StringFromGUID2(id, name, ARRAYSIZE(name)) != 0);
            const auto directory = std::filesystem::temp_directory_path() / name;
            Assert::IsTrue(std::filesystem::create_directory(directory));
            const auto cleanup = wil::scope_exit([&]() { RemoveDirectoryW(directory.c_str()); });
            const auto executable = directory / L"PowerScripts.Host.exe";
            wil::unique_hfile file{ CreateFileW(executable.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_TEMPORARY | FILE_FLAG_DELETE_ON_CLOSE, nullptr) };
            Assert::IsTrue(static_cast<bool>(file));

            for (const auto arguments : { L"run sample --consumer keyboard-manager", L"run sample --consumer keyboard-manager --no-consent", L"run sample --no-consent" })
            {
                Shortcut shortcut;
                shortcut.runProgramFilePath = executable.wstring();
                shortcut.runProgramArgs = arguments;
                shortcut.alreadyRunningAction = Shortcut::ProgramAlreadyRunningAction::StartAnother;
                shortcut.elevationLevel = Shortcut::ElevationLevel::NonElevated;
                shortcut.startWindowType = Shortcut::StartWindowType::Hidden;
                bool launched = false;
                std::wstring actualArguments;
                KeyboardEventHandlers::CreateOrShowProcessForShortcut(
                    shortcut, [&](const std::wstring&, const std::wstring& actual, const std::wstring&) {
                        launched = true;
                        actualArguments = actual;
                    });
                Assert::IsTrue(launched);
                Assert::AreEqual(L"run sample --consumer keyboard-manager", actualArguments.c_str());
            }
        }

        TEST_METHOD (RelativeProgramPath_ShouldUseEngineCurrentDirectory)
        {
            const auto expected = (std::filesystem::current_path() / L"NonexistentProgramLauncherTest.exe").wstring();
            const auto actual = KeyboardEventHandlers::ProgramLauncher::ExpandAndGetAbsolutePath(L".\\NonexistentProgramLauncherTest.exe");

            Assert::IsTrue(actual.has_value());
            Assert::AreEqual(expected.c_str(), actual->c_str());
        }

        TEST_METHOD (RelativeStartInPath_ShouldUseEngineCurrentDirectory)
        {
            const auto expected = (std::filesystem::current_path() / L"ProgramLauncherTestWorkingDirectory").wstring();
            const auto actual = KeyboardEventHandlers::ProgramLauncher::ExpandAndGetAbsolutePath(L".\\Unused\\..\\ProgramLauncherTestWorkingDirectory");

            Assert::IsTrue(actual.has_value());
            Assert::AreEqual(expected.c_str(), actual->c_str());
        }

        TEST_METHOD (EnvironmentVariable_ShouldExpandBeforeResolvingPath)
        {
            const DWORD capacity = GetEnvironmentVariableW(L"SystemRoot", nullptr, 0);
            Assert::IsTrue(capacity != 0);
            std::wstring systemRoot(capacity, L'\0');
            const DWORD length = GetEnvironmentVariableW(L"SystemRoot", systemRoot.data(), capacity);
            Assert::IsTrue(length != 0 && length < capacity);
            systemRoot.resize(length);

            const auto expected = (std::filesystem::path{ systemRoot } / L"NonexistentProgramLauncherTest.exe").wstring();
            const auto actual = KeyboardEventHandlers::ProgramLauncher::ExpandAndGetAbsolutePath(L"%SystemRoot%\\System32\\..\\NonexistentProgramLauncherTest.exe");

            Assert::IsTrue(actual.has_value());
            Assert::AreEqual(expected.c_str(), actual->c_str());
        }

        TEST_METHOD (LongAbsolutePath_ShouldNotBeTruncated)
        {
            std::wstring expected = L"C:\\";
            for (int i = 0; i < 10; ++i)
            {
                expected += L"ProgramLauncherLongPathTestDirectory\\";
            }
            expected += L"Example.exe";
            Assert::IsTrue(expected.size() > MAX_PATH);

            const auto actual = KeyboardEventHandlers::ProgramLauncher::ExpandAndGetAbsolutePath(expected);

            Assert::IsTrue(actual.has_value());
            Assert::AreEqual(expected.c_str(), actual->c_str());
        }

        TEST_METHOD (EmptyProgramPath_ShouldNotResolveToCurrentDirectory)
        {
            Assert::IsFalse(KeyboardEventHandlers::ProgramLauncher::ExpandAndGetAbsolutePath(L"").has_value());
        }

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
