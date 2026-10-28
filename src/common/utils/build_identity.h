#pragma once

namespace PowerToysBuildIdentity
{
#ifdef POWERTOYS_LOCAL_E2E_BUILD
    inline constexpr wchar_t SettingsFolder[] = L"Microsoft\\PowerToysLocalE2E";
    inline constexpr wchar_t DiagnosticsKey[] = L"Software\\Classes\\PowerToysLocalE2E";
    inline constexpr wchar_t StartupTaskFolder[] = L"\\PowerToysLocalE2E";
    inline constexpr wchar_t TrayClass[] = L"PToyTrayIconWindowLocalE2E";
#else
    inline constexpr wchar_t SettingsFolder[] = L"Microsoft\\PowerToys";
    inline constexpr wchar_t DiagnosticsKey[] = L"Software\\Classes\\PowerToys";
    inline constexpr wchar_t StartupTaskFolder[] = L"\\PowerToys";
    inline constexpr wchar_t TrayClass[] = L"PToyTrayIconWindow";
#endif
}
