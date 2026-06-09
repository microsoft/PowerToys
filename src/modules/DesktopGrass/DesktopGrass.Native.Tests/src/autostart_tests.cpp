#include "../third_party/catch2/catch.hpp"
#include "AutoStart.h"
#include "Persistence.h"

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <atomic>
#include <filesystem>
#include <fstream>
#include <string>
#include <vector>

namespace {

std::wstring unique_subkey(const wchar_t* name) {
    static std::atomic<int> counter{0};
    return std::wstring(L"Software\\DesktopGrass.Test.")
        + std::to_wstring(GetCurrentProcessId()) + L"."
        + std::to_wstring(GetTickCount64()) + L"."
        + std::to_wstring(counter.fetch_add(1)) + L"."
        + name;
}

class AutoStartRegistrySandbox {
public:
    explicit AutoStartRegistrySandbox(const wchar_t* name) : subkey_(unique_subkey(name)) {
        RegDeleteTreeW(HKEY_CURRENT_USER, subkey_.c_str());
        autostart::SetRegistryKeyOverride(subkey_);
    }

    ~AutoStartRegistrySandbox() {
        autostart::SetRegistryKeyOverride(subkey_);
        autostart::SetEnabled(false);
        RegDeleteTreeW(HKEY_CURRENT_USER, subkey_.c_str());
        autostart::SetRegistryKeyOverride(L"");
        desktopgrass::persistence::SetStateFilePathForTest(L"");
    }

    const std::wstring& subkey() const { return subkey_; }

private:
    std::wstring subkey_;
};

std::wstring read_registry_value(const std::wstring& subkey) {
    HKEY key = nullptr;
    REQUIRE(RegOpenKeyExW(HKEY_CURRENT_USER, subkey.c_str(), 0, KEY_QUERY_VALUE, &key) == ERROR_SUCCESS);

    DWORD type = 0;
    DWORD byteCount = 0;
    const std::wstring valueName = autostart::GetRegistryValueName();
    REQUIRE(RegQueryValueExW(key, valueName.c_str(), nullptr, &type, nullptr, &byteCount) == ERROR_SUCCESS);
    REQUIRE(type == REG_SZ);

    std::vector<wchar_t> buffer(byteCount / sizeof(wchar_t) + 1);
    REQUIRE(RegQueryValueExW(
        key, valueName.c_str(), nullptr, &type,
        reinterpret_cast<BYTE*>(buffer.data()), &byteCount) == ERROR_SUCCESS);
    RegCloseKey(key);
    return std::wstring(buffer.data());
}

std::filesystem::path test_state_path(const char* name) {
    std::filesystem::path dir = std::filesystem::current_path()
        / ".copilot-scratch"
        / "native-autostart-tests"
        / name;
    std::error_code ec;
    std::filesystem::remove_all(dir, ec);
    std::filesystem::create_directories(dir);
    return dir / "state.json";
}

} // namespace

TEST_CASE("autostart is disabled when registry value is missing", "[autostart]") {
    AutoStartRegistrySandbox sandbox(L"missing");

    REQUIRE_FALSE(autostart::IsEnabled());
}

TEST_CASE("autostart enable creates registry value", "[autostart]") {
    AutoStartRegistrySandbox sandbox(L"enable");

    REQUIRE(autostart::SetEnabled(true));

    REQUIRE(autostart::IsEnabled());
}

TEST_CASE("autostart disable deletes registry value", "[autostart]") {
    AutoStartRegistrySandbox sandbox(L"disable");

    REQUIRE(autostart::SetEnabled(true));
    REQUIRE(autostart::SetEnabled(false));

    REQUIRE_FALSE(autostart::IsEnabled());
}

TEST_CASE("autostart registry value contains current exe path", "[autostart]") {
    AutoStartRegistrySandbox sandbox(L"path");

    REQUIRE(autostart::SetEnabled(true));

    REQUIRE(read_registry_value(sandbox.subkey()) == autostart::GetCurrentExePath());
}

TEST_CASE("autostart enable is idempotent", "[autostart]") {
    AutoStartRegistrySandbox sandbox(L"enable-idempotent");

    REQUIRE(autostart::SetEnabled(true));
    REQUIRE(autostart::SetEnabled(true));

    REQUIRE(autostart::IsEnabled());
}

TEST_CASE("autostart disable missing value is no-op", "[autostart]") {
    AutoStartRegistrySandbox sandbox(L"disable-missing");

    REQUIRE(autostart::SetEnabled(false));

    REQUIRE_FALSE(autostart::IsEnabled());
}

TEST_CASE("autostart persisted true reconciles registry on startup", "[autostart][persistence]") {
    AutoStartRegistrySandbox sandbox(L"persisted-true");
    const auto path = test_state_path("persisted-true");
    desktopgrass::persistence::SetStateFilePathForTest(path.wstring());

    desktopgrass::persistence::AppState state;
    state.autoStart = true;
    REQUIRE(desktopgrass::persistence::SaveAppState(state));

    desktopgrass::persistence::AppState loaded;
    REQUIRE(desktopgrass::persistence::LoadAppState(loaded));
    REQUIRE(autostart::ReconcileWithState(loaded.autoStart));

    REQUIRE(autostart::IsEnabled());
}

TEST_CASE("autostart persisted false reconciles registry on startup", "[autostart][persistence]") {
    AutoStartRegistrySandbox sandbox(L"persisted-false");
    const auto path = test_state_path("persisted-false");
    desktopgrass::persistence::SetStateFilePathForTest(path.wstring());

    REQUIRE(autostart::SetEnabled(true));
    desktopgrass::persistence::AppState state;
    state.autoStart = false;
    REQUIRE(desktopgrass::persistence::SaveAppState(state));

    desktopgrass::persistence::AppState loaded;
    REQUIRE(desktopgrass::persistence::LoadAppState(loaded));
    REQUIRE(autostart::ReconcileWithState(loaded.autoStart));

    REQUIRE_FALSE(autostart::IsEnabled());
}
