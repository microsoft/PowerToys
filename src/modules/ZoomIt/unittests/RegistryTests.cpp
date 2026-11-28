#include "pch.h"
#include <CppUnitTest.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace ZoomItRegistryTests
{
    TEST_CLASS (StringSettingsTests)
    {
    public:
        TEST_METHOD (ClearedFileReplacesPreviouslyLoadedPath)
        {
            const auto key = L"Software\\PowerToys.ZoomIt.RegistryTests." +
                std::to_wstring(GetCurrentProcessId()) + L"." + std::to_wstring(GetTickCount64());
            auto cleanup = wil::scope_exit([&] { RegDeleteKey(HKEY_CURRENT_USER, key.c_str()); });
            ClassRegistry registry(key.c_str());
            TCHAR path[MAX_PATH] = L"C:\\Demos\\previous-script.txt";
            REG_SETTING settings[] = {
                { L"DemoTypeFile", SETTING_TYPE_STRING, sizeof(path), path, 0 },
                {}
            };

            registry.WriteRegSettings(settings);
            path[0] = L'\0';
            registry.WriteRegSettings(settings);

            // A running ZoomIt instance reloads into its existing filename buffer.
            wcscpy_s(path, L"C:\\Demos\\previous-script.txt");
            registry.ReadRegSettings(settings);
            Assert::AreEqual(L"", path);

            DWORD size = 0;
            DWORD type = 0;
            wil::unique_hkey handle;
            Assert::AreEqual(static_cast<LSTATUS>(ERROR_SUCCESS),
                RegOpenKeyEx(HKEY_CURRENT_USER, key.c_str(), 0, KEY_QUERY_VALUE, handle.put()));
            Assert::AreEqual(static_cast<LSTATUS>(ERROR_SUCCESS),
                RegQueryValueEx(handle.get(), L"DemoTypeFile", nullptr, &type, nullptr, &size));
            Assert::AreEqual(static_cast<DWORD>(REG_SZ), type);
            Assert::AreEqual(static_cast<DWORD>(sizeof(TCHAR)), size);
        }
    };
}
