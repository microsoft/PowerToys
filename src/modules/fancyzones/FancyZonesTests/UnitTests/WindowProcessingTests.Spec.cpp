#include "pch.h"

#include <FancyZonesLib/FancyZonesWindowProcessing.h>
#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/WindowUtils.h>

#include "Util.h"

#include <CppUnitTestLogger.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace Microsoft
{
    namespace VisualStudio
    {
        namespace CppUnitTestFramework
        {
            template<>
            std::wstring ToString<FancyZonesWindowProcessing::ProcessabilityType>(const FancyZonesWindowProcessing::ProcessabilityType& type)
            {
                return std::to_wstring((int)type);
            }

        }
    }
}

namespace FancyZonesUnitTests
{
    TEST_CLASS (WindowProcessingUnitTests)
    {
        HINSTANCE hInst{};

        TEST_METHOD_CLEANUP(CleanUp)
        {
            FancyZonesSettings::instance().SetSettings(Settings{});
        }

        TEST_METHOD (SplashScreen)
        {
            HWND window = Mocks::WindowCreate(hInst, L"", L"MsoSplash");
            Assert::IsTrue(FancyZonesWindowUtils::IsSplashScreen(window));

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::SplashScreen, FancyZonesWindowProcessing::DefineWindowType(window));
        }

        TEST_METHOD (MinimizedWindow)
        {
            HWND window = Mocks::WindowCreate(hInst);
            ShowWindow(window, SW_MINIMIZE);
            std::this_thread::sleep_for(std::chrono::milliseconds(100)); // let ShowWindow finish
            Assert::IsTrue(IsIconic(window));

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Minimized, FancyZonesWindowProcessing::DefineWindowType(window));
        }

        TEST_METHOD (ToolWindow)
        {
            HWND window = Mocks::WindowCreate(hInst, L"", L"", WS_EX_TOOLWINDOW);
            Assert::IsFalse(FancyZonesWindowUtils::IsStandardWindow(window));

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::NonStandardWindow, FancyZonesWindowProcessing::DefineWindowType(window));
        }

        TEST_METHOD (InvisibleWindow)
        {
            HWND window = Mocks::WindowCreate(hInst);
            ShowWindow(window, SW_HIDE);
            std::this_thread::sleep_for(std::chrono::milliseconds(100)); // let ShowWindow finish
            Assert::IsFalse(FancyZonesWindowUtils::IsStandardWindow(window));

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::NonStandardWindow, FancyZonesWindowProcessing::DefineWindowType(window));
        }

        TEST_METHOD (PopupApp_OptionDisabled)
        {
            FancyZonesSettings::instance().SetSettings(Settings{ .allowSnapPopupWindows = false });
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, WS_TILEDWINDOW | WS_POPUP);

            // should always be processable
            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Processable, FancyZonesWindowProcessing::DefineWindowType(window));
        }

        TEST_METHOD (PopupApp_OptionEnabled)
        {
            FancyZonesSettings::instance().SetSettings(Settings{ .allowSnapPopupWindows = true });
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, WS_TILEDWINDOW | WS_POPUP);

            // should always be processable
            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Processable, FancyZonesWindowProcessing::DefineWindowType(window));
        }

        TEST_METHOD (PopupMenu_OptionDisabled)
        {
            FancyZonesSettings::instance().SetSettings(Settings{ .allowSnapPopupWindows = false });
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, WS_POPUP | WS_TILED | WS_CLIPCHILDREN | WS_CLIPSIBLINGS);

            // should always not be processable
            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::PopupMenu, FancyZonesWindowProcessing::DefineWindowType(window));
        }

        TEST_METHOD (PopupMenu_OptionEnabled)
        {
            FancyZonesSettings::instance().SetSettings(Settings{ .allowSnapPopupWindows = true });
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, WS_POPUP | WS_TILED | WS_CLIPCHILDREN | WS_CLIPSIBLINGS);

            // should always not be processable
            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::PopupMenu, FancyZonesWindowProcessing::DefineWindowType(window));
        }

        TEST_METHOD (PopupMenuEdge_OptionDisabled)
        {
            FancyZonesSettings::instance().SetSettings(Settings{ .allowSnapPopupWindows = false });
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, WS_POPUP | WS_TILED | WS_CLIPCHILDREN | WS_CLIPSIBLINGS | WS_THICKFRAME | WS_SIZEBOX);

            // should always not be processable
            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::PopupMenu, FancyZonesWindowProcessing::DefineWindowType(window));
        }

        TEST_METHOD (PopupMenuEdge_OptionEnabled)
        {
            FancyZonesSettings::instance().SetSettings(Settings{ .allowSnapPopupWindows = true });
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, WS_POPUP | WS_TILED | WS_CLIPCHILDREN | WS_CLIPSIBLINGS | WS_THICKFRAME | WS_SIZEBOX);

            // should always not be processable
            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::PopupMenu, FancyZonesWindowProcessing::DefineWindowType(window));
        }

        TEST_METHOD (ChildWindow_OptionDisabled)
        {
            FancyZonesSettings::instance().SetSettings(Settings{ .allowSnapChildWindows = false });
            HWND parentWindow = Mocks::WindowCreate(hInst, L"", L"", 0, WS_TILEDWINDOW);
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, 0, parentWindow);

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::ChildWindow, FancyZonesWindowProcessing::DefineWindowType(window));
        }

        TEST_METHOD (ChildWindow_OptionEnabled)
        {
            FancyZonesSettings::instance().SetSettings(Settings{ .allowSnapChildWindows = true });
            HWND parentWindow = Mocks::WindowCreate(hInst, L"", L"", 0, WS_TILEDWINDOW);
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, 0, parentWindow);

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Processable, FancyZonesWindowProcessing::DefineWindowType(window));
        }

        TEST_METHOD (ExcludedApp)
        {
            // case sensitive, should be uppercase
            // created window path: \VisualStudio\Common7\IDE\Extensions\TestPlatform\testhost.exe
            FancyZonesSettings::instance().SetSettings(Settings{ .excludedAppsArray = { L"TESTHOST" } });
            HWND window = Mocks::WindowCreate(hInst);

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Excluded, FancyZonesWindowProcessing::DefineWindowType(window));
        }

        TEST_METHOD (ProcessableWindow)
        {
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, WS_TILEDWINDOW);

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Processable, FancyZonesWindowProcessing::DefineWindowType(window));
        }
    };
}