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

        TEST_METHOD (MinimizedWindow)
        {
            HWND window = Mocks::WindowCreate(hInst);
            ShowWindow(window, SW_MINIMIZE);
            std::this_thread::sleep_for(std::chrono::milliseconds(100)); // let ShowWindow finish
            Assert::IsTrue(IsIconic(window));

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Minimized, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsFalse(FancyZonesWindowProcessing::IsProcessable(window));
        }

        TEST_METHOD (ToolWindow)
        {
            HWND window = Mocks::WindowCreate(hInst, L"", L"", WS_EX_TOOLWINDOW);
            
            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::ToolWindow, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsFalse(FancyZonesWindowProcessing::IsProcessable(window));
        }

        TEST_METHOD (InvisibleWindow)
        {
            HWND window = Mocks::WindowCreate(hInst);
            ShowWindow(window, SW_HIDE);
            std::this_thread::sleep_for(std::chrono::milliseconds(100)); // let ShowWindow finish
            
            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::NotVisible, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsFalse(FancyZonesWindowProcessing::IsProcessable(window));
        }

        TEST_METHOD(NonRootWindow)
        {
            HWND rootWindow = Mocks::WindowCreate(hInst, L"RootWindow", L"", 0, WS_TILEDWINDOW | WS_CLIPCHILDREN);
            Assert::IsTrue(FancyZonesWindowUtils::IsRoot(rootWindow));

            HWND window = CreateWindow(WC_COMBOBOX, TEXT(""), CBS_DROPDOWN | CBS_HASSTRINGS | WS_CHILD | WS_OVERLAPPED | WS_VISIBLE, 0, 0, 10, 10, rootWindow, NULL, hInst, NULL);
            Assert::IsFalse(FancyZonesWindowUtils::IsRoot(window));

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::NonRootWindow, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsFalse(FancyZonesWindowProcessing::IsProcessable(window));
        }

        TEST_METHOD (Popup_App)
        {
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, WS_TILEDWINDOW | WS_POPUP);

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Processable, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsTrue(FancyZonesWindowProcessing::IsProcessable(window));
        }

        TEST_METHOD (Popup_Menu)
        {
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, WS_POPUP | WS_TILED | WS_CLIPCHILDREN | WS_CLIPSIBLINGS);

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::NonProcessablePopupWindow, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsFalse(FancyZonesWindowProcessing::IsProcessable(window));
        }

        TEST_METHOD (Popup_MenuEdge)
        {
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, WS_POPUP | WS_TILED | WS_CLIPCHILDREN | WS_CLIPSIBLINGS | WS_THICKFRAME | WS_SIZEBOX);

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::NonProcessablePopupWindow, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsFalse(FancyZonesWindowProcessing::IsProcessable(window));
        }

        TEST_METHOD (Popup_Calculator)
        {
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, WS_BORDER | WS_CLIPSIBLINGS | WS_DLGFRAME | WS_GROUP | WS_POPUP | WS_POPUPWINDOW | WS_SIZEBOX | WS_TABSTOP | WS_TILEDWINDOW);

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Processable, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsTrue(FancyZonesWindowProcessing::IsProcessable(window));
        }

        TEST_METHOD (Popup_CalculatorTopmost)
        {
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, WS_BORDER | WS_CAPTION | WS_CLIPSIBLINGS | WS_DLGFRAME | WS_OVERLAPPED | WS_POPUP | WS_POPUPWINDOW | WS_SIZEBOX | WS_SYSMENU | WS_THICKFRAME);

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Processable, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsTrue(FancyZonesWindowProcessing::IsProcessable(window));
        }

        TEST_METHOD(Popup_FacebookMessenger)
        {
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, WS_GROUP | WS_MAXIMIZEBOX | WS_MINIMIZEBOX | WS_POPUP | WS_TABSTOP | WS_THICKFRAME);

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Processable, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsTrue(FancyZonesWindowProcessing::IsProcessable(window));
        }

        TEST_METHOD (ChildWindow_OptionDisabled)
        {
            FancyZonesSettings::instance().SetSettings(Settings{ .allowSnapChildWindows = false });
            HWND parentWindow = Mocks::WindowCreate(hInst, L"", L"", 0, WS_TILEDWINDOW);
            if (!IsWindowVisible(parentWindow))
            {
                // skip the test if the parent window isn't visible.
                // test can run locally, but will fail in CI because of the configuration
                return;
            }

            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, 0, parentWindow);
            Assert::IsTrue(IsWindowVisible(window), L"Child window not visible");
            Assert::IsTrue(FancyZonesWindowUtils::HasVisibleOwner(window), L"Child window doesn't have visible owner");

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::ChildWindow, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsFalse(FancyZonesWindowProcessing::IsProcessable(window));
        }

        TEST_METHOD (ChildWindow_OptionEnabled)
        {
            FancyZonesSettings::instance().SetSettings(Settings{ .allowSnapChildWindows = true });
            HWND parentWindow = Mocks::WindowCreate(hInst, L"", L"", 0, WS_TILEDWINDOW);
            if (!IsWindowVisible(parentWindow))
            {
                // skip the test if the parent window isn't visible.
                // test can run locally, but will fail in CI because of the configuration
                return;
            }

            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, 0, parentWindow);

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Processable, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsTrue(FancyZonesWindowProcessing::IsProcessable(window));
        }

        TEST_METHOD (ExcludedApp_ByDefault)
        {
            // set class from the excluded list
            HWND window = Mocks::WindowCreate(hInst, L"", L"SysListView32");

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Excluded, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsFalse(FancyZonesWindowProcessing::IsProcessable(window));
        }

        TEST_METHOD (ExcludedApp_ByDefault_SplashScreen)
        {
            HWND window = Mocks::WindowCreate(hInst, L"", L"MsoSplash");

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Excluded, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsFalse(FancyZonesWindowProcessing::IsProcessable(window));
        }

        TEST_METHOD (ExcludedApp_ByUser)
        {
            // case sensitive, should be uppercase
            FancyZonesSettings::instance().SetSettings(Settings{ .excludedAppsArray = { L"TEST_EXCLUDED" } });

            // exclude by window title
            HWND window = Mocks::WindowCreate(hInst, L"Test_Excluded");

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Excluded, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsFalse(FancyZonesWindowProcessing::IsProcessable(window));
        }

        TEST_METHOD (ProcessableWindow)
        {
            HWND window = Mocks::WindowCreate(hInst, L"", L"", 0, WS_TILEDWINDOW);

            Assert::AreEqual(FancyZonesWindowProcessing::ProcessabilityType::Processable, FancyZonesWindowProcessing::DefineWindowType(window));
            Assert::IsTrue(FancyZonesWindowProcessing::IsProcessable(window));
        }
    };
}